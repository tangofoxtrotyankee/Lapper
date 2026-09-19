namespace Lapper.Privacy.Capture;

/// <summary>
/// Limits for in-memory window captures (threat model: image dimension and
/// pixel limits). Screenshots exist only as OCR input and are never written
/// to disk or transmitted.
/// </summary>
public static class ScreenshotPolicy
{
    public const int MaxPixels = 8_000_000;

    /// <summary>Scales dimensions down proportionally to fit maxDim on the longest side.</summary>
    public static (int Width, int Height) ClampDimensions(int width, int height, int maxDim)
    {
        if (width <= 0 || height <= 0)
        {
            return (0, 0);
        }
        var longest = Math.Max(width, height);
        if (longest <= maxDim)
        {
            return (width, height);
        }
        var scale = (double)maxDim / longest;
        return (Math.Max(1, (int)(width * scale)), Math.Max(1, (int)(height * scale)));
    }
}
