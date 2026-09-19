using Lapper.Privacy.Exclusions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.System.Threading;

namespace Lapper.Context.Windows;

public sealed record ForegroundWindowInfo(
    nint Hwnd,
    WindowIdentity Identity);

/// <summary>
/// Identifies the current foreground window and its owning process.
/// A process that cannot be identified yields ProcessImagePath = null, which
/// the exclusion policy treats as blocked (fail closed).
/// </summary>
public static class ForegroundWindowProbe
{
    public static ForegroundWindowInfo? Probe()
    {
        var hwnd = PInvoke.GetForegroundWindow();
        if (hwnd.IsNull || !PInvoke.IsWindow(hwnd) || PInvoke.IsIconic(hwnd) || IsCloaked(hwnd))
        {
            return null;
        }

        uint pid;
        unsafe
        {
            PInvoke.GetWindowThreadProcessId(hwnd, &pid);
        }
        if (pid == 0)
        {
            return null;
        }

        var imagePath = QueryImagePath(pid);
        var processName = imagePath is null ? string.Empty : Path.GetFileName(imagePath);

        var identity = new WindowIdentity(
            imagePath,
            processName,
            (int)pid,
            ReadWindowText(hwnd),
            ReadClassName(hwnd));
        return new ForegroundWindowInfo(hwnd, identity);
    }

    private static bool IsCloaked(HWND hwnd)
    {
        Span<byte> value = stackalloc byte[4];
        var hr = PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, value);
        return hr.Succeeded && BitConverter.ToInt32(value) != 0;
    }

    private static string? QueryImagePath(uint pid)
    {
        using var process = PInvoke.OpenProcess_SafeHandle(
            PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION,
            false,
            pid);
        if (process.IsInvalid)
        {
            return null;
        }

        Span<char> buffer = stackalloc char[1024];
        var size = (uint)buffer.Length;
        if (!PInvoke.QueryFullProcessImageName(process, PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32, buffer, ref size))
        {
            return null;
        }
        return new string(buffer[..(int)size]);
    }

    private static string ReadWindowText(HWND hwnd)
    {
        Span<char> buffer = stackalloc char[512];
        var length = PInvoke.GetWindowText(hwnd, buffer);
        return length > 0 ? new string(buffer[..length]) : string.Empty;
    }

    private static string ReadClassName(HWND hwnd)
    {
        Span<char> buffer = stackalloc char[256];
        var length = PInvoke.GetClassName(hwnd, buffer);
        return length > 0 ? new string(buffer[..length]) : string.Empty;
    }
}
