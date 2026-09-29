using System.Text.RegularExpressions;
using Ausschnitt.Core;

namespace Ausschnitt.Windows;

internal sealed class HidEndpoint
{
    public required string Path { get; init; }
    public required TabletSpec Tablet { get; init; }
    public required int InputLength { get; init; }
    public required int FeatureLength { get; init; }
    public bool CanOpen { get; init; }
}

internal sealed class HidPenSource : IDisposable
{
    private static readonly Regex IdPattern = new(@"vid_([0-9a-f]{4})&pid_([0-9a-f]{4})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private IntPtr _handle = new(-1);
    private Thread? _thread;
    private volatile bool _run;

    public IReadOnlyList<HidEndpoint> Scan()
    {
        if (!OperatingSystem.IsWindows())
            return [];

        var found = new List<HidEndpoint>();
        foreach (string path in EnumeratePaths())
        {
            if (!TryParseIds(path, out int vendor, out int product))
                continue;
            TabletSpec? known = TabletSpec.Find(vendor, product);
            if (known is null)
                continue;

            if (!TryInspect(path, out int inputLength, out int featureLength, out bool opened))
            {
                found.Add(new HidEndpoint
                {
                    Path = path,
                    Tablet = known.Value,
                    InputLength = 0,
                    FeatureLength = 0,
                    CanOpen = false,
                });
                continue;
            }

            found.Add(new HidEndpoint
            {
                Path = path,
                Tablet = known.Value,
                InputLength = inputLength,
                FeatureLength = featureLength,
                CanOpen = opened,
            });
        }

        return found
            .OrderByDescending(endpoint => endpoint.CanOpen)
            .ThenByDescending(endpoint => endpoint.InputLength is 10 or 11)
            .ToList();
    }

    public bool Start(HidEndpoint endpoint, Action<PenSample> onSample, Action<string> log)
    {
        Stop();
        _handle = Open(endpoint.Path);
        if (_handle == new IntPtr(-1) || _handle == IntPtr.Zero)
        {
            log("HID-Gerät ließ sich nicht öffnen.");
            _handle = new IntPtr(-1);
            return false;
        }

        TryEnablePen(_handle, endpoint.FeatureLength, log);
        bool strip = endpoint.InputLength >= 11;
        int length = Math.Max(endpoint.InputLength, 16);
        _run = true;
        _thread = new Thread(() => ReadLoop(length, strip, onSample, log))
        {
            IsBackground = true,
            Name = "Ausschnitt.Pen",
        };
        _thread.Start();
        return true;
    }

    public void Stop()
    {
        _run = false;
        IntPtr handle = _handle;
        _handle = new IntPtr(-1);
        if (handle != IntPtr.Zero && handle != new IntPtr(-1))
            NativeMethods.CloseHandle(handle);
        _thread?.Join(500);
        _thread = null;
    }

    public void Dispose() => Stop();

    private void ReadLoop(int length, bool strip, Action<PenSample> onSample, Action<string> log)
    {
        var buffer = new byte[Math.Max(length, 64)];
        while (_run)
        {
            if (!NativeMethods.ReadFile(_handle, buffer, (uint)buffer.Length, out uint read, IntPtr.Zero))
                break;
            if (read == 0)
                continue;
            if (IntuosReportParser.TryParse(buffer.AsSpan(0, (int)read), strip, out PenSample sample))
                onSample(sample);
        }

        if (_run)
            log("Lesen vom Tablett wurde unterbrochen.");
    }

    private static void TryEnablePen(IntPtr handle, int featureLength, Action<string> log)
    {
        int size = Math.Max(featureLength, 2);
        var feature = new byte[size];
        feature[0] = 0x02;
        feature[1] = 0x02;
        if (!NativeMethods.HidD_SetFeature(handle, feature, feature.Length))
            log("Startbefehl 02 02 wurde nicht angenommen. Der Stift kann trotzdem Daten schicken.");
    }

    private static bool TryInspect(string path, out int inputLength, out int featureLength, out bool opened)
    {
        inputLength = 0;
        featureLength = 0;
        opened = false;
        IntPtr handle = Open(path);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
            return false;
        opened = true;
        try
        {
            if (!NativeMethods.HidD_GetPreparsedData(handle, out IntPtr preparsed))
                return true;
            try
            {
                var caps = new NativeMethods.HIDP_CAPS();
                if (NativeMethods.HidP_GetCaps(preparsed, ref caps) == 0x00110000)
                {
                    inputLength = caps.InputReportByteLength;
                    featureLength = caps.FeatureReportByteLength;
                }
            }
            finally
            {
                NativeMethods.HidD_FreePreparsedData(preparsed);
            }

            return true;
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }

    private static IntPtr Open(string path)
    {
        return NativeMethods.CreateFile(
            path,
            NativeMethods.GenericRead | NativeMethods.GenericWrite,
            NativeMethods.FileShareRead | NativeMethods.FileShareWrite,
            IntPtr.Zero,
            NativeMethods.OpenExisting,
            0,
            IntPtr.Zero);
    }

    private static IEnumerable<string> EnumeratePaths()
    {
        NativeMethods.HidD_GetHidGuid(out Guid guid);
        IntPtr set = NativeMethods.SetupDiGetClassDevs(ref guid, null, IntPtr.Zero, NativeMethods.DigcfPresent | NativeMethods.DigcfDeviceInterface);
        if (set == IntPtr.Zero || set == new IntPtr(-1))
            yield break;

        try
        {
            var data = new NativeMethods.SP_DEVICE_INTERFACE_DATA
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.SP_DEVICE_INTERFACE_DATA>(),
            };
            for (int index = 0; NativeMethods.SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, index, ref data); index++)
            {
                NativeMethods.SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out uint required, IntPtr.Zero);
                if (required < 8)
                    continue;
                IntPtr detail = System.Runtime.InteropServices.Marshal.AllocHGlobal((int)required);
                try
                {
                    int cbSize = IntPtr.Size == 8 ? 8 : 6;
                    System.Runtime.InteropServices.Marshal.WriteInt32(detail, cbSize);
                    if (!NativeMethods.SetupDiGetDeviceInterfaceDetail(set, ref data, detail, required, out _, IntPtr.Zero))
                        continue;
                    int offset = IntPtr.Size == 8 ? 8 : 4;
                    string? path = System.Runtime.InteropServices.Marshal.PtrToStringUni(detail + offset);
                    if (!string.IsNullOrWhiteSpace(path))
                        yield return path;
                }
                finally
                {
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(detail);
                }
            }
        }
        finally
        {
            NativeMethods.SetupDiDestroyDeviceInfoList(set);
        }
    }

    private static bool TryParseIds(string path, out int vendor, out int product)
    {
        vendor = 0;
        product = 0;
        Match match = IdPattern.Match(path);
        if (!match.Success)
            return false;
        vendor = Convert.ToInt32(match.Groups[1].Value, 16);
        product = Convert.ToInt32(match.Groups[2].Value, 16);
        return true;
    }
}
