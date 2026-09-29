namespace Ausschnitt.Core;

/// <summary>
/// A rectangle in physical screen pixels. Origin is the top-left of the virtual desktop,
/// which can be negative when a monitor sits left of or above the primary display.
/// </summary>
public readonly record struct ScreenRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public bool IsUsable => Width >= 8 && Height >= 8;

    public bool Contains(int x, int y) => x >= X && y >= Y && x < Right && y < Bottom;

    public static ScreenRect FromPoints(int x0, int y0, int x1, int y1)
    {
        int x = Math.Min(x0, x1);
        int y = Math.Min(y0, y1);
        return new ScreenRect(x, y, Math.Abs(x1 - x0), Math.Abs(y1 - y0));
    }

    /// <summary>
    /// Locks the dragged rectangle to <paramref name="aspect"/> (width / height)
    /// while keeping the press point fixed.
    /// </summary>
    public static ScreenRect ConstrainAspect(int anchorX, int anchorY, int currentX, int currentY, double aspect)
    {
        if (aspect <= 0)
            aspect = 1;

        int rawW = Math.Abs(currentX - anchorX);
        int rawH = Math.Abs(currentY - anchorY);
        if (rawH < 1)
            rawH = 1;
        if (rawW < 1)
            rawW = 1;

        int width;
        int height;
        if (rawW / (double)rawH > aspect)
        {
            height = rawH;
            width = Math.Max(1, (int)Math.Round(height * aspect));
        }
        else
        {
            width = rawW;
            height = Math.Max(1, (int)Math.Round(width / aspect));
        }

        int x = currentX >= anchorX ? anchorX : anchorX - width;
        int y = currentY >= anchorY ? anchorY : anchorY - height;
        return new ScreenRect(x, y, width, height);
    }

    public static ScreenRect Union(IReadOnlyList<ScreenRect> rects)
    {
        if (rects.Count == 0)
            return new ScreenRect(0, 0, 0, 0);

        int left = rects.Min(r => r.X);
        int top = rects.Min(r => r.Y);
        int right = rects.Max(r => r.Right);
        int bottom = rects.Max(r => r.Bottom);
        return new ScreenRect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    public override string ToString() => $"{Width} × {Height} bei ({X}, {Y})";
}
