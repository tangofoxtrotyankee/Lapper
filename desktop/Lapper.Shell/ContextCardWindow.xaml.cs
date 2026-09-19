using Lapper.Actions;
using Lapper.Contracts;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.System;

namespace Lapper.Shell;

/// <summary>
/// The context card: streams the orientation, shows facts/warnings and the
/// allowlisted suggested actions, streams cloud action results, and takes
/// follow-up questions. All content is rendered as plain text — screen
/// content and model output are hostile and never interpreted.
/// </summary>
public sealed partial class ContextCardWindow : Window
{
    private static readonly SizeInt32 BaseSize = new(440, 460); // at 96 DPI

    private readonly double _scale;
    private readonly ActionDispatcher _actions;
    private string _resultTextValue = string.Empty;
    private OrientationResult? _currentResult;

    /// <summary>A suggested action button was clicked (already allowlist-filtered).</summary>
    public event EventHandler<SuggestedAction>? ActionRequested;

    /// <summary>The user asked a follow-up question.</summary>
    public event EventHandler<string>? QuestionAsked;

    /// <summary>The card was hidden (Esc/Hide) — cancel in-flight work.</summary>
    public event EventHandler? Hidden;

    /// <summary>The user asked to re-read the screen.</summary>
    public event EventHandler? RefreshRequested;

    public bool DeepMode => DeepToggle.IsChecked == true;

    /// <summary>Best text for copy/read-aloud right now: result, else orientation+summary.</summary>
    public string CurrentReadableText =>
        _resultTextValue.Length > 0
            ? _resultTextValue
            : (_currentResult is null
                ? OrientationText.Text
                : $"{_currentResult.Orientation} {_currentResult.Summary}".Trim());

    public ContextCardWindow(ActionDispatcher actions)
    {
        _actions = actions;
        InitializeComponent();

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _scale = Interop.Win32.GetDpiForWindow(hwnd) / 96.0;

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.IsAlwaysOnTop = true;
        }
        AppWindow.IsShownInSwitchers = false;
        AppWindow.Resize(new SizeInt32((int)(BaseSize.Width * _scale), (int)(BaseSize.Height * _scale)));

        AppWindow.Closing += (_, e) =>
        {
            e.Cancel = true;
            HideCard();
        };
    }

    // ---- session lifecycle (called by the orchestrator on the UI thread) ----

    public void BeginSession()
    {
        Spinner.IsActive = true;
        StatusRow.Visibility = Visibility.Visible;
        StatusText.Text = "Reading this screen…";
        OrientationText.Text = string.Empty;
        OrientationText.Visibility = Visibility.Collapsed;
        SummaryText.Visibility = Visibility.Collapsed;
        FactsPanel.Children.Clear();
        FactsPanel.Visibility = Visibility.Collapsed;
        WarningsText.Visibility = Visibility.Collapsed;
        ActionsPanel.Children.Clear();
        ActionsPanel.Visibility = Visibility.Collapsed;
        ResultPanel.Visibility = Visibility.Collapsed;
        ResultText.Text = string.Empty;
        _resultTextValue = string.Empty;
        _currentResult = null;
        CopyResultButton.IsEnabled = false;
        ReadResultButton.IsEnabled = false;
    }

    public void ShowBlocked(string message)
    {
        Spinner.IsActive = false;
        StatusText.Text = message;
    }

    public void UpdateOrientation(string text)
    {
        Spinner.IsActive = true;
        StatusText.Text = "Orienting…";
        OrientationText.Text = text;
        OrientationText.Visibility = Visibility.Visible;
    }

    public void ShowOrientation(OrientationResult result, IReadOnlyList<SuggestedAction> allowedActions)
    {
        _currentResult = result;
        Spinner.IsActive = false;
        StatusRow.Visibility = Visibility.Collapsed;
        OrientationText.Text = result.Orientation;
        OrientationText.Visibility = Visibility.Visible;

        if (result.Summary.Trim().Length > 0 && result.Summary.Trim() != result.Orientation.Trim())
        {
            SummaryText.Text = result.Summary;
            SummaryText.Visibility = Visibility.Visible;
        }

        if (result.Facts.Count > 0)
        {
            foreach (var fact in result.Facts)
            {
                FactsPanel.Children.Add(new TextBlock
                {
                    Text = $"• {fact.Label}: {fact.Value}",
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true,
                });
            }
            FactsPanel.Visibility = Visibility.Visible;
        }

        if (result.Warnings.Count > 0)
        {
            WarningsText.Text = string.Join('\n', result.Warnings);
            WarningsText.Visibility = Visibility.Visible;
        }

        foreach (var action in allowedActions.Take(4))
        {
            var button = new Button { Content = action.Label };
            var captured = action;
            var armed = false;
            button.Click += (_, _) =>
            {
                if (ActionPolicy.NeedsConfirmation(captured) && !armed)
                {
                    armed = true;
                    button.Content = $"Confirm: {captured.Label}";
                    return;
                }
                ActionRequested?.Invoke(this, captured);
            };
            ActionsPanel.Children.Add(button);
        }
        ActionsPanel.Visibility = allowedActions.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public void ShowError(string message, string code)
    {
        Spinner.IsActive = false;
        StatusRow.Visibility = Visibility.Visible;
        StatusText.Text = $"{message} ({code})";
    }

    public void NotifyLocalActionDone(string actionType)
    {
        StatusRow.Visibility = Visibility.Visible;
        Spinner.IsActive = false;
        StatusText.Text = actionType switch
        {
            "copy_text" => "Copied to clipboard.",
            "share_text" => "Copied to clipboard, ready to share.",
            "read_aloud" => "Reading aloud…",
            _ => string.Empty,
        };
    }

    public void NotifyLocalActionFailed(string actionType)
    {
        StatusRow.Visibility = Visibility.Visible;
        Spinner.IsActive = false;
        StatusText.Text = actionType switch
        {
            "copy_text" or "share_text" => "Couldn't access the clipboard — try again.",
            "read_aloud" => "Read aloud isn't available on this device.",
            _ => "That didn't work.",
        };
    }

    /// <summary>Status note without switching the card into an error state.</summary>
    public void ShowStatusNote(string message)
    {
        StatusRow.Visibility = Visibility.Visible;
        Spinner.IsActive = false;
        StatusText.Text = message;
    }

    // ---- cloud action result streaming ----

    public void BeginActionResult()
    {
        ResultPanel.Visibility = Visibility.Visible;
        ResultText.Text = "…";
        _resultTextValue = string.Empty;
        CopyResultButton.IsEnabled = false;
        ReadResultButton.IsEnabled = false;
    }

    public void UpdateActionResult(string text)
    {
        ResultText.Text = text;
    }

    public void CompleteActionResult(string text)
    {
        _resultTextValue = text;
        ResultText.Text = text;
        CopyResultButton.IsEnabled = true;
        ReadResultButton.IsEnabled = true;
    }

    public void FocusQuestionBox() => QuestionBox.Focus(FocusState.Programmatic);

    // ---- window management ----

    public void ShowCard()
    {
        PositionAboveBottomRight();
        AppWindow.Show();
        Activate();
    }

    /// <summary>
    /// Shows the card WITHOUT taking keyboard focus. Used while acquisition
    /// runs: the target window must stay focused or the UIA password gate
    /// would examine Lapper's own card instead of the probed window.
    /// </summary>
    public void ShowCardPassive()
    {
        PositionAboveBottomRight();
        AppWindow.Show(activateWindow: false);
    }

    /// <summary>Takes focus once capture is over, so Esc and buttons work.</summary>
    public void ActivateForInput() => Activate();

    public void HideCard()
    {
        AppWindow.Hide();
        Hidden?.Invoke(this, EventArgs.Empty);
    }

    private void PositionAboveBottomRight()
    {
        var workArea = DisplayArea.Primary.WorkArea;
        const int margin = 24;
        var size = AppWindow.Size;
        AppWindow.Move(new PointInt32(
            workArea.X + workArea.Width - size.Width - margin,
            workArea.Y + workArea.Height - size.Height - margin));
    }

    // ---- input handlers ----

    private void OnHideClicked(object sender, RoutedEventArgs e) => HideCard();

    private void OnRefreshClicked(object sender, RoutedEventArgs e) =>
        RefreshRequested?.Invoke(this, EventArgs.Empty);

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            HideCard();
            e.Handled = true;
        }
    }

    private void OnAskClicked(object sender, RoutedEventArgs e) => SubmitQuestion();

    private void OnQuestionKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            SubmitQuestion();
            e.Handled = true;
        }
    }

    private void SubmitQuestion()
    {
        var question = QuestionBox.Text.Trim();
        if (question.Length == 0)
        {
            return;
        }
        QuestionAsked?.Invoke(this, question);
        QuestionBox.Text = string.Empty;
    }

    private void OnCopyResultClicked(object sender, RoutedEventArgs e)
    {
        if (_resultTextValue.Length > 0)
        {
            if (ClipboardService.TrySetText(_resultTextValue))
            {
                NotifyLocalActionDone("copy_text");
            }
            else
            {
                NotifyLocalActionFailed("copy_text");
            }
        }
    }

    private async void OnReadResultClicked(object sender, RoutedEventArgs e)
    {
        if (_resultTextValue.Length == 0)
        {
            return;
        }
        // async void event handler: an escaping exception would crash the
        // app, so failures surface as a status note instead.
        try
        {
            var ok = await _actions.ExecuteLocalAsync("read_aloud", _resultTextValue);
            if (ok)
            {
                NotifyLocalActionDone("read_aloud");
            }
            else
            {
                NotifyLocalActionFailed("read_aloud");
            }
        }
        catch (Exception)
        {
            NotifyLocalActionFailed("read_aloud");
        }
    }
}
