using System.ComponentModel;
using System.Runtime.CompilerServices;
using Ausschnitt.Core;
using Ausschnitt.Session;

namespace Ausschnitt.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly AppSettings _settings;
    private string _tabletLine = "Suche nach dem Wacom One …";
    private string _engineLine = "Noch kein Bereich aktiv.";
    private string _areaLine = "Ganzer Bildschirm";
    private string _detailLine = "Strg+Alt+A, Rechteck ziehen, loslassen.";
    private string _banner = "";
    private bool _bannerIsError;
    private bool _needsAdmin;
    private bool _busy;
    private bool _capturingHotkey;
    private bool _penVisible;
    private double _penX;
    private double _penY;
    private int _revision;
    private IReadOnlyList<ScreenRect> _screens = [];

    public MainViewModel(AppSettings settings)
    {
        _settings = settings;
        KeepProportions = settings.KeepProportions;
        RunAtStartup = settings.RunAtStartup;
        Orientation = PenOrientationInfo.Parse(settings.Orientation);
        Engine = settings.Engine;
        _hotkeyLabel = settings.HotkeyLabel;
        if (settings.Area is { } area)
            SetAreaText(area);

        PickCommand = new RelayCommand(() => PickRequested?.Invoke(), () => !Busy);
        FullCommand = new RelayCommand(() => FullRequested?.Invoke(), () => !Busy);
        ManualCommand = new RelayCommand(() => ManualRequested?.Invoke(), () => !Busy);
        ElevateCommand = new RelayCommand(() => ElevateRequested?.Invoke(), () => NeedsAdmin && !Busy);
        HotkeyCommand = new RelayCommand(() => CapturingHotkey = true, () => !Busy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? PickRequested;
    public event Action? FullRequested;
    public event Action? ManualRequested;
    public event Action? ElevateRequested;

    public RelayCommand PickCommand { get; }
    public RelayCommand FullCommand { get; }
    public RelayCommand ManualCommand { get; }
    public RelayCommand ElevateCommand { get; }
    public RelayCommand HotkeyCommand { get; }

    public IReadOnlyList<string> Orientations { get; } =
    [
        PenOrientationInfo.Label(PenOrientation.Normal),
        PenOrientationInfo.Label(PenOrientation.Rotate180),
        PenOrientationInfo.Label(PenOrientation.FlipX),
        PenOrientationInfo.Label(PenOrientation.FlipY),
    ];

    public IReadOnlyList<string> Engines { get; } = ["Automatisch", "Wacom-Treiber", "Direkt"];

    public string TabletLine
    {
        get => _tabletLine;
        set => Set(ref _tabletLine, value);
    }

    public string EngineLine
    {
        get => _engineLine;
        set => Set(ref _engineLine, value);
    }

    public string AreaLine
    {
        get => _areaLine;
        set => Set(ref _areaLine, value);
    }

    public string DetailLine
    {
        get => _detailLine;
        set => Set(ref _detailLine, value);
    }

    public bool HasBanner => !string.IsNullOrWhiteSpace(_banner);

    public string Banner
    {
        get => _banner;
        set
        {
            if (Set(ref _banner, value))
                OnPropertyChanged(nameof(HasBanner));
        }
    }

    public bool BannerIsError
    {
        get => _bannerIsError;
        set => Set(ref _bannerIsError, value);
    }

    public bool NeedsAdmin
    {
        get => _needsAdmin;
        set
        {
            if (Set(ref _needsAdmin, value))
                ElevateCommand.RaiseCanExecute();
        }
    }

    public bool Busy
    {
        get => _busy;
        set
        {
            if (Set(ref _busy, value))
            {
                PickCommand.RaiseCanExecute();
                FullCommand.RaiseCanExecute();
                ManualCommand.RaiseCanExecute();
                ElevateCommand.RaiseCanExecute();
                HotkeyCommand.RaiseCanExecute();
            }
        }
    }

    public bool CapturingHotkey
    {
        get => _capturingHotkey;
        set => Set(ref _capturingHotkey, value);
    }

    private string _hotkeyLabel = "Strg + Alt + A";
    private bool _keepProportions;
    private bool _runAtStartup;

    public string HotkeyLabel
    {
        get => _hotkeyLabel;
        set => Set(ref _hotkeyLabel, value);
    }

    public bool KeepProportions
    {
        get => _keepProportions;
        set => Set(ref _keepProportions, value);
    }

    public bool RunAtStartup
    {
        get => _runAtStartup;
        set => Set(ref _runAtStartup, value);
    }

    public PenOrientation Orientation { get; set; }
    public EnginePreference Engine { get; set; }

    public string AreaX { get; set; } = "";
    public string AreaY { get; set; } = "";
    public string AreaWidth { get; set; } = "";
    public string AreaHeight { get; set; } = "";

    public IReadOnlyList<ScreenRect> Screens
    {
        get => _screens;
        set
        {
            _screens = value;
            OnPropertyChanged();
            Touch();
        }
    }

    private ScreenRect? _activeArea;

    public ScreenRect? ActiveArea
    {
        get => _activeArea;
        private set => Set(ref _activeArea, value);
    }

    public bool PenVisible
    {
        get => _penVisible;
        private set => Set(ref _penVisible, value);
    }

    public double PenX
    {
        get => _penX;
        private set => Set(ref _penX, value);
    }

    public double PenY
    {
        get => _penY;
        private set => Set(ref _penY, value);
    }

    public int Revision
    {
        get => _revision;
        private set => Set(ref _revision, value);
    }

    public string SelectedOrientationLabel
    {
        get => PenOrientationInfo.Label(Orientation);
        set
        {
            if (string.IsNullOrEmpty(value))
                return;
            var next = value switch
            {
                "180°" => PenOrientation.Rotate180,
                "Horizontal spiegeln" => PenOrientation.FlipX,
                "Vertikal spiegeln" => PenOrientation.FlipY,
                _ => PenOrientation.Normal,
            };
            if (next == Orientation)
                return;
            Orientation = next;
            OnPropertyChanged();
        }
    }

    public string SelectedEngineLabel
    {
        get => Engine switch
        {
            EnginePreference.Driver => "Wacom-Treiber",
            EnginePreference.Direct => "Direkt",
            _ => "Automatisch",
        };
        set
        {
            Engine = value switch
            {
                "Wacom-Treiber" => EnginePreference.Driver,
                "Direkt" => EnginePreference.Direct,
                _ => EnginePreference.Auto,
            };
            OnPropertyChanged();
        }
    }

    public AppSettings Settings => _settings;

    public void SetScreens(IReadOnlyList<ScreenRect> screens)
    {
        Screens = screens;
    }

    public void Remember(ScreenRect area)
    {
        ActiveArea = area;
        AreaLine = area.ToString();
        SetAreaText(area);
        Touch();
    }

    public void RememberFull()
    {
        ActiveArea = null;
        AreaLine = "Ganzer Bildschirm";
        Touch();
    }

    public bool TryReadManual(out ScreenRect area)
    {
        area = default;
        if (!int.TryParse(AreaX, out int x) || !int.TryParse(AreaY, out int y)
            || !int.TryParse(AreaWidth, out int width) || !int.TryParse(AreaHeight, out int height))
            return false;
        area = new ScreenRect(x, y, width, height);
        return area.IsUsable;
    }

    public void ShowPen(double x, double y)
    {
        PenX = x;
        PenY = y;
        PenVisible = true;
        Touch();
    }

    public void HidePen()
    {
        if (!_penVisible)
            return;
        PenVisible = false;
        Touch();
    }

    public void ShowBanner(string text, bool error, bool needsAdmin)
    {
        Banner = text;
        BannerIsError = error;
        NeedsAdmin = needsAdmin;
    }

    public void NoteHotkey(string label) => HotkeyLabel = label;

    private void SetAreaText(ScreenRect area)
    {
        AreaX = area.X.ToString();
        AreaY = area.Y.ToString();
        AreaWidth = area.Width.ToString();
        AreaHeight = area.Height.ToString();
        OnPropertyChanged(nameof(AreaX));
        OnPropertyChanged(nameof(AreaY));
        OnPropertyChanged(nameof(AreaWidth));
        OnPropertyChanged(nameof(AreaHeight));
    }

    private void Touch()
    {
        Revision++;
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
