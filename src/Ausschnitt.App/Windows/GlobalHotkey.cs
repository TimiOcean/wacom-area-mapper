using System.Runtime.InteropServices;

namespace Ausschnitt.Windows;

internal sealed class GlobalHotkey : IDisposable
{
    private readonly NativeMethods.WndProc _proc;
    private Thread? _thread;
    private uint _threadId;
    private IntPtr _hwnd;
    private int _mods = (int)(NativeMethods.ModControl | NativeMethods.ModAlt);
    private int _vk = 0x41;
    private bool _registered;

    public GlobalHotkey()
    {
        _proc = Proc;
    }

    public event Action? Pressed;

    public void Start(int modifiers, int virtualKey)
    {
        _mods = modifiers;
        _vk = virtualKey;
        if (!OperatingSystem.IsWindows())
            return;
        if (_thread != null)
        {
            PostReset();
            return;
        }

        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = "Ausschnitt.Hotkey",
        };
        _thread.Start();
    }

    public void Update(int modifiers, int virtualKey)
    {
        _mods = modifiers;
        _vk = virtualKey;
        PostReset();
    }

    public void Dispose()
    {
        if (_threadId != 0)
            NativeMethods.PostThreadMessage(_threadId, 0x0012, IntPtr.Zero, IntPtr.Zero);
        _thread?.Join(500);
        _thread = null;
    }

    private void PostReset()
    {
        if (_threadId != 0)
            NativeMethods.PostThreadMessage(_threadId, NativeMethods.WmAppResetHotkey, IntPtr.Zero, IntPtr.Zero);
    }

    private void Loop()
    {
        _threadId = NativeMethods.GetCurrentThreadId();
        var cls = new NativeMethods.WNDCLASS
        {
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_proc),
            lpszClassName = "AusschnittHotkeyWindow",
        };
        NativeMethods.RegisterClass(ref cls);
        _hwnd = NativeMethods.CreateWindowEx(0, cls.lpszClassName, "", 0, 0, 0, 0, 0, NativeMethods.HwndMessage, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        Register();
        while (NativeMethods.GetMessage(out var message, IntPtr.Zero, 0, 0))
        {
            if (message.message == NativeMethods.WmAppResetHotkey)
                Register();
            else if (message.message == NativeMethods.WmHotkey)
                Pressed?.Invoke();
            NativeMethods.TranslateMessage(ref message);
            NativeMethods.DispatchMessage(ref message);
        }

        if (_registered && _hwnd != IntPtr.Zero)
            NativeMethods.UnregisterHotKey(_hwnd, 1);
    }

    private void Register()
    {
        if (_hwnd == IntPtr.Zero)
            return;
        if (_registered)
            NativeMethods.UnregisterHotKey(_hwnd, 1);
        _registered = NativeMethods.RegisterHotKey(
            _hwnd,
            1,
            (uint)_mods | NativeMethods.ModNoRepeat,
            (uint)_vk);
    }

    private IntPtr Proc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }
}
