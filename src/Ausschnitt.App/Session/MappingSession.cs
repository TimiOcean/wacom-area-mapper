using Ausschnitt.Core;
using Ausschnitt.Windows;

namespace Ausschnitt.Session;

public enum LiveEngine
{
    None,
    Preview,
    Driver,
    Direct,
}

public sealed record ApplyResult(bool Ok, bool NeedsAdmin, LiveEngine Engine, string Message);

public sealed class MappingSession : IDisposable
{
    private readonly WacomServiceHost _services = new();
    private readonly HidPenSource _hid = new();
    private readonly WindowsPenInjector _injector = new();
    private readonly object _gate = new();
    private ScreenRect _area;
    private bool _proportions;
    private PenOrientation _orientation = PenOrientation.Normal;
    private TabletSpec _tablet = TabletSpec.OneSmall;
    private bool _driverTouched;
    private bool _directRunning;
    private int _penTick;

    public TabletSpec AssumedTablet { get; private set; } = TabletSpec.OneSmall;
    public bool TabletVisible { get; private set; }
    public bool TabletReadable { get; private set; }
    public bool PreferencePatchable { get; private set; }
    public string? PreferencePath { get; private set; }
    public IReadOnlyList<string> InstalledServices { get; private set; } = [];
    public LiveEngine Engine { get; private set; } = LiveEngine.None;

    public event Action<int, int, bool>? PenAt;

    public void Detect(Action<string> log)
    {
        InstalledServices = _services.FindInstalled();
        PreferencePath = LocatePreference();
        PreferencePatchable = PreferencePath is not null && WacomPreferencePatcher.LooksPatchable(PreferencePath);
        TabletVisible = false;
        TabletReadable = false;

        if (OperatingSystem.IsWindows())
        {
            var endpoints = _hid.Scan();
            HidEndpoint? best = endpoints.FirstOrDefault(endpoint => endpoint.InputLength is 10 or 11)
                ?? endpoints.FirstOrDefault();
            if (best is not null)
            {
                AssumedTablet = best.Tablet;
                TabletVisible = true;
                TabletReadable = best.CanOpen;
                log($"{best.Tablet.Name} erkannt{(best.CanOpen ? "" : ", vom Treiber belegt")}.");
            }
            else if (InstalledServices.Count > 0)
            {
                log("Wacom-Dienst ist installiert, das Tablett hat sich noch nicht als HID gemeldet.");
            }
        }

        if (PreferencePatchable)
            log("Wacom-Einstellungen enthalten einen Bildschirmbereich und können beschrieben werden.");
        else if (PreferencePath is not null)
            log("Wacom-Datei gefunden, aber ohne lesbaren Bildschirmbereich. Ausschnitt nutzt den Direktmodus.");
    }

    public ApplyResult Apply(
        ScreenRect area,
        bool keepProportions,
        PenOrientation orientation,
        EnginePreference preference,
        ScreenRect fullDesktop,
        Action<string> log)
    {
        lock (_gate)
        {
            _area = area;
            _proportions = keepProportions;
            _orientation = orientation;
            _tablet = AssumedTablet;
        }

        bool wantDriver = preference == EnginePreference.Driver
            || (preference == EnginePreference.Auto
                && PreferencePatchable
                && !keepProportions
                && orientation == PenOrientation.Normal);

        if (wantDriver)
        {
            ApplyResult driver = ApplyDriver(area, fullDesktop, log);
            if (driver.Ok || driver.NeedsAdmin || preference == EnginePreference.Driver)
                return driver;
            log(driver.Message + " Wechsel in den Direktmodus.");
        }

        if (!OperatingSystem.IsWindows())
        {
            Engine = LiveEngine.Preview;
            return new ApplyResult(true, false, LiveEngine.Preview, "Unter Linux gilt der Bereich nur in der Vorschau. Auf Windows 11 legt er den Stift um.");
        }

        return ApplyDirect(log);
    }

    public ApplyResult Clear(ScreenRect fullDesktop, Action<string> log)
    {
        StopDirect();
        if (_driverTouched && PreferencePath is not null && OperatingSystem.IsWindows())
        {
            if (!WindowsSession.IsAdmin())
                return new ApplyResult(false, true, Engine, "Zum Zurücksetzen auf den ganzen Bildschirm braucht Ausschnitt Administratorrechte.");
            if (!_services.Pause())
            {
                _services.ResumePaused();
                return new ApplyResult(false, true, Engine, "Der Wacom-Dienst ließ sich nicht anhalten.");
            }

            var outcome = WacomPreferencePatcher.PatchFile(PreferencePath, fullDesktop);
            _services.StartInstalled();
            _driverTouched = false;
            Engine = LiveEngine.None;
            if (outcome.Status != PreferencePatchStatus.Updated)
                return new ApplyResult(false, false, LiveEngine.None, "Der Bereich im Treiber ließ sich nicht zurücksetzen.");
            log("Treiber zeigt wieder auf den ganzen Bildschirm.");
            return new ApplyResult(true, false, LiveEngine.None, "Der Stift steuert wieder den ganzen Bildschirm.");
        }

        if (OperatingSystem.IsWindows())
            _services.StartInstalled();
        Engine = LiveEngine.None;
        log("Ausschnitt ist aus. Der Wacom-Treiber hat den Stift wieder.");
        return new ApplyResult(true, false, LiveEngine.None, "Der Stift steuert wieder den ganzen Bildschirm.");
    }

    public void UpdateLive(ScreenRect area, bool keepProportions, PenOrientation orientation)
    {
        lock (_gate)
        {
            _area = area;
            _proportions = keepProportions;
            _orientation = orientation;
        }
    }

    public void Dispose()
    {
        StopDirect();
        if (OperatingSystem.IsWindows())
            _services.StartInstalled();
        _hid.Dispose();
        _injector.Dispose();
    }

    private ApplyResult ApplyDriver(ScreenRect area, ScreenRect fullDesktop, Action<string> log)
    {
        if (PreferencePath is null || !PreferencePatchable)
            return new ApplyResult(false, false, Engine, "Die Wacom-Datei enthält keinen beschreibbaren Bildschirmbereich.");
        if (!OperatingSystem.IsWindows())
            return new ApplyResult(false, false, Engine, "Den Wacom-Treiber gibt es hier nicht.");
        if (!WindowsSession.IsAdmin())
            return new ApplyResult(false, true, Engine, "Der Treiber wird neu geladen. Dafür braucht Ausschnitt einmal Administratorrechte.");

        StopDirect();
        if (!_services.Pause())
        {
            _services.ResumePaused();
            return new ApplyResult(false, true, Engine, "Der Wacom-Dienst ließ sich nicht anhalten.");
        }

        PreferencePatchOutcome outcome = WacomPreferencePatcher.PatchFile(PreferencePath, area);
        bool started = _services.StartInstalled();
        if (outcome.Status != PreferencePatchStatus.Updated)
        {
            return new ApplyResult(false, false, Engine, "Die Wacom-Datei ließ sich nicht ändern.");
        }

        _driverTouched = true;
        Engine = LiveEngine.Driver;
        log($"Bildschirmbereich in {Path.GetFileName(PreferencePath)} geschrieben ({outcome.Replacements} Werte).");
        string message = started
            ? "Der Wacom-Treiber mappt die Stiftfläche jetzt nur noch auf das Rechteck. Druck und Windows Ink bleiben original."
            : "Der Bereich steht in der Datei, der Dienst ist aber nicht wieder angesprungen.";
        return new ApplyResult(started, false, LiveEngine.Driver, message);
    }

    private ApplyResult ApplyDirect(Action<string> log)
    {
        if (InstalledServices.Count > 0 && _services.AnyRunning())
        {
            if (!WindowsSession.IsAdmin())
                return new ApplyResult(false, true, Engine, "Damit der Stift nicht doppelt ankommt, pausiert Ausschnitt den Wacom-Dienst. Dafür braucht es Administratorrechte.");
            if (!_services.Pause())
            {
                _services.ResumePaused();
                return new ApplyResult(false, true, Engine, "Der Wacom-Dienst ließ sich nicht pausieren.");
            }
        }

        HidEndpoint? endpoint = _hid.Scan().FirstOrDefault(item => item.CanOpen && item.InputLength is 10 or 11)
            ?? _hid.Scan().FirstOrDefault(item => item.CanOpen);
        if (endpoint is null)
        {
            _services.StartInstalled();
            return new ApplyResult(false, false, Engine, "Das Wacom One ist nicht lesbar. USB prüfen und Ausschnitt als Administrator starten.");
        }

        AssumedTablet = endpoint.Tablet;
        lock (_gate)
            _tablet = endpoint.Tablet;

        if (!_injector.Start(log) || !_hid.Start(endpoint, OnSample, log))
        {
            StopDirect();
            _services.StartInstalled();
            return new ApplyResult(false, false, Engine, "Der Stiftkanal ließ sich nicht öffnen.");
        }

        _directRunning = true;
        Engine = LiveEngine.Direct;
        log($"Direktmodus: {endpoint.Tablet.Name}, Bericht {endpoint.InputLength} Bytes.");
        return new ApplyResult(true, false, LiveEngine.Direct,
            "Die ganze Stiftfläche steuert jetzt nur das Rechteck. Der Wacom-Dienst ist pausiert und startet wieder, wenn du den ganzen Bildschirm wählst oder Ausschnitt beendest. Der Stiftdruck hat in diesem Modus 1024 Stufen.");
    }

    private void OnSample(PenSample sample)
    {
        ScreenRect area;
        bool proportions;
        PenOrientation orientation;
        TabletSpec tablet;
        lock (_gate)
        {
            area = _area;
            proportions = _proportions;
            orientation = _orientation;
            tablet = _tablet;
        }

        if (!sample.InRange || !area.IsUsable)
        {
            _injector.Submit(sample, 0, 0, tablet.MaxPressure);
            return;
        }

        (int x, int y) = AreaMapper.MapPixel(sample.X, sample.Y, tablet, area, proportions, orientation);
        _injector.Submit(sample, x, y, tablet.MaxPressure);
        if ((Interlocked.Increment(ref _penTick) & 3) == 0)
            PenAt?.Invoke(x, y, sample.Pressure > 0);
    }

    private void StopDirect()
    {
        if (!_directRunning)
            return;
        _hid.Stop();
        _injector.Reset();
        _directRunning = false;
    }

    private static string? LocatePreference()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string[] candidates =
        [
            Path.Combine(appData, "WTablet", "Wacom_Tablet.dat"),
            Path.Combine(appData, "Wacom", "Wacom_Tablet.dat"),
        ];
        return candidates.FirstOrDefault(File.Exists);
    }
}
