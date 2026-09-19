using System.Security.Cryptography;
using Lapper.Privacy.Capture;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.Storage.Xps;

namespace Lapper.Context.Windows.Capture;

/// <summary>
/// A captured window frame that provably leaves memory: Dispose zeroes the
/// pixel buffer. Never written to disk, never transmitted — OCR input only.
/// </summary>
public sealed class CapturedFrame : IDisposable
{
    public required byte[] Bgra { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }

    public void Dispose() => CryptographicOperations.ZeroMemory(Bgra);

    public override string ToString() => $"CapturedFrame({Width}x{Height})";
}

/// <summary>
/// GDI capture of ONLY the target window (PrintWindow with full content
/// rendering; BitBlt of the window rect as fallback, which can include
/// overlapping windows — acceptable, documented MVP caveat). No WinRT
/// capture APIs (no picker, no yellow border), no files, no encoders.
/// </summary>
public static class WindowCapture
{
    // Absent from win32metadata's PRINT_WINDOW_FLAGS; renders DirectComposition content.
    private const uint PW_RENDERFULLCONTENT = 0x00000002;

    public static unsafe CapturedFrame? Capture(nint hwndRaw, int maxDimension)
    {
        var hwnd = new HWND((void*)hwndRaw);
        if (!PInvoke.GetWindowRect(hwnd, out var rect))
        {
            return null;
        }

        var width = rect.right - rect.left;
        var height = rect.bottom - rect.top;
        if (width <= 0 || height <= 0 || (long)width * height > ScreenshotPolicy.MaxPixels)
        {
            return null;
        }

        var screenDc = PInvoke.GetDC(HWND.Null);
        if (screenDc.IsNull)
        {
            return null;
        }
        var memDc = PInvoke.CreateCompatibleDC(screenDc);
        HBITMAP dib = default;
        HGDIOBJ previous = default;
        try
        {
            void* bits;
            var info = new BITMAPINFO();
            info.bmiHeader.biSize = (uint)sizeof(BITMAPINFOHEADER);
            info.bmiHeader.biWidth = width;
            info.bmiHeader.biHeight = -height; // top-down
            info.bmiHeader.biPlanes = 1;
            info.bmiHeader.biBitCount = 32;
            info.bmiHeader.biCompression = 0; // BI_RGB

            dib = PInvoke.CreateDIBSection(screenDc, &info, DIB_USAGE.DIB_RGB_COLORS, &bits, HANDLE.Null, 0);
            if (dib.IsNull || bits is null)
            {
                return null;
            }
            previous = PInvoke.SelectObject(memDc, dib);

            if (!PInvoke.PrintWindow(hwnd, memDc, (PRINT_WINDOW_FLAGS)PW_RENDERFULLCONTENT))
            {
                PInvoke.BitBlt(memDc, 0, 0, width, height, screenDc, rect.left, rect.top, ROP_CODE.SRCCOPY);
            }
            PInvoke.GdiFlush();

            var (targetWidth, targetHeight) = ScreenshotPolicy.ClampDimensions(width, height, maxDimension);
            if (targetWidth == width && targetHeight == height)
            {
                var pixels = new byte[(long)width * height * 4];
                new ReadOnlySpan<byte>(bits, pixels.Length).CopyTo(pixels);
                return new CapturedFrame { Bgra = pixels, Width = width, Height = height };
            }

            return Downscale(screenDc, memDc, width, height, targetWidth, targetHeight);
        }
        finally
        {
            if (!previous.IsNull)
            {
                PInvoke.SelectObject(memDc, previous);
            }
            if (!dib.IsNull)
            {
                PInvoke.DeleteObject(dib);
            }
            PInvoke.DeleteDC(memDc);
            PInvoke.ReleaseDC(HWND.Null, screenDc);
        }
    }

    private static unsafe CapturedFrame? Downscale(
        HDC screenDc,
        HDC sourceDc,
        int sourceWidth,
        int sourceHeight,
        int targetWidth,
        int targetHeight)
    {
        var scaledDc = PInvoke.CreateCompatibleDC(screenDc);
        HBITMAP scaledDib = default;
        HGDIOBJ previous = default;
        try
        {
            void* scaledBits;
            var info = new BITMAPINFO();
            info.bmiHeader.biSize = (uint)sizeof(BITMAPINFOHEADER);
            info.bmiHeader.biWidth = targetWidth;
            info.bmiHeader.biHeight = -targetHeight;
            info.bmiHeader.biPlanes = 1;
            info.bmiHeader.biBitCount = 32;
            info.bmiHeader.biCompression = 0;

            scaledDib = PInvoke.CreateDIBSection(
                screenDc, &info, DIB_USAGE.DIB_RGB_COLORS, &scaledBits, HANDLE.Null, 0);
            if (scaledDib.IsNull || scaledBits is null)
            {
                return null;
            }
            previous = PInvoke.SelectObject(scaledDc, scaledDib);
            PInvoke.SetStretchBltMode(scaledDc, STRETCH_BLT_MODE.COLORONCOLOR);
            PInvoke.StretchBlt(
                scaledDc, 0, 0, targetWidth, targetHeight,
                sourceDc, 0, 0, sourceWidth, sourceHeight, ROP_CODE.SRCCOPY);
            PInvoke.GdiFlush();

            var pixels = new byte[(long)targetWidth * targetHeight * 4];
            new ReadOnlySpan<byte>(scaledBits, pixels.Length).CopyTo(pixels);
            return new CapturedFrame { Bgra = pixels, Width = targetWidth, Height = targetHeight };
        }
        finally
        {
            if (!previous.IsNull)
            {
                PInvoke.SelectObject(scaledDc, previous);
            }
            if (!scaledDib.IsNull)
            {
                PInvoke.DeleteObject(scaledDib);
            }
            PInvoke.DeleteDC(scaledDc);
        }
    }
}
