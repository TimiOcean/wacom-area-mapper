namespace Ausschnitt.Core;

public enum PenOrientation
{
    Normal = 0,
    Rotate180 = 1,
    FlipX = 2,
    FlipY = 3,
}

public static class PenOrientationInfo
{
    public static PenOrientation Parse(string? value) => value switch
    {
        nameof(PenOrientation.Rotate180) => PenOrientation.Rotate180,
        nameof(PenOrientation.FlipX) => PenOrientation.FlipX,
        nameof(PenOrientation.FlipY) => PenOrientation.FlipY,
        _ => PenOrientation.Normal,
    };

    public static string Label(PenOrientation orientation) => orientation switch
    {
        PenOrientation.Rotate180 => "180°",
        PenOrientation.FlipX => "Horizontal spiegeln",
        PenOrientation.FlipY => "Vertikal spiegeln",
        _ => "Normal",
    };
}
