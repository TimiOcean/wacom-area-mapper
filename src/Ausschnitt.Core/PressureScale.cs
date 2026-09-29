namespace Ausschnitt.Core;

public static class PressureScale
{
    /// <summary>
    /// Windows synthetic pen packets accept pressure from 0 to 1024.
    /// The Wacom One reports 0 to 2047, so direct mode keeps 1024 steps.
    /// </summary>
    public const int WindowsMax = 1024;

    public static int ToWindows(int raw, int deviceMax)
    {
        if (raw <= 0 || deviceMax <= 0)
            return 0;
        if (raw >= deviceMax)
            return WindowsMax;
        return (int)Math.Round(raw * (double)WindowsMax / deviceMax);
    }
}
