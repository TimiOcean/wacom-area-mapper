namespace Ausschnitt.Core;

/// <summary>
/// Digitizer limits for a pen tablet. Counts match the HID report, not millimeters.
/// </summary>
public readonly record struct TabletSpec(
    string Name,
    int VendorId,
    int ProductId,
    int MaxX,
    int MaxY,
    int MaxPressure,
    double WidthMm,
    double HeightMm)
{
    public double Aspect => MaxY == 0 ? 1 : MaxX / (double)MaxY;

    public static TabletSpec OneSmall { get; } = new(
        "Wacom One Small (CTL-472)",
        0x056A,
        0x037A,
        MaxX: 15200,
        MaxY: 9500,
        MaxPressure: 2047,
        WidthMm: 152,
        HeightMm: 95);

    public static TabletSpec OneMedium { get; } = new(
        "Wacom One Medium (CTL-672)",
        0x056A,
        0x037B,
        MaxX: 21600,
        MaxY: 13500,
        MaxPressure: 2047,
        WidthMm: 216,
        HeightMm: 135);

    public static IReadOnlyList<TabletSpec> Known { get; } = [OneSmall, OneMedium];

    public static TabletSpec? Find(int vendorId, int productId)
    {
        foreach (var tablet in Known)
        {
            if (tablet.VendorId == vendorId && tablet.ProductId == productId)
                return tablet;
        }

        return null;
    }
}
