using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.System;

namespace Lapper.Shell;

public enum CardState
{
    Loading,
    Error,
    Success,
}

/// <summary>
/// The compact context card: the surface where orientation results will
/// stream from Phase 3. In Phase 1 it exists with explicit loading, error
/// and success states, a compact/expanded toggle, and hide-on-close.
/// </summary>
public sealed partial class ContextCardWindow : Window
{
    private static readonly SizeInt32 CompactSize = new(420, 240); // at 96 DPI
    private static readonly SizeInt32 ExpandedSize = new(420, 420);

    private readonly double _scale;
    private bool _expanded;

    public ContextCardWindow()
    {
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

        // Closing the card only hides it; the app lives in the tray.
        AppWindow.Closing += (_, e) =>
        {
            e.Cancel = true;
            HideCard();
        };

        SetState(CardState.Success);
        ApplySize();
    }

    public void ShowCard()
    {
        PositionAboveBottomRight();
        AppWindow.Show();
        Activate();
    }

    public void HideCard() => AppWindow.Hide();

    public void SetState(CardState state)
    {
        LoadingState.Visibility = state == CardState.Loading ? Visibility.Visible : Visibility.Collapsed;
        ErrorState.Visibility = state == CardState.Error ? Visibility.Visible : Visibility.Collapsed;
        SuccessState.Visibility = state == CardState.Success ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplySize()
    {
        var size = _expanded ? ExpandedSize : CompactSize;
        AppWindow.Resize(new SizeInt32((int)(size.Width * _scale), (int)(size.Height * _scale)));
        ExpandButton.Content = _expanded ? "Collapse" : "Expand";
        StatePreviewRow.Visibility = _expanded ? Visibility.Visible : Visibility.Collapsed;
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

    private void OnExpandClicked(object sender, RoutedEventArgs e)
    {
        _expanded = !_expanded;
        ApplySize();
        PositionAboveBottomRight();
    }

    private void OnHideClicked(object sender, RoutedEventArgs e) => HideCard();

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            HideCard();
            e.Handled = true;
        }
    }

    private void OnPreviewLoading(object sender, RoutedEventArgs e) => SetState(CardState.Loading);

    private void OnPreviewError(object sender, RoutedEventArgs e) => SetState(CardState.Error);

    private void OnPreviewSuccess(object sender, RoutedEventArgs e) => SetState(CardState.Success);
}
