using Lapper.Shell.Core;
using Lapper.Shell.Services;
using Microsoft.UI.Xaml;

namespace Lapper.Shell;

/// <summary>
/// Minimal settings surface for Phase 1: the global shortcut, pill
/// visibility, and the default-off "start with Windows" option.
/// Closing hides the window; the app lives in the tray.
/// </summary>
public sealed partial class SettingsWindow : Window
{
    private readonly SettingsService _settings;
    private readonly StartupService _startup;
    private readonly Func<ShortcutGesture, bool> _applyGesture;
    private readonly Action<bool> _setPillVisible;
    private bool _suppressStartupToggle;

    public SettingsWindow(
        SettingsService settings,
        StartupService startup,
        Func<ShortcutGesture, bool> applyGesture,
        Action<bool> setPillVisible)
    {
        _settings = settings;
        _startup = startup;
        _applyGesture = applyGesture;
        _setPillVisible = setPillVisible;
        InitializeComponent();

        AppWindow.Closing += (_, e) =>
        {
            e.Cancel = true;
            AppWindow.Hide();
        };
    }

    public void ShowSettings()
    {
        ShortcutBox.Text = _settings.Gesture.Format();
        PillToggle.IsOn = _settings.PillVisible;
        ShortcutFeedback.Visibility = Visibility.Collapsed;
        SaveFeedback.Text = string.Empty;
        _ = RefreshStartupToggleAsync();

        AppWindow.Show();
        Activate();
    }

    private async Task RefreshStartupToggleAsync()
    {
        var enabled = await _startup.IsEnabledAsync();
        _suppressStartupToggle = true;
        StartupToggle.IsOn = enabled;
        _suppressStartupToggle = false;
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        if (!ShortcutGesture.TryParse(ShortcutBox.Text, out var gesture))
        {
            ShortcutFeedback.Text =
                "That shortcut isn't valid. Use at least one modifier plus a key, e.g. Ctrl+Alt+L.";
            ShortcutFeedback.Visibility = Visibility.Visible;
            return;
        }

        ShortcutFeedback.Visibility = Visibility.Collapsed;

        if (!_applyGesture(gesture!))
        {
            ShortcutFeedback.Text =
                "Windows rejected that shortcut (another app may already use it). Pick a different one.";
            ShortcutFeedback.Visibility = Visibility.Visible;
            return;
        }

        _setPillVisible(PillToggle.IsOn);
        SaveFeedback.Text = "Saved.";
    }

    private async void OnStartupToggled(object sender, RoutedEventArgs e)
    {
        if (_suppressStartupToggle)
        {
            return;
        }

        var result = await _startup.SetEnabledAsync(StartupToggle.IsOn);
        if (result != StartupToggle.IsOn)
        {
            // The OS refused (policy or user-disabled in Task Manager); reflect reality.
            _suppressStartupToggle = true;
            StartupToggle.IsOn = result;
            _suppressStartupToggle = false;
        }
    }
}
