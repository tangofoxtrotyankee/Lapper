using System.Text.RegularExpressions;

namespace Lapper.Context.Windows.Blocks;

/// <summary>Pure block cleanup: trim, dedup, cap navigation repetition.</summary>
public static partial class BlockNormalizer
{
    private const int MaxConsecutiveNav = 8;
    private const int KeptNav = 5;

    private static readonly HashSet<string> NavRoles =
        [ContextBlockRoles.ListItem, ContextBlockRoles.Link, ContextBlockRoles.MenuItem, ContextBlockRoles.Tab];

    [GeneratedRegex(@"[ \t ]+")]
    private static partial Regex WhitespaceRuns();

    [GeneratedRegex(@"^[\W_]+$")]
    private static partial Regex PureChrome();

    public static List<RawBlock> Normalize(IEnumerable<RawBlock> raw)
    {
        var cleaned = new List<RawBlock>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var block in raw.OrderBy(b => b.TreeOrder))
        {
            var text = WhitespaceRuns().Replace(block.Text, " ").Trim();
            if (text.Length < 2 || PureChrome().IsMatch(text))
            {
                continue;
            }

            var key = block.Role + "|" + text[..Math.Min(200, text.Length)];
            if (!seen.Add(key))
            {
                continue;
            }

            cleaned.Add(block with { Text = text });
        }

        // Drop blocks fully contained in an earlier document block (UIA Name
        // vs TextPattern double-reporting in Chromium/Word).
        var documents = cleaned
            .Where(b => b.Role == ContextBlockRoles.Document)
            .Select(b => b.Text)
            .ToList();
        var contained = cleaned
            .Where(b =>
                b.Role != ContextBlockRoles.Document &&
                b.Text.Length >= 8 &&
                documents.Any(d => d.Contains(b.Text, StringComparison.Ordinal)))
            .ToHashSet();
        cleaned.RemoveAll(contained.Contains);

        return CapNavigationRuns(cleaned);
    }

    private static List<RawBlock> CapNavigationRuns(List<RawBlock> blocks)
    {
        var result = new List<RawBlock>(blocks.Count);
        for (var i = 0; i < blocks.Count;)
        {
            var current = blocks[i];
            if (!NavRoles.Contains(current.Role))
            {
                result.Add(current);
                i++;
                continue;
            }

            var runEnd = i;
            while (runEnd + 1 < blocks.Count && blocks[runEnd + 1].Role == current.Role)
            {
                runEnd++;
            }
            var runLength = runEnd - i + 1;
            if (runLength <= MaxConsecutiveNav)
            {
                result.AddRange(blocks.Skip(i).Take(runLength));
            }
            else
            {
                result.AddRange(blocks.Skip(i).Take(KeptNav));
                result.Add(new RawBlock(
                    ContextBlockRoles.Section,
                    $"(+{runLength - KeptNav} more similar items)",
                    HasFocus: false,
                    blocks[i + KeptNav].TreeOrder));
            }
            i = runEnd + 1;
        }
        return result;
    }
}
