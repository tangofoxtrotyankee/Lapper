using Lapper.Actions;
using Lapper.ApiClient;
using Lapper.Context.Windows;
using Lapper.Contracts;
using Lapper.Shell.Core;
using Microsoft.UI.Dispatching;

namespace Lapper.Shell.Services;

/// <summary>
/// Drives the core loop: trigger → probe (FIRST, before any Lapper window
/// takes foreground) → show card → local capture → privacy-filtered request
/// → SSE orientation into the card → actions.
///
/// Threading contract: public methods run on the UI thread; streaming runs
/// on background continuations and repaints via DispatcherQueue with a
/// session-counter guard so a cancelled request's late events can never
/// touch a newer session's card.
/// </summary>
public sealed class LapperOrchestrator : IDisposable
{
    private const string ClientVersion = "0.2.0";

    private readonly SettingsService _settings;
    private readonly IContextAcquisitionService _acquisition;
    private readonly ActionDispatcher _actions;
    private readonly DispatcherQueue _dispatcher;
    private readonly HttpClient _httpClient = new() { Timeout = Timeout.InfiniteTimeSpan };
    private readonly LapperApiClient _api;

    private ContextCardWindow? _card;
    private int _session;
    private CancellationTokenSource? _cts;
    private ContextSnapshot? _lastSnapshot;
    private ForegroundWindowInfo? _lastProbe;
    private int _actionRun;
    private CancellationTokenSource? _actionCts;

    public LapperOrchestrator(
        SettingsService settings,
        IContextAcquisitionService acquisition,
        ActionDispatcher actions,
        DispatcherQueue dispatcher)
    {
        _settings = settings;
        _acquisition = acquisition;
        _actions = actions;
        _dispatcher = dispatcher;
        _api = new LapperApiClient(_httpClient, () =>
            BackendUrlValidator.TryValidate(_settings.BackendUrl, out var url)
                ? url!
                : new Uri(SettingsService.DefaultBackendUrl));
    }

    public void AttachCard(ContextCardWindow card)
    {
        _card = card;
        card.ActionRequested += (_, action) => OnActionRequested(action);
        card.QuestionAsked += (_, question) => _ = RunCloudActionAsync("ask_question", question);
        card.Hidden += (_, _) => CancelActiveSession();
    }

    /// <summary>The global trigger: hotkey, pill click, tray, second launch.</summary>
    public async Task TriggerAsync()
    {
        if (_card is null)
        {
            return;
        }

        // Probe BEFORE the card takes foreground, or we'd capture Lapper.
        var probe = ForegroundWindowProbe.Probe();

        // Triggered from Lapper's own UI (Refresh button, hotkey while the
        // card has focus): the foreground window IS Lapper, which the
        // exclusion policy would rightly block as self-capture. Re-read the
        // window from the previous session instead, if it still exists.
        if (probe is not null && probe.Identity.ProcessId == Environment.ProcessId)
        {
            probe = _lastProbe is not null && ForegroundWindowProbe.IsWindowAlive(_lastProbe.Hwnd)
                ? _lastProbe
                : null;
        }
        if (probe is not null)
        {
            _lastProbe = probe;
        }

        var session = ++_session;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _actions.StopSpeech();
        _lastSnapshot = null;

        _card.BeginSession();
        // Passive show: the card must NOT take focus yet, or the UIA
        // password gate would examine Lapper's own card instead of the
        // target window. Focus moves to the card after acquisition.
        _card.ShowCardPassive();

        if (probe is null)
        {
            _card.ActivateForInput();
            _card.ShowBlocked("No readable window is in the foreground.");
            return;
        }

        ContextAcquisitionResult result;
        try
        {
            result = await _acquisition.AcquireAsync(probe, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception)
        {
            Post(session, card =>
            {
                card.ActivateForInput();
                card.ShowError("Could not read this screen.", "CAPTURE_FAILED");
            });
            return;
        }

        if (session != _session)
        {
            return;
        }
        _card.ActivateForInput();

        switch (result.Outcome)
        {
            case AcquisitionOutcome.ExcludedApp:
                _card.ShowBlocked(result.Exclusion?.Reason switch
                {
                    Privacy.Exclusions.ExclusionReason.UserExcludedApp =>
                        "This app is on your exclusion list, so Lapper didn't look at it.",
                    Privacy.Exclusions.ExclusionReason.UnknownProcess =>
                        "Lapper can't identify this app, so nothing was captured.",
                    _ => "This app is protected, so Lapper didn't look at it.",
                });
                return;
            case AcquisitionOutcome.SensitiveContentBlocked:
                _card.ShowBlocked("A password field has focus here, so Lapper didn't look at this screen.");
                return;
            case AcquisitionOutcome.NoForegroundWindow:
            case AcquisitionOutcome.Failed:
                _card.ShowBlocked("Lapper couldn't extract anything useful from this screen.");
                return;
        }

        var snapshot = result.Snapshot!;
        _lastSnapshot = snapshot;
        await StreamOrientationAsync(session, snapshot, ct);
    }

    private async Task StreamOrientationAsync(int session, ContextSnapshot snapshot, CancellationToken ct)
    {
        var request = new Contracts.OrientRequest
        {
            RequestId = Guid.NewGuid().ToString(),
            Application = new ApplicationInfo
            {
                ProcessName = Clamp(snapshot.ProcessName, 200),
                WindowTitle = null, // window titles stay local
                Category = snapshot.Category,
            },
            Context = BuildPayload(snapshot),
            Options = _card!.DeepMode ? new Contracts.RequestOptions { Deep = true } : null,
            Client = new ClientInfo
            {
                Version = ClientVersion,
                Capabilities = BuildCapabilities(),
            },
        };

        var extractor = new OrientationDeltaExtractor();
        var sawTerminal = false;
        try
        {
            await foreach (var apiEvent in _api.OrientAsync(request, ct).ConfigureAwait(false))
            {
                switch (apiEvent)
                {
                    case ApiStreamEvent.Delta(var text):
                        if (extractor.Feed(text))
                        {
                            var visible = extractor.Text;
                            Post(session, card => card.UpdateOrientation(visible));
                        }
                        break;
                    case ApiStreamEvent.OrientResult(var orientation):
                        sawTerminal = true;
                        Post(session, card => card.ShowOrientation(
                            orientation,
                            ActionPolicy.FilterAllowed(orientation.SuggestedActions)));
                        break;
                    case ApiStreamEvent.Error(var code, var message):
                        Post(session, card => card.ShowError(message, code));
                        return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception)
        {
            Post(session, card => card.ShowError("Couldn't reach the Lapper backend.", "BACKEND_UNREACHABLE"));
            return;
        }

        // A clean connection close (proxy idle timeout, early server end)
        // raises no exception — without this the card would spin forever.
        if (!sawTerminal && !ct.IsCancellationRequested)
        {
            Post(session, card => card.ShowError("The connection ended unexpectedly.", "STREAM_ENDED"));
        }
    }

    private void OnActionRequested(SuggestedAction action)
    {
        if (!ActionPolicy.IsAllowed(action.Type))
        {
            return; // unknown model-proposed action: rejected
        }

        if (ActionPolicy.KindOf(action.Type) == ActionExecutionKind.Local)
        {
            var text = _card?.CurrentReadableText ?? string.Empty;
            _ = RunLocalActionAsync(action.Type, text);
            return;
        }

        if (action.Type == "ask_question")
        {
            _card?.FocusQuestionBox();
            return;
        }

        _ = RunCloudActionAsync(action.Type, null);
    }

    /// <summary>Runs on the UI thread; success/failure lands on the card.</summary>
    private async Task RunLocalActionAsync(string actionType, string text)
    {
        var ok = false;
        try
        {
            ok = await _actions.ExecuteLocalAsync(actionType, text);
        }
        catch (Exception)
        {
            // Local actions must never crash the shell.
        }
        if (ok)
        {
            _card?.NotifyLocalActionDone(actionType);
        }
        else
        {
            _card?.NotifyLocalActionFailed(actionType);
        }
    }

    private async Task RunCloudActionAsync(string actionType, string? question)
    {
        if (_card is null)
        {
            return;
        }
        if (_lastSnapshot is null || _cts is null)
        {
            // The question box is visible even after a blocked/failed
            // acquisition; say why nothing happens rather than going silent.
            _card.ShowStatusNote("Nothing was read from this screen, so that isn't available.");
            return;
        }
        var session = _session;

        // One cloud action at a time: a new one cancels and supersedes the
        // stream still writing to the shared result panel.
        var run = ++_actionRun;
        _actionCts?.Cancel();
        _actionCts?.Dispose();
        _actionCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        var ct = _actionCts.Token;

        var request = new Contracts.ActionRequest
        {
            RequestId = Guid.NewGuid().ToString(),
            Action = new ActionSpec
            {
                Type = actionType,
                Question = question is null ? null : Clamp(question, 2000),
            },
            Application = new ApplicationInfo
            {
                ProcessName = Clamp(_lastSnapshot.ProcessName, 200),
                WindowTitle = null,
                Category = _lastSnapshot.Category,
            },
            Context = BuildPayload(_lastSnapshot),
            Options = _card.DeepMode ? new Contracts.RequestOptions { Deep = true } : null,
            Client = new ClientInfo { Version = ClientVersion, Capabilities = BuildCapabilities() },
        };

        _card.BeginActionResult();
        var buffer = new System.Text.StringBuilder();
        var sawTerminal = false;
        try
        {
            await foreach (var apiEvent in _api.ActionAsync(request, ct).ConfigureAwait(false))
            {
                switch (apiEvent)
                {
                    case ApiStreamEvent.Delta(var text):
                        buffer.Append(text);
                        var visible = buffer.ToString();
                        PostAction(session, run, card => card.UpdateActionResult(visible));
                        break;
                    case ApiStreamEvent.TextResult(var text):
                        sawTerminal = true;
                        PostAction(session, run, card => card.CompleteActionResult(text));
                        break;
                    case ApiStreamEvent.Error(var code, var message):
                        PostAction(session, run, card => card.ShowError(message, code));
                        return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception)
        {
            Post(session, card => card.ShowError("Couldn't reach the Lapper backend.", "BACKEND_UNREACHABLE"));
            return;
        }

        if (!sawTerminal && !ct.IsCancellationRequested)
        {
            PostAction(session, run, card => card.ShowError("The connection ended unexpectedly.", "STREAM_ENDED"));
        }
    }

    public void CancelActiveSession()
    {
        _actionCts?.Cancel();
        _cts?.Cancel();
        _actions.StopSpeech();
    }

    private ScreenContextPayload BuildPayload(ContextSnapshot snapshot) => new()
    {
        SelectedText = snapshot.SelectedText is null ? null : Clamp(snapshot.SelectedText, 20000),
        Blocks = snapshot.Blocks,
        OcrText = snapshot.OcrText is null ? null : Clamp(snapshot.OcrText, 30000),
        ImageIncluded = false,
    };

    private static IReadOnlyList<string> BuildCapabilities()
    {
        var capabilities = new List<string> { "uia", "local_tts" };
        if (Context.Windows.Capture.OcrFallback.IsAvailable)
        {
            capabilities.Insert(1, "local_ocr");
        }
        return capabilities;
    }

    private static string Clamp(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private void Post(int session, Action<ContextCardWindow> update)
    {
        _dispatcher.TryEnqueue(() =>
        {
            if (session == _session && _card is not null)
            {
                update(_card);
            }
        });
    }

    /// <summary>A superseded cloud action's late events never repaint the panel.</summary>
    private void PostAction(int session, int run, Action<ContextCardWindow> update)
    {
        _dispatcher.TryEnqueue(() =>
        {
            if (session == _session && run == _actionRun && _card is not null)
            {
                update(_card);
            }
        });
    }

    public void Dispose()
    {
        _actionCts?.Cancel();
        _actionCts?.Dispose();
        _cts?.Cancel();
        _cts?.Dispose();
        _httpClient.Dispose();
    }
}
