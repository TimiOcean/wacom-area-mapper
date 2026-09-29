namespace Ausschnitt.Core;

/// <summary>
/// Turns a stream of pen samples into edge-aware pointer frames.
/// Button 1 (closer to the nib on the Wacom One) is the barrel button.
/// Button 2 becomes a middle click, matching the tablet's usual default.
/// </summary>
public sealed class PenPointerComposer
{
    private bool _seen;
    private bool _contact;
    private bool _barrel;
    private bool _middle;

    public void Reset()
    {
        _seen = false;
        _contact = false;
        _barrel = false;
        _middle = false;
    }

    public PointerFrame? Next(PenSample sample, int screenX, int screenY, int deviceMaxPressure)
    {
        if (!sample.InRange)
        {
            if (!_seen && !_contact && !_barrel && !_middle)
                return null;

            var leave = new PointerFrame(
                screenX,
                screenY,
                InRange: false,
                InContact: false,
                IsNew: false,
                TipDown: false,
                TipUp: _contact,
                Barrel: false,
                BarrelDown: false,
                BarrelUp: _barrel,
                MiddleDown: false,
                MiddleUp: _middle,
                Pressure: 0);
            Reset();
            return leave;
        }

        bool contact = sample.Pressure > 0;
        bool barrel = sample.Button1;
        bool middle = sample.Button2;
        int pressure = contact ? PressureScale.ToWindows(sample.Pressure, deviceMaxPressure) : 0;

        var frame = new PointerFrame(
            screenX,
            screenY,
            InRange: true,
            InContact: contact,
            IsNew: !_seen,
            TipDown: contact && !_contact,
            TipUp: !contact && _contact,
            Barrel: barrel,
            BarrelDown: barrel && !_barrel,
            BarrelUp: !barrel && _barrel,
            MiddleDown: middle && !_middle,
            MiddleUp: !middle && _middle,
            Pressure: pressure);

        _seen = true;
        _contact = contact;
        _barrel = barrel;
        _middle = middle;
        return frame;
    }
}
