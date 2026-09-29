namespace Ausschnitt.Core;

public readonly record struct MappedPoint(double X, double Y, bool Clamped);

/// <summary>
/// Maps the full tablet surface onto a screen rectangle.
/// With proportions locked, a strip of the tablet is left unused so a circle stays a circle
/// and the whole rectangle is still reachable.
/// </summary>
public static class AreaMapper
{
    public static MappedPoint Map(
        double tabletX,
        double tabletY,
        TabletSpec tablet,
        ScreenRect target,
        bool keepProportions,
        PenOrientation orientation)
    {
        double maxX = Math.Max(1, tablet.MaxX);
        double maxY = Math.Max(1, tablet.MaxY);
        (tabletX, tabletY) = Orient(tabletX, tabletY, maxX, maxY, orientation);

        double u0 = 0;
        double v0 = 0;
        double u1 = maxX;
        double v1 = maxY;

        if (keepProportions && target.Width > 0 && target.Height > 0)
        {
            double tabletAspect = maxX / maxY;
            double screenAspect = target.Width / (double)target.Height;
            if (tabletAspect > screenAspect)
            {
                double usedW = maxY * screenAspect;
                double margin = (maxX - usedW) / 2;
                u0 = margin;
                u1 = maxX - margin;
            }
            else if (tabletAspect < screenAspect)
            {
                double usedH = maxX / screenAspect;
                double margin = (maxY - usedH) / 2;
                v0 = margin;
                v1 = maxY - margin;
            }
        }

        bool clamped = false;
        double nx = u1 == u0 ? 0 : (tabletX - u0) / (u1 - u0);
        double ny = v1 == v0 ? 0 : (tabletY - v0) / (v1 - v0);
        if (nx < 0 || nx > 1 || ny < 0 || ny > 1)
            clamped = true;
        nx = Math.Clamp(nx, 0, 1);
        ny = Math.Clamp(ny, 0, 1);

        return new MappedPoint(
            target.X + nx * target.Width,
            target.Y + ny * target.Height,
            clamped);
    }

    public static (int X, int Y) MapPixel(
        double tabletX,
        double tabletY,
        TabletSpec tablet,
        ScreenRect target,
        bool keepProportions,
        PenOrientation orientation)
    {
        var point = Map(tabletX, tabletY, tablet, target, keepProportions, orientation);
        int x = (int)Math.Round(point.X);
        int y = (int)Math.Round(point.Y);
        int maxX = Math.Max(target.X, target.Right - 1);
        int maxY = Math.Max(target.Y, target.Bottom - 1);
        return (Math.Clamp(x, target.X, maxX), Math.Clamp(y, target.Y, maxY));
    }

    private static (double X, double Y) Orient(double x, double y, double maxX, double maxY, PenOrientation orientation)
    {
        return orientation switch
        {
            PenOrientation.Rotate180 => (maxX - x, maxY - y),
            PenOrientation.FlipX => (maxX - x, y),
            PenOrientation.FlipY => (x, maxY - y),
            _ => (x, y),
        };
    }
}
