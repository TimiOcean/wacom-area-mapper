namespace Ausschnitt.Windows;

internal sealed class WacomServiceHost
{
    public static readonly string[] Names = ["WTabletServicePro", "WTabletServiceCon", "TabletServiceWacom"];

    private readonly List<string> _paused = [];

    public IReadOnlyList<string> FindInstalled()
    {
        if (!OperatingSystem.IsWindows())
            return [];

        var found = new List<string>();
        IntPtr manager = NativeMethods.OpenSCManager(null, null, NativeMethods.ScManagerConnect);
        if (manager == IntPtr.Zero)
            return found;

        try
        {
            foreach (string name in Names)
            {
                IntPtr service = NativeMethods.OpenService(manager, name, NativeMethods.ServiceQueryStatus);
                if (service == IntPtr.Zero)
                    continue;
                NativeMethods.CloseServiceHandle(service);
                found.Add(name);
            }
        }
        finally
        {
            NativeMethods.CloseServiceHandle(manager);
        }

        return found;
    }

    public bool AnyRunning()
    {
        return FindInstalled().Any(IsRunning);
    }

    /// <summary>
    /// Stops running Wacom services. Returns false when Windows denies access.
    /// </summary>
    public bool Pause()
    {
        if (!OperatingSystem.IsWindows())
            return true;

        _paused.Clear();
        foreach (string name in FindInstalled())
        {
            if (!IsRunning(name))
                continue;
            if (!Stop(name))
                return false;
            _paused.Add(name);
        }

        return true;
    }

    public void ResumePaused()
    {
        foreach (string name in _paused)
            Start(name);
        _paused.Clear();
    }

    public bool StartInstalled()
    {
        if (!OperatingSystem.IsWindows())
            return true;

        bool ok = true;
        foreach (string name in FindInstalled())
        {
            if (IsRunning(name))
                continue;
            if (!Start(name))
                ok = false;
        }

        _paused.Clear();
        return ok;
    }

    private static bool IsRunning(string name)
    {
        return TryQuery(name, out uint state) && state == NativeMethods.ServiceRunning;
    }

    private static bool Stop(string name)
    {
        if (!WithService(name, NativeMethods.ServiceStop | NativeMethods.ServiceQueryStatus, out IntPtr service))
            return false;
        try
        {
            var status = new NativeMethods.SERVICE_STATUS();
            if (!NativeMethods.ControlService(service, NativeMethods.ServiceControlStop, ref status))
            {
                int error = MarshalError();
                return error == 1062;
            }

            return Wait(service, NativeMethods.ServiceStopped);
        }
        finally
        {
            NativeMethods.CloseServiceHandle(service);
        }
    }

    private static bool Start(string name)
    {
        if (!WithService(name, NativeMethods.ServiceStart | NativeMethods.ServiceQueryStatus, out IntPtr service))
            return false;
        try
        {
            if (TryQueryHandle(service, out uint state) && state == NativeMethods.ServiceRunning)
                return true;
            if (!NativeMethods.StartService(service, 0, IntPtr.Zero))
            {
                int error = MarshalError();
                if (error != 1056)
                    return false;
            }

            return Wait(service, NativeMethods.ServiceRunning);
        }
        finally
        {
            NativeMethods.CloseServiceHandle(service);
        }
    }

    private static bool Wait(IntPtr service, uint desired)
    {
        var deadline = DateTime.UtcNow.AddSeconds(12);
        while (DateTime.UtcNow < deadline)
        {
            if (!TryQueryHandle(service, out uint state))
                return false;
            if (state == desired)
                return true;
            Thread.Sleep(150);
        }

        return false;
    }

    private static bool TryQuery(string name, out uint state)
    {
        state = 0;
        if (!WithService(name, NativeMethods.ServiceQueryStatus, out IntPtr service))
            return false;
        try
        {
            return TryQueryHandle(service, out state);
        }
        finally
        {
            NativeMethods.CloseServiceHandle(service);
        }
    }

    private static bool TryQueryHandle(IntPtr service, out uint state)
    {
        var status = new NativeMethods.SERVICE_STATUS();
        if (!NativeMethods.QueryServiceStatus(service, ref status))
        {
            state = 0;
            return false;
        }

        state = status.dwCurrentState;
        return true;
    }

    private static bool WithService(string name, uint access, out IntPtr service)
    {
        service = IntPtr.Zero;
        IntPtr manager = NativeMethods.OpenSCManager(null, null, NativeMethods.ScManagerConnect);
        if (manager == IntPtr.Zero)
            return false;
        service = NativeMethods.OpenService(manager, name, access);
        NativeMethods.CloseServiceHandle(manager);
        return service != IntPtr.Zero;
    }

    private static int MarshalError() => System.Runtime.InteropServices.Marshal.GetLastWin32Error();
}
