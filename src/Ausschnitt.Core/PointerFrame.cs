namespace Ausschnitt.Core;

public readonly record struct PointerFrame(
    int X,
    int Y,
    bool InRange,
    bool InContact,
    bool IsNew,
    bool TipDown,
    bool TipUp,
    bool Barrel,
    bool BarrelDown,
    bool BarrelUp,
    bool MiddleDown,
    bool MiddleUp,
    int Pressure);
