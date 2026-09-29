using Ausschnitt.Core;

namespace Ausschnitt.Windows;

internal sealed class WindowsPenInjector : IDisposable
{
    private const int PtPen = 3;
    private const int PointerFeedbackDefault = 1;
    private const uint FlagNew = 0x00000001;
    private const uint FlagInRange = 0x00000002;
    private const uint FlagInContact = 0x00000004;
    private const uint FlagFirstButton = 0x00000010;
    private const uint FlagSecondButton = 0x00000020;
    private const uint FlagPrimary = 0x00002000;
    private const uint FlagConfidence = 0x00004000;
    private const uint FlagDown = 0x00010000;
    private const uint FlagUpdate = 0x00020000;
    private const uint FlagUp = 0x00040000;
    private const uint PenFlagBarrel = 0x00000001;
    private const uint PenMaskPressure = 0x00000001;
    private const int ChangeNone = 0;
    private const int ChangeFirstDown = 1;
    private const int ChangeFirstUp = 2;
    private const int ChangeSecondDown = 3;
    private const int ChangeSecondUp = 4;
    private const uint MouseMiddleDown = 0x0020;
    private const uint MouseMiddleUp = 0x0040;

    private IntPtr _device;
    private readonly PenPointerComposer _composer = new();

    public bool Start(Action<string> log)
    {
        if (!OperatingSystem.IsWindows())
            return false;
        if (_device != IntPtr.Zero)
            return true;
        _device = NativeMethods.CreateSyntheticPointerDevice(PtPen, 1, PointerFeedbackDefault);
        if (_device == IntPtr.Zero)
        {
            log("Windows hat keinen synthetischen Stift angelegt.");
            return false;
        }

        _composer.Reset();
        return true;
    }

    public void Submit(PenSample sample, int screenX, int screenY, int deviceMaxPressure)
    {
        if (_device == IntPtr.Zero)
            return;
        PointerFrame? frame = _composer.Next(sample, screenX, screenY, deviceMaxPressure);
        if (frame is null)
            return;
        Inject(frame.Value);
    }

    public void Reset() => _composer.Reset();

    public void Dispose()
    {
        if (_device != IntPtr.Zero)
        {
            NativeMethods.DestroySyntheticPointerDevice(_device);
            _device = IntPtr.Zero;
        }
    }

    private void Inject(PointerFrame frame)
    {
        uint flags = FlagPrimary | FlagConfidence | FlagUpdate;
        if (frame.IsNew)
            flags |= FlagNew;
        if (frame.InRange)
            flags |= FlagInRange;
        if (frame.InContact)
            flags |= FlagInContact | FlagFirstButton;
        if (frame.TipDown)
            flags |= FlagDown;
        if (frame.TipUp || !frame.InRange)
            flags |= FlagUp;
        if (frame.Barrel)
            flags |= FlagSecondButton;

        int change = ChangeNone;
        if (frame.TipDown)
            change = ChangeFirstDown;
        else if (frame.TipUp)
            change = ChangeFirstUp;
        else if (frame.BarrelDown)
            change = ChangeSecondDown;
        else if (frame.BarrelUp)
            change = ChangeSecondUp;

        (int hx, int hy) = ToHimetric(frame.X, frame.Y);
        var info = new NativeMethods.POINTER_TYPE_INFO
        {
            type = PtPen,
            penInfo = new NativeMethods.POINTER_PEN_INFO
            {
                pointerInfo = new NativeMethods.POINTER_INFO
                {
                    pointerType = PtPen,
                    pointerId = 1,
                    pointerFlags = flags,
                    ptPixelLocation = new NativeMethods.POINT { X = frame.X, Y = frame.Y },
                    ptPixelLocationRaw = new NativeMethods.POINT { X = frame.X, Y = frame.Y },
                    ptHimetricLocation = new NativeMethods.POINT { X = hx, Y = hy },
                    ptHimetricLocationRaw = new NativeMethods.POINT { X = hx, Y = hy },
                    ButtonChangeType = change,
                },
                penFlags = frame.Barrel ? PenFlagBarrel : 0,
                penMask = PenMaskPressure,
                pressure = (uint)Math.Clamp(frame.Pressure, 0, PressureScale.WindowsMax),
            },
        };

        NativeMethods.InjectSyntheticPointerInput(_device, [info], 1);

        if (frame.MiddleDown)
            ClickMiddle(true);
        else if (frame.MiddleUp)
            ClickMiddle(false);
    }

    private static void ClickMiddle(bool down)
    {
        var input = new NativeMethods.INPUT
        {
            type = 0,
            mi = new NativeMethods.MOUSEINPUT
            {
                dwFlags = down ? MouseMiddleDown : MouseMiddleUp,
            },
        };
        NativeMethods.SendInput(1, [input], System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static (int X, int Y) ToHimetric(int x, int y)
    {
        uint dpiX = 96;
        uint dpiY = 96;
        var point = new NativeMethods.POINT { X = x, Y = y };
        IntPtr monitor = NativeMethods.MonitorFromPoint(point, 2);
        if (monitor != IntPtr.Zero && NativeMethods.GetDpiForMonitor(monitor, 0, out uint mx, out uint my) == 0 && mx > 0 && my > 0)
        {
            dpiX = mx;
            dpiY = my;
        }

        return ((int)Math.Round(x * 2540d / dpiX), (int)Math.Round(y * 2540d / dpiY));
    }
}
