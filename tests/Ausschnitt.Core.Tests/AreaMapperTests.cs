using Ausschnitt.Core;

namespace Ausschnitt.Core.Tests;

public class AreaMapperTests
{
    private static readonly TabletSpec Tablet = TabletSpec.OneSmall;

    [Fact]
    public void Corners_land_on_the_rectangle()
    {
        var area = new ScreenRect(100, 50, 800, 400);

        var topLeft = AreaMapper.Map(0, 0, Tablet, area, false, PenOrientation.Normal);
        var bottomRight = AreaMapper.Map(Tablet.MaxX, Tablet.MaxY, Tablet, area, false, PenOrientation.Normal);

        Assert.Equal(100, topLeft.X);
        Assert.Equal(50, topLeft.Y);
        Assert.Equal(900, bottomRight.X);
        Assert.Equal(450, bottomRight.Y);
    }

    [Fact]
    public void Center_of_the_tablet_is_the_center_of_the_area()
    {
        var area = new ScreenRect(0, 0, 200, 100);
        var point = AreaMapper.Map(Tablet.MaxX / 2d, Tablet.MaxY / 2d, Tablet, area, false, PenOrientation.Normal);
        Assert.Equal(100, point.X, 0);
        Assert.Equal(50, point.Y, 0);
    }

    [Fact]
    public void Pixel_mapping_stays_inside_the_area()
    {
        var area = new ScreenRect(10, 20, 30, 40);
        var (x, y) = AreaMapper.MapPixel(Tablet.MaxX, Tablet.MaxY, Tablet, area, false, PenOrientation.Normal);
        Assert.InRange(x, area.X, area.Right - 1);
        Assert.InRange(y, area.Y, area.Bottom - 1);
    }

    [Fact]
    public void Proportions_crop_the_wider_tablet_against_a_square()
    {
        var squareTablet = Tablet with { MaxX = 160, MaxY = 100 };
        var area = new ScreenRect(0, 0, 100, 100);

        var leftEdge = AreaMapper.Map(0, 50, squareTablet, area, true, PenOrientation.Normal);
        var usedLeft = AreaMapper.Map(30, 50, squareTablet, area, true, PenOrientation.Normal);
        var usedRight = AreaMapper.Map(130, 50, squareTablet, area, true, PenOrientation.Normal);

        Assert.True(leftEdge.Clamped);
        Assert.Equal(0, usedLeft.X, 1);
        Assert.Equal(100, usedRight.X, 1);
    }

    [Fact]
    public void Proportions_crop_tablet_height_when_the_screen_is_wider()
    {
        var squareTablet = Tablet with { MaxX = 160, MaxY = 100 };
        var area = new ScreenRect(0, 0, 200, 100);

        var top = AreaMapper.Map(80, 10, squareTablet, area, true, PenOrientation.Normal);
        var bottom = AreaMapper.Map(80, 90, squareTablet, area, true, PenOrientation.Normal);

        Assert.Equal(0, top.Y, 1);
        Assert.Equal(100, bottom.Y, 1);
    }

    [Fact]
    public void FlipY_puts_the_top_of_the_tablet_at_the_bottom_of_the_area()
    {
        var area = new ScreenRect(0, 0, 100, 80);
        var point = AreaMapper.Map(0, 0, Tablet, area, false, PenOrientation.FlipY);
        Assert.Equal(0, point.X, 0);
        Assert.Equal(80, point.Y, 0);
    }

    [Fact]
    public void One_Small_aspect_is_16_to_10()
    {
        Assert.Equal(1.6, TabletSpec.OneSmall.Aspect, 5);
        Assert.Equal(1.6, TabletSpec.OneMedium.Aspect, 5);
        Assert.Equal(0x037A, TabletSpec.OneSmall.ProductId);
        Assert.Equal(0x037B, TabletSpec.OneMedium.ProductId);
    }
}
