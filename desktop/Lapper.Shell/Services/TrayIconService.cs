using H.NotifyIcon;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Lapper.Shell.Services;

/// <summary>
/// System tray icon and menu — the surface that makes Lapper fully
/// controllable without the floating pill: open the card, toggle the pill,
/// open settings, and the only way to exit the app.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly TaskbarIcon _icon;
    private readonly ToggleMenuFlyoutItem _pillItem;

    public TrayIconService(
        Action openCard,
        Action openSettings,
        Func<bool> isPillVisible,
        Action<bool> setPillVisible,
        Action exit)
    {
        var openItem = new MenuFlyoutItem { Text = "Open Lapper" };
        openItem.Click += (_, _) => openCard();

        _pillItem = new ToggleMenuFlyoutItem { Text = "Show floating pill" };
        _pillItem.Click += (_, _) => setPillVisible(_pillItem.IsChecked);

        var settingsItem = new MenuFlyoutItem { Text = "Settings" };
        settingsItem.Click += (_, _) => openSettings();

        var exitItem = new MenuFlyoutItem { Text = "Exit Lapper" };
        exitItem.Click += (_, _) => exit();

        var menu = new MenuFlyout();
        menu.Items.Add(openItem);
        menu.Items.Add(_pillItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(exitItem);
        menu.Opening += (_, _) => _pillItem.IsChecked = isPillVisible();

        _icon = new TaskbarIcon
        {
            ToolTipText = "Lapper",
            IconSource = new BitmapImage(new Uri("ms-appx:///Assets/Square44x44Logo.png")),
            ContextMenuMode = ContextMenuMode.SecondWindow,
            ContextFlyout = menu,
            LeftClickCommand = new DelegateCommand(openCard),
            NoLeftClickDelay = true,
        };
        _icon.ForceCreate();
    }

    public void Dispose() => _icon.Dispose();
}
