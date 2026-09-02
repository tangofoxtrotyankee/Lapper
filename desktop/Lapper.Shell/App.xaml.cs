using Microsoft.UI.Xaml;

namespace Lapper.Shell;

/// <summary>
/// Application entry point. Phase 0 opens an empty window only; the floating
/// pill, tray icon and global shortcut arrive in Phase 1.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
