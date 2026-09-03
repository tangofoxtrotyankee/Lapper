namespace Lapper.Shell.Core;

/// <summary>A point in physical screen pixels.</summary>
public readonly record struct PixelPoint(int X, int Y);

/// <summary>A size in physical screen pixels.</summary>
public readonly record struct PixelSize(int Width, int Height);

/// <summary>A rectangle in physical screen pixels (X/Y is the top-left corner).</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;

    public bool Contains(PixelPoint point) =>
        point.X >= X && point.X < Right && point.Y >= Y && point.Y < Bottom;
}
