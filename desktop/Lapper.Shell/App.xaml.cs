using Lapper.Shell.Core;
using Lapper.Shell.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace Lapper.Shell;

/// <summary>
/// Application coordinator for the Phase 1 shell: wires the tray icon,
/// floating pill, context card, settings window and global shortcut
/// together. No screen capture, no AI calls in this phase.
/// </summary>
public partial class App : Application
{
    private DispatcherQueue? _dispatcher;
    private SettingsService? _settings;
    private StartupService? _startup;
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

        // A second launch was redirected here (see Program.Main): surface the card.
        AppInstance.GetCurrent().Activated += (_, _) => _dispatcher?.TryEnqueue(ShowCard);

        _settings = new SettingsService();
        _startup = new StartupService();

        _card = new ContextCardWindow();
        _pill = new PillWindow(_settings);
        _pill.Clicked += (_, _) => ShowCard();

        _hotkey = new HotkeyService(ShowCard);
        _hotkey.TryRegister(_settings.Gesture);

        _tray = new TrayIconService(
            openCard: ShowCard,
            openSettings: ShowSettings,
            isPillVisible: () => _settings!.PillVisible,
            setPillVisible: SetPillVisible,
            exit: ExitApp);

        if (_settings.PillVisible)
        {
            _pill.ShowPill();
        }
    }

    private void ShowCard() => _card?.ShowCard();

    private void ShowSettings()
    {
        _settingsWindow ??= new SettingsWindow(_settings!, _startup!, ApplyGesture, SetPillVisible);
        _settingsWindow.ShowSettings();
    }

    private bool ApplyGesture(ShortcutGesture gesture)
    {
        if (_hotkey is null || !_hotkey.TryRegister(gesture))
        {
            // Keep the previous stored gesture registered so the user is never
            // left without a working shortcut.
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
        _settings?.Dispose();
        Exit();
    }
}
