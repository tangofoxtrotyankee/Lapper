using Lapper.Shell.Core;
using Lapper.Shell.Interop;
using Lapper.Shell.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;

namespace Lapper.Shell;

/// <summary>
/// The floating pill: a small, frameless, always-on-top window that never
/// takes keyboard focus (WS_EX_NOACTIVATE), can be dragged anywhere on
/// screen, and remembers its position across restarts.
/// </summary>
public sealed partial class PillWindow : Window
{
    private static readonly PixelSize BasePillSize = new(180, 48); // at 96 DPI
    private const int ClickDragThresholdPx = 4;

    private readonly SettingsService _settings;
    private readonly nint _hwnd;
    private readonly PixelSize _pillSize;

    private bool _dragging;
    private bool _movedBeyondThreshold;
    private Win32.POINT _dragStartCursor;
    private PixelPoint _dragStartWindow;

    /// <summary>Raised when the pill is clicked (pressed and released without dragging).</summary>
    public event EventHandler? Clicked;

    public PillWindow(SettingsService settings)
    {
        _settings = settings;
        InitializeComponent();

        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        Win32.MakeNonActivating(_hwnd);

        var scale = Win32.GetDpiForWindow(_hwnd) / 96.0;
        _pillSize = new PixelSize(
            (int)(BasePillSize.Width * scale),
            (int)(BasePillSize.Height * scale));

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        AppWindow.IsShownInSwitchers = false;

        var restored = PillPlacement.Restore(
            _settings.PillPosition,
            _pillSize,
            ToPixelRect(DisplayArea.Primary.WorkArea),
            [.. DisplayArea.FindAll().Select(area => ToPixelRect(area.WorkArea))]);

        AppWindow.MoveAndResize(new RectInt32(
            restored.X, restored.Y, _pillSize.Width, _pillSize.Height));

        // The pill is hidden/shown by the app, never closed on its own; closing
        // the window would tear it down, so translate close into hide.
        AppWindow.Closing += (_, e) =>
        {
            e.Cancel = true;
            HidePill();
        };
    }

    public void ShowPill() => AppWindow.Show(activateWindow: false);

    public void HidePill() => AppWindow.Hide();

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _dragging = PillRoot.CapturePointer(e.Pointer);
        _movedBeyondThreshold = false;
        Win32.GetCursorPos(out _dragStartCursor);
        _dragStartWindow = new PixelPoint(AppWindow.Position.X, AppWindow.Position.Y);
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        Win32.GetCursorPos(out var cursor);
        var deltaX = cursor.X - _dragStartCursor.X;
        var deltaY = cursor.Y - _dragStartCursor.Y;
        if (Math.Abs(deltaX) + Math.Abs(deltaY) > ClickDragThresholdPx)
        {
            _movedBeyondThreshold = true;
        }

        var desired = new PixelPoint(_dragStartWindow.X + deltaX, _dragStartWindow.Y + deltaY);
        var workArea = ToPixelRect(
            DisplayArea.GetFromPoint(new PointInt32(cursor.X, cursor.Y), DisplayAreaFallback.Nearest)
                .WorkArea);
        var clamped = PillPlacement.Clamp(desired, _pillSize, workArea);
        AppWindow.Move(new PointInt32(clamped.X, clamped.Y));
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        PillRoot.ReleasePointerCapture(e.Pointer);

        if (_movedBeyondThreshold)
        {
            _settings.PillPosition = new PixelPoint(AppWindow.Position.X, AppWindow.Position.Y);
        }
        else
        {
            Clicked?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (_dragging)
        {
            _dragging = false;
            _settings.PillPosition = new PixelPoint(AppWindow.Position.X, AppWindow.Position.Y);
        }
    }

    private static PixelRect ToPixelRect(RectInt32 rect) =>
        new(rect.X, rect.Y, rect.Width, rect.Height);
}
