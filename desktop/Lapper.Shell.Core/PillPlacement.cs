namespace Lapper.Shell.Core;

/// <summary>
/// Placement rules for the floating pill. Pure math so the behavior that
/// backs the "pill position survives restart" acceptance criterion is
/// unit-testable without a display.
/// </summary>
public static class PillPlacement
{
    /// <summary>Margin kept between the default pill position and the work-area edge.</summary>
    public const int DefaultEdgeMargin = 24;

    /// <summary>Clamps a desired pill position so the pill stays fully inside the work area.</summary>
    public static PixelPoint Clamp(PixelPoint desired, PixelSize pillSize, PixelRect workArea)
    {
        var maxX = workArea.Right - pillSize.Width;
        var maxY = workArea.Bottom - pillSize.Height;
        return new PixelPoint(
            Math.Clamp(desired.X, workArea.X, Math.Max(workArea.X, maxX)),
            Math.Clamp(desired.Y, workArea.Y, Math.Max(workArea.Y, maxY)));
    }

    /// <summary>Default position: bottom-right of the work area with a margin.</summary>
    public static PixelPoint DefaultPosition(PixelSize pillSize, PixelRect workArea) =>
        new(
            Math.Max(workArea.X, workArea.Right - pillSize.Width - DefaultEdgeMargin),
            Math.Max(workArea.Y, workArea.Bottom - pillSize.Height - DefaultEdgeMargin));

    /// <summary>
    /// Restores a saved position: reuse it (clamped) when its top-left still
    /// falls on a known work area — e.g. after a monitor change — otherwise
    /// fall back to the default position on the primary work area.
    /// </summary>
    public static PixelPoint Restore(
        PixelPoint? saved,
        PixelSize pillSize,
        PixelRect primaryWorkArea,
        IReadOnlyList<PixelRect> allWorkAreas)
    {
        if (saved is { } position)
        {
            foreach (var workArea in allWorkAreas)
            {
                if (workArea.Contains(position))
                {
                    return Clamp(position, pillSize, workArea);
                }
            }
        }

        return DefaultPosition(pillSize, primaryWorkArea);
    }
}
