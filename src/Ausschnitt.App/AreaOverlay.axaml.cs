using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Ausschnitt.Core;
using Ausschnitt.Windows;

namespace Ausschnitt;

public partial class AreaOverlay : Window
{
    private readonly TaskCompletionSource<ScreenRect?> _done = new();
    private int _anchorX;
    private int _anchorY;
    private bool _dragging;
    private bool _shift;
    private bool _accept;
    private ScreenRect? _draft;

    public AreaOverlay()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            FitToDesktop();
            Focus();
        };
        Closed += (_, _) => _done.TrySetResult(_accept ? _draft : null);
    }

    public double TabletAspect { get; set; } = 1.6;

    public Task<ScreenRect?> PickAsync()
    {
        Show();
        Activate();
        return _done.Task;
    }

    private void FitToDesktop()
    {
        var screens = Screens?.All;
        if (screens is null || screens.Count == 0)
            return;

        int left = screens.Min(screen => screen.Bounds.X);
        int top = screens.Min(screen => screen.Bounds.Y);
        int right = screens.Max(screen => screen.Bounds.Right);
        int bottom = screens.Max(screen => screen.Bounds.Bottom);
        double scale = screens[0].Scaling;
        if (scale <= 0)
            scale = 1;

        Position = new PixelPoint(left, top);
        Width = (right - left) / scale;
        Height = (bottom - top) / scale;
        Surface.Origin = Position;
        Surface.Scale = scale;
        Surface.InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        ( _anchorX, _anchorY) = ReadPointer(e);
        _dragging = true;
        _draft = null;
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging)
            return;
        (int x, int y) = ReadPointer(e);
        _draft = _shift
            ? ScreenRect.ConstrainAspect(_anchorX, _anchorY, x, y, TabletAspect)
            : ScreenRect.FromPoints(_anchorX, _anchorY, x, y);
        Surface.Draft = _draft;
        Surface.InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging)
            return;
        _dragging = false;
        e.Pointer.Capture(null);
        if (_draft is { IsUsable: true })
        {
            _accept = true;
            Close();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.LeftShift or Key.RightShift)
            _shift = true;
        if (e.Key == Key.Escape)
        {
            _accept = false;
            Close();
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key is Key.LeftShift or Key.RightShift)
            _shift = false;
    }

    private (int X, int Y) ReadPointer(PointerEventArgs e)
    {
        if (VirtualScreen.TryGetCursor(out int x, out int y))
            return (x, y);
        var local = e.GetPosition(this);
        var origin = VisualExtensions.PointToScreen(this, new Point(0, 0));
        return (
            origin.X + (int)Math.Round(local.X * RenderScaling),
            origin.Y + (int)Math.Round(local.Y * RenderScaling));
    }
}
