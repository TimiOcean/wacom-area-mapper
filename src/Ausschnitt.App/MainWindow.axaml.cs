using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Ausschnitt.Core;
using Ausschnitt.Session;
using Ausschnitt.ViewModels;
using Ausschnitt.Windows;

namespace Ausschnitt;

public partial class MainWindow : Window
{
    private readonly MappingSession _session = new();
    private readonly MainViewModel _model;
    private readonly GlobalHotkey _hotkey = new();
    private readonly AppSettings _settings;
    private AreaFrameWindow? _frame;
    private bool _allowClose;
    private bool _applying;

    public MainWindow()
    {
        _settings = AppSettingsStore.Load();
        _model = new MainViewModel(_settings);
        InitializeComponent();
        DataContext = _model;
        Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Ausschnitt/Assets/ausschnitt.png")));
        Opened += (_, _) => OnOpened();
        Closing += OnClosing;
        KeyDown += OnKeyDown;
        _model.PickRequested += () => _ = PickAsync();
        _model.FullRequested += () => _ = ClearAsync();
        _model.ManualRequested += () => _ = ApplyManualAsync();
        _model.ElevateRequested += Elevate;
        _model.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(MainViewModel.KeepProportions) or nameof(MainViewModel.SelectedOrientationLabel))
                _ = OptionsChangedAsync();
            if (args.PropertyName == nameof(MainViewModel.RunAtStartup))
                WindowsSession.SetStartup(_model.RunAtStartup);
        };
        TabletPad.PointerMoved += OnPadMoved;
        TabletPad.PointerExited += (_, _) =>
        {
            PadDot.IsVisible = false;
            _model.HidePen();
        };
        _session.PenAt += (x, y, _) => Dispatcher.UIThread.Post(() => _model.ShowPen(x, y));
        _hotkey.Pressed += () => Dispatcher.UIThread.Post(() => _ = PickAsync());
    }

    public MappingSession Session => _session;

    public void PrepareExit()
    {
        _allowClose = true;
        _frame?.Close();
        _hotkey.Dispose();
        _session.Dispose();
    }

    private void OnOpened()
    {
        RefreshScreens();
        _session.Detect(Log);
        DescribeDevice();
        _hotkey.Start(_settings.HotkeyModifiers, _settings.HotkeyVirtualKey);
        if (_settings.Area is { } area)
        {
            _model.Remember(area);
            _ = ApplyAreaAsync(area, announce: false);
        }
        BuildTray();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowClose)
            return;
        e.Cancel = true;
        Hide();
        Log("Ausschnitt liegt in der Taskleiste. Beenden steht im Menü dort.");
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_model.CapturingHotkey)
            return;
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;
        if (e.Key == Key.Escape)
        {
            _model.CapturingHotkey = false;
            Log("Kürzel unverändert.");
            return;
        }

        int? vk = ToVirtualKey(e.Key);
        if (vk is null || e.KeyModifiers == KeyModifiers.None)
        {
            Log("Nimm eine Taste mit Strg, Alt oder Umschalt.");
            return;
        }

        _settings.HotkeyModifiers = ToNativeModifiers(e.KeyModifiers);
        _settings.HotkeyVirtualKey = vk.Value;
        _settings.HotkeyLabel = FormatHotkey(e.KeyModifiers, e.Key);
        _model.NoteHotkey(_settings.HotkeyLabel);
        _model.CapturingHotkey = false;
        _hotkey.Update(_settings.HotkeyModifiers, _settings.HotkeyVirtualKey);
        Persist();
        Log($"Kürzel ist jetzt {_settings.HotkeyLabel}.");
        e.Handled = true;
    }

    private async Task PickAsync()
    {
        if (_applying)
            return;
        RefreshScreens();
        var overlay = new AreaOverlay { TabletAspect = _session.AssumedTablet.Aspect };
        ScreenRect? area = await overlay.PickAsync();
        if (area is not { IsUsable: true } rect)
        {
            Log("Abgebrochen.");
            return;
        }

        await ApplyAreaAsync(rect, announce: true);
    }

    private async Task ApplyManualAsync()
    {
        if (!_model.TryReadManual(out ScreenRect area))
        {
            _model.ShowBanner("Die Pixel brauchen ganze Zahlen, und das Rechteck muss mindestens 8 Pixel groß sein.", true, false);
            return;
        }

        await ApplyAreaAsync(area, announce: true);
    }

    private async Task ClearAsync()
    {
        if (_applying)
            return;
        _applying = true;
        _model.Busy = true;
        RefreshScreens();
        ScreenRect full = CurrentDesktop();
        ApplyResult result = await Task.Run(() => _session.Clear(full, Log));
        _settings.SetArea(null);
        Persist();
        _model.RememberFull();
        _model.Busy = false;
        _applying = false;
        Present(result);
        HideFrame();
    }

    private async Task ApplyAreaAsync(ScreenRect area, bool announce)
    {
        if (_applying)
            return;
        _applying = true;
        _model.Busy = true;
        _model.Remember(area);
        _settings.SetArea(area);
        _settings.KeepProportions = _model.KeepProportions;
        _settings.Orientation = _model.Orientation.ToString();
        _settings.Engine = _model.Engine;
        Persist();
        RefreshScreens();
        ScreenRect full = CurrentDesktop();
        ApplyResult result = await Task.Run(() => _session.Apply(
            area,
            _model.KeepProportions,
            _model.Orientation,
            _model.Engine,
            full,
            Log));
        _model.Busy = false;
        _applying = false;
        if (announce || !result.Ok)
            Present(result);
        else
            _model.ShowBanner(result.Message, !result.Ok, result.NeedsAdmin);
        DescribeDevice();
        if (result.Ok && result.Engine is LiveEngine.Direct or LiveEngine.Driver)
            ShowFrame(area);
        else
            HideFrame();
    }

    private async Task OptionsChangedAsync()
    {
        _settings.KeepProportions = _model.KeepProportions;
        _settings.Orientation = _model.Orientation.ToString();
        Persist();
        if (_settings.Area is not { } area)
            return;
        if (_session.Engine == LiveEngine.Direct)
        {
            _session.UpdateLive(area, _model.KeepProportions, _model.Orientation);
            Log(_model.KeepProportions
                ? "Proportionen an: ein Streifen am Tabletrand bleibt ungenutzt."
                : "Die ganze Stiftfläche füllt das Rechteck, auch wenn sich das Seitenverhältnis unterscheidet.");
            return;
        }

        if (_session.Engine == LiveEngine.Driver)
            await ApplyAreaAsync(area, announce: true);
    }

    private void Present(ApplyResult result)
    {
        _model.ShowBanner(result.Message, !result.Ok, result.NeedsAdmin);
        _model.EngineLine = result.Engine switch
        {
            LiveEngine.Driver => "Aktiv über den Wacom-Treiber. Druck bleibt bei 2048 Stufen.",
            LiveEngine.Direct => "Aktiv im Direktmodus. Der Wacom-Dienst ist pausiert, Druck hat 1024 Stufen.",
            LiveEngine.Preview => "Nur Vorschau. Auf Windows 11 wird der Stift wirklich umgelegt.",
            _ => "Kein Ausschnitt aktiv.",
        };
        Log(result.Message);
    }

    private void DescribeDevice()
    {
        if (_session.TabletVisible)
        {
            _model.TabletLine = _session.TabletReadable
                ? $"{_session.AssumedTablet.Name} ist angeschlossen."
                : $"{_session.AssumedTablet.Name} steckt, der Treiber hält das Gerät.";
        }
        else if (_session.InstalledServices.Count > 0)
        {
            _model.TabletLine = "Wacom-Dienst ist da, das Tablett meldet sich noch nicht.";
        }
        else if (!OperatingSystem.IsWindows())
        {
            _model.TabletLine = "Vorschau auf diesem Rechner. Am Windows-11-PC wird das Wacom One Small (CTL-472) erkannt.";
        }
        else
        {
            _model.TabletLine = "Kein Wacom One erkannt. USB prüfen: CTL-472 Small oder CTL-672 Medium.";
        }

        _model.DetailLine = _session.PreferencePatchable
            ? "Automatisch schreibt den Bereich in den Wacom-Treiber, solange Proportionen aus sind und die Ausrichtung normal bleibt. Sonst liest Ausschnitt das Tablett selbst."
            : "Ausschnitt liest das Tablett selbst und pausiert den Wacom-Dienst, solange ein Bereich aktiv ist. Shift beim Aufziehen sperrt 16:10.";
    }

    private void Elevate()
    {
        Persist();
        if (!WindowsSession.TryRelaunchElevated())
        {
            _model.ShowBanner("Die Abfrage für Administratorrechte wurde abgebrochen.", true, true);
            return;
        }

        PrepareExit();
        if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    private void RefreshScreens()
    {
        var screens = Screens?.All;
        if (screens is null)
            return;
        _model.SetScreens(screens.Select(screen => new ScreenRect(
            screen.Bounds.X,
            screen.Bounds.Y,
            screen.Bounds.Width,
            screen.Bounds.Height)).ToList());
    }

    private ScreenRect CurrentDesktop()
    {
        if (OperatingSystem.IsWindows())
        {
            ScreenRect native = VirtualScreen.FromWin32();
            if (native.IsUsable)
                return native;
        }

        return ScreenRect.Union(_model.Screens);
    }

    private void OnPadMoved(object? sender, PointerEventArgs e)
    {
        var point = e.GetPosition(TabletPad);
        if (TabletPad.Bounds.Width <= 0 || TabletPad.Bounds.Height <= 0)
            return;
        double nx = Math.Clamp(point.X / TabletPad.Bounds.Width, 0, 1);
        double ny = Math.Clamp(point.Y / TabletPad.Bounds.Height, 0, 1);
        PadDot.IsVisible = true;
        Canvas.SetLeft(PadDot, point.X - 6);
        Canvas.SetTop(PadDot, point.Y - 6);
        var target = _model.ActiveArea ?? CurrentDesktop();
        if (!target.IsUsable)
            return;
        var tablet = _session.AssumedTablet;
        var mapped = AreaMapper.Map(nx * tablet.MaxX, ny * tablet.MaxY, tablet, target, _model.KeepProportions, _model.Orientation);
        _model.ShowPen(mapped.X, mapped.Y);
    }

    private void ShowFrame(ScreenRect area)
    {
        if (!OperatingSystem.IsWindows())
            return;
        _frame ??= new AreaFrameWindow();
        _frame.Place(area);
    }

    private void HideFrame()
    {
        if (_frame is null)
            return;
        _frame.Hide();
    }

    private void Persist()
    {
        _settings.KeepProportions = _model.KeepProportions;
        _settings.RunAtStartup = _model.RunAtStartup;
        _settings.Orientation = _model.Orientation.ToString();
        _settings.Engine = _model.Engine;
        _settings.HotkeyLabel = _model.HotkeyLabel;
        AppSettingsStore.Save(_settings);
    }

    private void Log(string message)
    {
        void Write()
        {
            LogLine.Text = message;
        }

        if (Dispatcher.UIThread.CheckAccess())
            Write();
        else
            Dispatcher.UIThread.Post(Write);
    }

    private void BuildTray()
    {
        var menu = new NativeMenu();
        var pick = new NativeMenuItem("Bereich aufziehen");
        pick.Click += (_, _) => _ = PickAsync();
        var full = new NativeMenuItem("Ganzer Bildschirm");
        full.Click += (_, _) => _ = ClearAsync();
        var show = new NativeMenuItem("Fenster zeigen");
        show.Click += (_, _) => ShowFront();
        var exit = new NativeMenuItem("Beenden");
        exit.Click += (_, _) =>
        {
            PrepareExit();
            if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown();
        };
        menu.Items.Add(pick);
        menu.Items.Add(full);
        menu.Items.Add(show);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(exit);

        var icon = new TrayIcon
        {
            Icon = Icon,
            ToolTipText = "Ausschnitt",
            Menu = menu,
        };
        icon.Clicked += (_, _) => ShowFront();
        var icons = new TrayIcons { icon };
        TrayIcon.SetIcons(Application.Current!, icons);
    }

    private void ShowFront()
    {
        if (!IsVisible)
            Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private static int? ToVirtualKey(Key key)
    {
        if (key is >= Key.A and <= Key.Z)
            return 0x41 + (key - Key.A);
        if (key is >= Key.D0 and <= Key.D9)
            return 0x30 + (key - Key.D0);
        if (key is >= Key.F1 and <= Key.F24)
            return 0x70 + (key - Key.F1);
        return key == Key.Space ? 0x20 : null;
    }

    private static int ToNativeModifiers(KeyModifiers modifiers)
    {
        int value = 0;
        if (modifiers.HasFlag(KeyModifiers.Alt))
            value |= 0x0001;
        if (modifiers.HasFlag(KeyModifiers.Control))
            value |= 0x0002;
        if (modifiers.HasFlag(KeyModifiers.Shift))
            value |= 0x0004;
        if (modifiers.HasFlag(KeyModifiers.Meta))
            value |= 0x0008;
        return value;
    }

    private static string FormatHotkey(KeyModifiers modifiers, Key key)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(KeyModifiers.Control))
            parts.Add("Strg");
        if (modifiers.HasFlag(KeyModifiers.Alt))
            parts.Add("Alt");
        if (modifiers.HasFlag(KeyModifiers.Shift))
            parts.Add("Umschalt");
        if (modifiers.HasFlag(KeyModifiers.Meta))
            parts.Add("Win");
        parts.Add(key.ToString());
        return string.Join(" + ", parts);
    }
}
