using Lapper.Privacy.Capture;
using Lapper.Privacy.Gate;
using Xunit;

namespace Lapper.Privacy.Tests;

public class GateAndPolicyTests
{
    [Fact]
    public void FocusedPasswordBlocksEverything()
    {
        Assert.Equal(
            GateDecision.Block,
            SensitiveContextGate.Evaluate(focusedElementIsPassword: true, 0, 0));
    }

    [Fact]
    public void NonPasswordContextProceeds()
    {
        Assert.Equal(
            GateDecision.Proceed,
            SensitiveContextGate.Evaluate(focusedElementIsPassword: false, 3, 2));
    }

    [Theory]
    [InlineData(1920, 1080, 4000, 1920, 1080)]
    [InlineData(8000, 4000, 4000, 4000, 2000)]
    [InlineData(4000, 8000, 4000, 2000, 4000)]
    public void ScreenshotDimensionsClampProportionally(int w, int h, int maxDim, int ew, int eh)
    {
        Assert.Equal((ew, eh), ScreenshotPolicy.ClampDimensions(w, h, maxDim));
    }
}
