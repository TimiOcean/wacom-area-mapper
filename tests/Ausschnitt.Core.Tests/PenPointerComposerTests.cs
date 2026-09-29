using Ausschnitt.Core;

namespace Ausschnitt.Core.Tests;

public class PenPointerComposerTests
{
    private readonly PenPointerComposer _composer = new();

    [Fact]
    public void Hover_then_stroke_then_leave()
    {
        var hover = _composer.Next(new PenSample(true, 10, 10, 0, false, false), 5, 6, 2047);
        var down = _composer.Next(new PenSample(true, 10, 10, 1000, false, false), 5, 6, 2047);
        var hold = _composer.Next(new PenSample(true, 12, 12, 1000, false, false), 7, 8, 2047);
        var up = _composer.Next(new PenSample(true, 12, 12, 0, false, false), 7, 8, 2047);
        var leave = _composer.Next(PenSample.Left, 7, 8, 2047);
        var again = _composer.Next(PenSample.Left, 7, 8, 2047);

        Assert.True(hover.HasValue);
        Assert.True(hover.Value.IsNew);
        Assert.False(hover.Value.InContact);

        Assert.True(down.HasValue && down.Value.TipDown);
        Assert.True(down.Value.InContact);
        Assert.True(down.Value.Pressure > 0);

        Assert.True(hold.HasValue);
        Assert.False(hold.Value.TipDown);
        Assert.False(hold.Value.TipUp);
        Assert.Equal(7, hold.Value.X);

        Assert.True(up.HasValue && up.Value.TipUp);
        Assert.False(up.Value.InContact);

        Assert.True(leave.HasValue && !leave.Value.InRange);
        Assert.Null(again);
    }

    [Fact]
    public void Barrel_and_middle_are_edges()
    {
        _composer.Next(new PenSample(true, 1, 1, 0, false, false), 0, 0, 2047);
        var barrel = _composer.Next(new PenSample(true, 1, 1, 0, true, false), 0, 0, 2047);
        var both = _composer.Next(new PenSample(true, 1, 1, 0, true, true), 0, 0, 2047);
        var released = _composer.Next(new PenSample(true, 1, 1, 0, false, false), 0, 0, 2047);

        Assert.True(barrel.HasValue && barrel.Value.BarrelDown);
        Assert.True(barrel.Value.Barrel);
        Assert.False(barrel.Value.MiddleDown);

        Assert.True(both.HasValue);
        Assert.False(both.Value.BarrelDown);
        Assert.True(both.Value.MiddleDown);

        Assert.True(released.HasValue && released.Value.BarrelUp);
        Assert.True(released.Value.MiddleUp);
    }
}
