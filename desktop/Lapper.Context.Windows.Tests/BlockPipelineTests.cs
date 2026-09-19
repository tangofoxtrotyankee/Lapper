using Lapper.Context.Windows;
using Lapper.Context.Windows.Blocks;
using Xunit;

namespace Lapper.Context.Windows.Tests;

public class AppCategoryMapTests
{
    [Theory]
    [InlineData("msedge.exe", "browser")]
    [InlineData("CHROME.EXE", "browser")]
    [InlineData("OUTLOOK.EXE", "email")]
    [InlineData("olk.exe", "email")]
    [InlineData("winword.exe", "document")]
    [InlineData("notepad.exe", "document")]
    [InlineData("acrord32.exe", "pdf")]
    [InlineData("code.exe", "code")]
    [InlineData("windowsterminal.exe", "terminal")]
    [InlineData("explorer.exe", "files")]
    [InlineData("someunknown.exe", "other")]
    public void MapsProcessesToWireCategories(string process, string expected)
    {
        Assert.Equal(expected, AppCategoryMap.Map(process));
    }
}

public class BlockNormalizerTests
{
    private static RawBlock Block(string role, string text, int order) =>
        new(role, text, HasFocus: false, order);

    [Fact]
    public void TrimsCollapsesAndDropsNoise()
    {
        var result = BlockNormalizer.Normalize(
        [
            Block("text", "  hello   world  ", 0),
            Block("text", "x", 1),          // too short
            Block("text", "___", 2),        // pure chrome
        ]);
        Assert.Single(result);
        Assert.Equal("hello world", result[0].Text);
    }

    [Fact]
    public void DeduplicatesByRoleAndPrefix()
    {
        var result = BlockNormalizer.Normalize(
        [
            Block("text", "Same content", 0),
            Block("text", "Same content", 1),
            Block("heading", "Same content", 2), // different role survives
        ]);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void DropsBlocksContainedInDocumentText()
    {
        var result = BlockNormalizer.Normalize(
        [
            Block("document", "The quarterly report shows revenue rising to £52,000 in Q3.", 0),
            Block("text", "revenue rising to £52,000", 1), // contained → dropped
            Block("text", "Completely different sentence.", 2),
        ]);
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, b => b.Text == "revenue rising to £52,000");
    }

    [Fact]
    public void CapsLongNavigationRuns()
    {
        var blocks = Enumerable.Range(0, 20)
            .Select(i => Block("link", $"Navigation item number {i}", i))
            .ToList();
        var result = BlockNormalizer.Normalize(blocks);
        Assert.Equal(6, result.Count); // 5 kept + 1 "(+N more)" marker
        Assert.Contains(result, b => b.Text.Contains("+15 more similar items"));
    }
}

public class BlockRankerTests
{
    private static RawBlock Block(string role, string text, int order, bool focus = false) =>
        new(role, text, focus, order);

    [Fact]
    public void AssignsDeterministicStableIds()
    {
        var blocks = new List<RawBlock>
        {
            Block("link", "A link somewhere in navigation area", 0),
            Block("document", "The main document body text with plenty of substance here.", 1),
            Block("heading", "Quarterly results", 2),
        };
        var first = BlockRanker.Rank(blocks);
        var second = BlockRanker.Rank(blocks);
        Assert.Equal(first.Select(b => (b.Id, b.Text)), second.Select(b => (b.Id, b.Text)));
        Assert.Equal("b1", first[0].Id);
        Assert.Equal("document", first[0].Role);
    }

    [Fact]
    public void FocusBoostsRanking()
    {
        var blocks = new List<RawBlock>
        {
            Block("text", "Unfocused text block with reasonable length for scoring.", 0),
            Block("text", "Focused text block with reasonable length for scoring!", 1, focus: true),
        };
        var ranked = BlockRanker.Rank(blocks);
        Assert.Equal("Focused text block with reasonable length for scoring!", ranked[0].Text);
    }

    [Fact]
    public void EnforcesBlockAndCharacterCaps()
    {
        var blocks = Enumerable.Range(0, 100)
            .Select(i => Block("text", new string('x', 500), i))
            .ToList();
        var ranked = BlockRanker.Rank(blocks, maxBlocks: 10, maxTotalChars: 2600);
        Assert.True(ranked.Count <= 10);
        Assert.True(ranked.Sum(b => b.Text.Length) <= 2600);
    }

    [Fact]
    public void SplitsOversizeBlocksToTheContractBound()
    {
        // An 8000-char document block (extractor cap) must never produce a
        // block above the contract's 4000-char maxLength — the backend
        // schema would reject the whole request.
        var text = string.Join(' ', Enumerable.Repeat("word", 1600)); // 7999 chars
        var ranked = BlockRanker.Rank([Block("document", text, 0)]);

        Assert.True(ranked.Count >= 2);
        Assert.All(ranked, b => Assert.True(b.Text.Length <= BlockRanker.MaxBlockChars));
        Assert.All(ranked, b => Assert.Matches("^b[0-9]{1,4}$", b.Id));
        // Content is preserved across the split, not truncated away.
        Assert.True(ranked.Sum(b => b.Text.Length) > 7000);
    }
}

public class OcrLineGrouperTests
{
    [Fact]
    public void GroupsAdjacentLinesIntoParagraphs()
    {
        var lines = new List<Capture.OcrLineInfo>
        {
            new("Dear customer,", 10, 10, 200, 12),
            new("your invoice is attached.", 10, 24, 210, 12),   // small gap → same paragraph
            new("Kind regards", 10, 80, 150, 12),                // large gap → new paragraph
        };
        var blocks = Capture.OcrLineGrouper.Group(lines);
        Assert.Equal(2, blocks.Count);
        Assert.Equal("Dear customer, your invoice is attached.", blocks[0].Text);
        Assert.Equal("ocr_paragraph", blocks[0].Role);
    }

    [Fact]
    public void SeparatesColumnsWithoutHorizontalOverlap()
    {
        var lines = new List<Capture.OcrLineInfo>
        {
            new("Left column", 10, 10, 100, 12),
            new("Right column", 400, 22, 100, 12), // vertical neighbour but no overlap
        };
        var blocks = Capture.OcrLineGrouper.Group(lines);
        Assert.Equal(2, blocks.Count);
    }

    [Fact]
    public void EmptyInputYieldsNoBlocks()
    {
        Assert.Empty(Capture.OcrLineGrouper.Group([]));
    }
}
