using System.Drawing;

namespace ToolSlice;

// Physical desktop pixels; monitor origins can be negative.
public readonly record struct CaptureRegion(int X, int Y, int Width, int Height)
{
    public Rectangle Bounds => new(X, Y, Width, Height);
    public static CaptureRegion FromPoints(Point a, Point b) => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
}
