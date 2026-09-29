using Ausschnitt.Core;

namespace Ausschnitt.Core.Tests;

public class IntuosReportParserTests
{
    [Fact]
    public void Parses_a_ten_byte_proximity_report()
    {
        var report = Packet(flags: 0x40 | 0x02, x: 7600, y: 4750, pressure: 1024);
        Assert.True(IntuosReportParser.TryParse(report, stripLeadingByte: false, out var sample));
        Assert.True(sample.InRange);
        Assert.Equal(7600, sample.X);
        Assert.Equal(4750, sample.Y);
        Assert.Equal(1024, sample.Pressure);
        Assert.True(sample.Button1);
        Assert.False(sample.Button2);
    }

    [Fact]
    public void Proximity_bit_clear_means_the_pen_left()
    {
        var report = Packet(flags: 0, x: 0, y: 0, pressure: 0);
        Assert.True(IntuosReportParser.TryParse(report, false, out var sample));
        Assert.False(sample.InRange);
    }

    [Fact]
    public void Strips_the_windows_report_id_when_asked()
    {
        var body = Packet(flags: 0x40 | 0x04, x: 100, y: 200, pressure: 10);
        var wrapped = new byte[body.Length + 1];
        wrapped[0] = 0x02;
        body.CopyTo(wrapped.AsSpan(1));

        Assert.True(IntuosReportParser.TryParse(wrapped, stripLeadingByte: true, out var sample));
        Assert.True(sample.InRange);
        Assert.Equal(100, sample.X);
        Assert.Equal(200, sample.Y);
        Assert.True(sample.Button2);
        Assert.False(sample.Button1);
    }

    [Fact]
    public void Rejects_a_short_or_foreign_packet()
    {
        Assert.False(IntuosReportParser.TryParse(new byte[] { 0x10, 0x40, 0, 0, 0, 0, 0, 0, 0 }, false, out _));
        Assert.False(IntuosReportParser.TryParse(new byte[] { 0x02, 0x40 }, false, out _));
    }

    [Fact]
    public void Scales_full_pressure_to_the_windows_limit()
    {
        Assert.Equal(0, PressureScale.ToWindows(0, 2047));
        Assert.Equal(1024, PressureScale.ToWindows(2047, 2047));
        Assert.Equal(512, PressureScale.ToWindows(1024, 2047));
    }

    private static byte[] Packet(byte flags, int x, int y, int pressure)
    {
        var report = new byte[10];
        report[0] = 0x02;
        report[1] = flags;
        report[2] = (byte)(x & 0xFF);
        report[3] = (byte)((x >> 8) & 0xFF);
        report[4] = (byte)(y & 0xFF);
        report[5] = (byte)((y >> 8) & 0xFF);
        report[6] = (byte)(pressure & 0xFF);
        report[7] = (byte)((pressure >> 8) & 0xFF);
        return report;
    }
}
