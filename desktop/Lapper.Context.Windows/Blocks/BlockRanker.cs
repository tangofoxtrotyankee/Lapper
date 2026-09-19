using Lapper.Contracts;

namespace Lapper.Context.Windows.Blocks;

/// <summary>
/// Relevance ranking with deterministic stable IDs: descending score, ties
/// by tree order; the same snapshot always yields the same b1..bn, which
/// the model's facts[].sourceIds reference.
/// </summary>
public static class BlockRanker
{
    public const int DefaultMaxBlocks = 40;
    public const int DefaultMaxTotalChars = 12000;

    /// <summary>
    /// Contract bound (orient-request.schema.json blocks[].text maxLength):
    /// blocks are split to fit so a content-rich document never produces a
    /// request the backend schema rejects.
    /// </summary>
    public const int MaxBlockChars = 4000;

    private static readonly Dictionary<string, int> RoleWeights = new(StringComparer.Ordinal)
    {
        [ContextBlockRoles.Document] = 100,
        [ContextBlockRoles.Edit] = 70,
        [ContextBlockRoles.Heading] = 80,
        [ContextBlockRoles.Cell] = 60,
        [ContextBlockRoles.Table] = 60,
        [ContextBlockRoles.Text] = 50,
        [ContextBlockRoles.OcrParagraph] = 45,
        [ContextBlockRoles.ListItem] = 35,
        [ContextBlockRoles.Link] = 25,
        [ContextBlockRoles.Tab] = 20,
        [ContextBlockRoles.Section] = 15,
        [ContextBlockRoles.Option] = 10,
        [ContextBlockRoles.Button] = 10,
        [ContextBlockRoles.MenuItem] = 10,
    };

    public static List<ContextBlock> Rank(
        IReadOnlyList<RawBlock> blocks,
        int maxBlocks = DefaultMaxBlocks,
        int maxTotalChars = DefaultMaxTotalChars)
    {
        var focusOrder = blocks.FirstOrDefault(b => b.HasFocus)?.TreeOrder;

        var sized = blocks.SelectMany(SplitToContractSize).ToList();

        var scored = sized
            .Select(block =>
            {
                var score = RoleWeights.GetValueOrDefault(block.Role, 20);
                if (block.HasFocus)
                {
                    score += 40;
                }
                else if (focusOrder is { } order && Math.Abs(block.TreeOrder - order) <= 10)
                {
                    score += 15;
                }
                if (block.Text.Length is >= 40 and <= 1500)
                {
                    score += 10;
                }
                return (block, score);
            })
            .OrderByDescending(entry => entry.score)
            .ThenBy(entry => entry.block.TreeOrder)
            .ToList();

        var result = new List<ContextBlock>();
        var totalChars = 0;
        foreach (var (block, _) in scored)
        {
            if (result.Count >= maxBlocks || totalChars + block.Text.Length > maxTotalChars)
            {
                if (result.Count >= maxBlocks)
                {
                    break;
                }
                continue;
            }
            totalChars += block.Text.Length;
            result.Add(new ContextBlock
            {
                Id = $"b{result.Count + 1}",
                Role = block.Role,
                Text = block.Text,
            });
        }
        return result;
    }

    /// <summary>
    /// Splits an oversize block (long documents, redaction-marker growth)
    /// into contract-size chunks, breaking at whitespace where possible.
    /// </summary>
    private static IEnumerable<RawBlock> SplitToContractSize(RawBlock block)
    {
        if (block.Text.Length <= MaxBlockChars)
        {
            yield return block;
            yield break;
        }

        var remaining = block.Text;
        var first = true;
        while (remaining.Length > 0)
        {
            var take = Math.Min(MaxBlockChars, remaining.Length);
            if (take < remaining.Length)
            {
                var lastBreak = remaining.LastIndexOfAny([' ', '\n', '\t'], take - 1);
                if (lastBreak > MaxBlockChars / 2)
                {
                    take = lastBreak + 1;
                }
            }
            var chunk = remaining[..take].Trim();
            remaining = remaining[take..];
            if (chunk.Length > 0)
            {
                // Focus stays on the first chunk only, so proximity scoring
                // is not inflated by an artificially split block.
                yield return block with { Text = chunk, HasFocus = block.HasFocus && first };
            }
            first = false;
        }
    }
}
