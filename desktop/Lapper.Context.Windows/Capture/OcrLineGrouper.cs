using Lapper.Context.Windows.Blocks;

namespace Lapper.Context.Windows.Capture;

/// <summary>Geometry of one OCR line — plain data so grouping is unit-testable.</summary>
public sealed record OcrLineInfo(string Text, double X, double Y, double Width, double Height);

/// <summary>
/// Merges OCR lines into paragraph blocks: consecutive lines join when the
/// vertical gap is small relative to line height and they overlap
/// horizontally. Output ordered top-left reading order.
/// </summary>
public static class OcrLineGrouper
{
    private const double MaxGapFactor = 0.6;
    private const double MinHorizontalOverlap = 0.3;

    public static List<RawBlock> Group(IReadOnlyList<OcrLineInfo> lines, int treeOrderBase = 10_000)
    {
        if (lines.Count == 0)
        {
            return [];
        }

        var ordered = lines.OrderBy(l => l.Y).ThenBy(l => l.X).ToList();
        var medianHeight = Median(ordered.Select(l => l.Height).Where(h => h > 0).ToList());

        var paragraphs = new List<List<OcrLineInfo>> { new() { ordered[0] } };
        for (var i = 1; i < ordered.Count; i++)
        {
            var previous = paragraphs[^1][^1];
            var current = ordered[i];
            var gap = current.Y - (previous.Y + previous.Height);
            if (gap <= MaxGapFactor * medianHeight && HorizontalOverlap(previous, current) >= MinHorizontalOverlap)
            {
                paragraphs[^1].Add(current);
            }
            else
            {
                paragraphs.Add([current]);
            }
        }

        return
        [
            .. paragraphs.Select((paragraph, index) => new RawBlock(
                ContextBlockRoles.OcrParagraph,
                string.Join(' ', paragraph.Select(l => l.Text)),
                HasFocus: false,
                treeOrderBase + index)),
        ];
    }

    private static double HorizontalOverlap(OcrLineInfo a, OcrLineInfo b)
    {
        var overlap = Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X);
        var narrower = Math.Min(a.Width, b.Width);
        return narrower <= 0 ? 0 : Math.Max(0, overlap) / narrower;
    }

    private static double Median(List<double> values)
    {
        if (values.Count == 0)
        {
            return 1;
        }
        values.Sort();
        return values[values.Count / 2];
    }
}
