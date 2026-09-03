using Xunit;

namespace Lapper.Shell.Core.Tests;

public class ShortcutGestureTests
{
    [Theory]
    [InlineData("Ctrl+Alt+L", ShortcutModifiers.Control | ShortcutModifiers.Alt, "L", 0x4C)]
    [InlineData("ctrl+shift+space", ShortcutModifiers.Control | ShortcutModifiers.Shift, "SPACE", 0x20)]
    [InlineData("Win+F12", ShortcutModifiers.Win, "F12", 0x7B)]
    [InlineData("Control + Alt + 7", ShortcutModifiers.Control | ShortcutModifiers.Alt, "7", 0x37)]
    public void ParsesValidGestures(string text, ShortcutModifiers modifiers, string key, int vk)
    {
        Assert.True(ShortcutGesture.TryParse(text, out var gesture));
        Assert.NotNull(gesture);
        Assert.Equal(modifiers, gesture.Modifiers);
        Assert.Equal(key, gesture.Key);
        Assert.Equal(vk, gesture.VirtualKeyCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("L")] // no modifier: would swallow plain typing
    [InlineData("Ctrl+Alt")] // no key
    [InlineData("Ctrl+A+B")] // two keys
    [InlineData("Ctrl+Escape+")] // trailing separator
    [InlineData("Ctrl+F25")] // unsupported key
    [InlineData("Ctrl+é")] // unsupported key
    public void RejectsInvalidGestures(string? text)
    {
        Assert.False(ShortcutGesture.TryParse(text, out var gesture));
        Assert.Null(gesture);
    }

    [Fact]
    public void FormatAndParseRoundTrip()
    {
        var original = new ShortcutGesture(
            ShortcutModifiers.Control | ShortcutModifiers.Shift | ShortcutModifiers.Win, "F5");
        Assert.Equal("Ctrl+Shift+Win+F5", original.Format());
        Assert.True(ShortcutGesture.TryParse(original.Format(), out var reparsed));
        Assert.Equal(original, reparsed);
    }

    [Fact]
    public void DefaultGestureIsValid()
    {
        Assert.True(ShortcutGesture.TryParse(ShortcutGesture.Default.Format(), out var gesture));
        Assert.Equal(ShortcutGesture.Default, gesture);
        Assert.NotEqual(0, ShortcutGesture.Default.VirtualKeyCode);
    }
}
