using System.Security.Principal;
using Microsoft.Win32;

namespace Ausschnitt.Windows;

internal static class WindowsSession
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunName = "Ausschnitt";

    public static bool IsAdmin()
    {
        if (!OperatingSystem.IsWindows())
            return true;
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static bool TryRelaunchElevated()
    {
        if (!OperatingSystem.IsWindows() || Environment.ProcessPath is null)
            return false;
        try
        {
            return System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = Environment.ProcessPath,
                UseShellExecute = true,
                Verb = "runas",
                Arguments = "--elevated",
            }) is not null;
        }
        catch
        {
            return false;
        }
    }

    public static void SetStartup(bool enabled)
    {
        if (!OperatingSystem.IsWindows() || Environment.ProcessPath is null)
            return;
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key is null)
            return;
        if (enabled)
            key.SetValue(RunName, $"\"{Environment.ProcessPath}\"");
        else
            key.DeleteValue(RunName, throwOnMissingValue: false);
    }

    public static void MakeClickThrough(IntPtr hwnd)
    {
        if (!OperatingSystem.IsWindows() || hwnd == IntPtr.Zero)
            return;
        int style = NativeMethods.GetWindowLong(hwnd, NativeMethods.GwlExStyle);
        style |= NativeMethods.WsExTransparent | NativeMethods.WsExLayered | NativeMethods.WsExNoActivate | NativeMethods.WsExToolWindow;
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GwlExStyle, style);
    }
}
