using System.Runtime.InteropServices;
using Lapper.Shell.Core;
using Lapper.Shell.Interop;

namespace Lapper.Shell.Services;

/// <summary>
/// Registers the configurable global shortcut via RegisterHotKey against a
/// hidden message-only window, so Lapper can be invoked from any app.
/// Created on the UI thread; WM_HOTKEY therefore arrives on the UI thread.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 1;

    private readonly nint _messageWindow;
    private readonly Win32.SubclassProc _subclassProc; // field: keeps the delegate alive
    private readonly Action _invoked;
    private bool _registered;

    public HotkeyService(Action invoked)
    {
        _invoked = invoked;
        _messageWindow = Win32.CreateWindowExW(
            0, "STATIC", null, 0, 0, 0, 0, 0, Win32.HWND_MESSAGE, 0, 0, 0);
        if (_messageWindow == 0)
        {
            throw new InvalidOperationException(
                $"Failed to create hotkey message window (error {Marshal.GetLastPInvokeError()}).");
        }

        _subclassProc = HandleMessage;
        Win32.SetWindowSubclass(_messageWindow, _subclassProc, 1, 0);
    }

    /// <summary>Registers the gesture, replacing any previous one. Returns false when the OS rejects it (e.g. already taken by another app).</summary>
    public bool TryRegister(ShortcutGesture gesture)
    {
        Unregister();
        _registered = Win32.RegisterHotKey(
            _messageWindow,
            HotkeyId,
            (uint)gesture.Modifiers | Win32.MOD_NOREPEAT,
            (uint)gesture.VirtualKeyCode);
        return _registered;
    }

    private void Unregister()
    {
        if (_registered)
        {
            Win32.UnregisterHotKey(_messageWindow, HotkeyId);
            _registered = false;
        }
    }

    private nint HandleMessage(
        nint hWnd, uint uMsg, nint wParam, nint lParam, nuint uIdSubclass, nuint dwRefData)
    {
        if (uMsg == Win32.WM_HOTKEY && wParam == HotkeyId)
        {
            _invoked();
            return 0;
        }

        return Win32.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    public void Dispose()
    {
        Unregister();
        Win32.DestroyWindow(_messageWindow);
    }
}
