using Lapper.Actions;
using Lapper.Context.Windows;
using Lapper.Privacy.Exclusions;
using Lapper.Privacy.Redaction;
using Lapper.Shell.Core;
using Lapper.Shell.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace Lapper.Shell;

/// <summary>
/// Application coordinator: tray, pill, card, settings, global shortcut,
/// and the orientation loop (capture → privacy filter → backend → card).
/// </summary>
public partial class App : Application
{
    private DispatcherQueue? _dispatcher;
    private SettingsService? _settings;
    private StartupService? _startup;
    private SpeechService? _speech;
    private ActionDispatcher? _actionDispatcher;
    private ContextAcquisitionService? _acquisition;
    private LapperOrchestrator? _orchestrator;
    private PillWindow? _pill;
    private ContextCardWindow? _card;
    private SettingsWindow? _settingsWindow;
    private TrayIconService? _tray;
    private HotkeyService? _hotkey;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        // A second launch was redirected here (see Program.Main): trigger.
        AppInstance.GetCurrent().Activated += (_, _) => _dispatcher?.TryEnqueue(Trigger);

        _settings = new SettingsService();
        _startup = new StartupService();
        _speech = new SpeechService();
        _actionDispatcher = new ActionDispatcher(_speech);
        _acquisition = new ContextAcquisitionService(
            new ExclusionPolicy(new SettingsExclusionSource(_settings)),
            new SecretRedactor());
        _orchestrator = new LapperOrchestrator(_settings, _acquisition, _actionDispatcher, _dispatcher!);

        _card = new ContextCardWindow(_actionDispatcher);
        _card.RefreshRequested += (_, _) => Trigger();
        _orchestrator.AttachCard(_card);

        _pill = new PillWindow(_settings);
        _pill.Clicked += (_, _) => Trigger();

        _hotkey = new HotkeyService(Trigger);
        _hotkey.TryRegister(_settings.Gesture);

        _tray = new TrayIconService(
            openCard: Trigger,
            openSettings: ShowSettings,
            isPillVisible: () => _settings!.PillVisible,
            setPillVisible: SetPillVisible,
            exit: ExitApp);

        if (_settings.PillVisible)
        {
            _pill.ShowPill();
        }
    }

    private void Trigger() => _ = _orchestrator?.TriggerAsync();

    private void ShowSettings()
    {
        _settingsWindow ??= new SettingsWindow(_settings!, _startup!, ApplyGesture, SetPillVisible);
        _settingsWindow.ShowSettings();
    }

    private bool ApplyGesture(ShortcutGesture gesture)
    {
        if (_hotkey is null || !_hotkey.TryRegister(gesture))
        {
            _hotkey?.TryRegister(_settings!.Gesture);
            return false;
        }

        _settings!.Gesture = gesture;
        return true;
    }

    private void SetPillVisible(bool visible)
    {
        _settings!.PillVisible = visible;
        if (visible)
        {
            _pill?.ShowPill();
        }
        else
        {
            _pill?.HidePill();
        }
    }

    private void ExitApp()
    {
        _tray?.Dispose();
        _hotkey?.Dispose();
        _orchestrator?.Dispose();
        _acquisition?.Dispose();
        _speech?.Dispose();
        _settings?.Dispose();
        Exit();
    }
}
