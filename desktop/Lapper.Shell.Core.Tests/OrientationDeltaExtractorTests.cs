using Xunit;

namespace Lapper.Shell.Core.Tests;

public class OrientationDeltaExtractorTests
{
    private const string FullJson =
        "{\"contentType\":\"renewal_notice\",\"orientation\":\"Your fee rises from \\u00a3400 to \\u00a3472 \\\"soon\\\".\",\"summary\":\"...\"}";

    private const string ExpectedText = "Your fee rises from £400 to £472 \"soon\".";

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(1000)]
    public void ExtractsOrientationAcrossAnyChunkBoundary(int chunkSize)
    {
        var extractor = new OrientationDeltaExtractor();
        for (var i = 0; i < FullJson.Length; i += chunkSize)
        {
            extractor.Feed(FullJson.Substring(i, Math.Min(chunkSize, FullJson.Length - i)));
        }
        Assert.Equal(ExpectedText, extractor.Text);
        Assert.True(extractor.IsComplete);
    }

    [Fact]
    public void IgnoresOrientationLikeContentInsideOtherStrings()
    {
        var extractor = new OrientationDeltaExtractor();
        extractor.Feed("{\"contentType\":\"x\\\"orientation\\\":\\\"fake\",\"orientation\":\"real\"}");
        Assert.Equal("real", extractor.Text);
    }

    [Fact]
    public void SurfacesTextIncrementally()
    {
        var extractor = new OrientationDeltaExtractor();
        extractor.Feed("{\"orientation\":\"Hel");
        Assert.Equal("Hel", extractor.Text);
        Assert.False(extractor.IsComplete);
        extractor.Feed("lo\"");
        Assert.Equal("Hello", extractor.Text);
        Assert.True(extractor.IsComplete);
    }

    [Fact]
    public void HandlesNewlineAndTabEscapes()
    {
        var extractor = new OrientationDeltaExtractor();
        extractor.Feed("{\"orientation\":\"a\\nb\\tc\"}");
        Assert.Equal("a\nb\tc", extractor.Text);
    }

    [Fact]
    public void EmptyBeforeKeyAppears()
    {
        var extractor = new OrientationDeltaExtractor();
        extractor.Feed("{\"contentType\":\"invoice\",");
        Assert.Equal(string.Empty, extractor.Text);
        Assert.False(extractor.IsComplete);
    }
}
