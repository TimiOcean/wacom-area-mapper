using Ausschnitt.Core;

namespace Ausschnitt.Windows;

internal static class VirtualScreen
{
    public static ScreenRect FromWin32()
    {
        return new ScreenRect(
            NativeMethods.GetSystemMetrics(NativeMethods.SmXVirtualScreen),
            NativeMethods.GetSystemMetrics(NativeMethods.SmYVirtualScreen),
            NativeMethods.GetSystemMetrics(NativeMethods.SmCxVirtualScreen),
            NativeMethods.GetSystemMetrics(NativeMethods.SmCyVirtualScreen));
    }

    public static bool TryGetCursor(out int x, out int y)
    {
        if (OperatingSystem.IsWindows() && NativeMethods.GetCursorPos(out var point))
        {
            x = point.X;
            y = point.Y;
            return true;
        }

        x = 0;
        y = 0;
        return false;
    }
}
