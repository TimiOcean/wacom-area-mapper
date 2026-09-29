namespace Ausschnitt.Core;

/// <summary>
/// One pen report in tablet counts. Pressure is the raw device value (0…2047 on the Wacom One).
/// </summary>
public readonly record struct PenSample(
    bool InRange,
    int X,
    int Y,
    int Pressure,
    bool Button1,
    bool Button2)
{
    public static PenSample Left { get; } = new(false, 0, 0, 0, false, false);
}
