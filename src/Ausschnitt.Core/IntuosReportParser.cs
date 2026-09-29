namespace Ausschnitt.Core;

/// <summary>
/// Parses the Intuos-style HID reports used by the Wacom One CTL-472 and CTL-672.
/// Linux hidraw delivers 10 bytes starting with packet type 0x02.
/// The Windows HID stack sometimes delivers 11 bytes and the packet type sits at index 1.
/// </summary>
public static class IntuosReportParser
{
    public static bool TryParse(ReadOnlySpan<byte> report, bool stripLeadingByte, out PenSample sample)
    {
        sample = PenSample.Left;
        if (stripLeadingByte)
        {
            if (report.Length < 2)
                return false;
            report = report[1..];
        }

        if (report.Length < 9 || report[0] != 0x02)
            return false;

        byte flags = report[1];
        bool inRange = (flags & 0x40) != 0;
        if (!inRange)
        {
            sample = PenSample.Left;
            return true;
        }

        int x = report[2] | (report[3] << 8);
        int y = report[4] | (report[5] << 8);
        int pressure = report[6] | (report[7] << 8);
        sample = new PenSample(
            InRange: true,
            X: x,
            Y: y,
            Pressure: pressure,
            Button1: (flags & 0x02) != 0,
            Button2: (flags & 0x04) != 0);
        return true;
    }
}
