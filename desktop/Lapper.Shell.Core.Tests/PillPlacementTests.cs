using Xunit;

namespace Lapper.Shell.Core.Tests;

public class PillPlacementTests
{
    private static readonly PixelSize PillSize = new(180, 48);
    private static readonly PixelRect Primary = new(0, 0, 1920, 1040);

    [Fact]
    public void ClampKeepsPillInsideWorkArea()
    {
        var clamped = PillPlacement.Clamp(new PixelPoint(5000, -200), PillSize, Primary);
        Assert.Equal(new PixelPoint(1920 - 180, 0), clamped);
    }

    [Fact]
    public void ClampLeavesValidPositionUntouched()
    {
        var position = new PixelPoint(600, 500);
        Assert.Equal(position, PillPlacement.Clamp(position, PillSize, Primary));
    }

    [Fact]
    public void DefaultPositionIsBottomRightWithMargin()
    {
        var position = PillPlacement.DefaultPosition(PillSize, Primary);
        Assert.Equal(new PixelPoint(1920 - 180 - 24, 1040 - 48 - 24), position);
    }

    [Fact]
    public void RestoreReusesSavedPositionOnAKnownMonitor()
    {
        var secondary = new PixelRect(1920, 0, 1920, 1080);
        var saved = new PixelPoint(2500, 300);
        var restored = PillPlacement.Restore(saved, PillSize, Primary, [Primary, secondary]);
        Assert.Equal(saved, restored);
    }

    [Fact]
    public void RestoreFallsBackToDefaultWhenSavedPositionIsOffAllMonitors()
    {
        var saved = new PixelPoint(-4000, -4000); // e.g. unplugged monitor
        var restored = PillPlacement.Restore(saved, PillSize, Primary, [Primary]);
        Assert.Equal(PillPlacement.DefaultPosition(PillSize, Primary), restored);
    }

    [Fact]
    public void RestoreFallsBackToDefaultWhenNothingSaved()
    {
        var restored = PillPlacement.Restore(null, PillSize, Primary, [Primary]);
        Assert.Equal(PillPlacement.DefaultPosition(PillSize, Primary), restored);
    }
}
