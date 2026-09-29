using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Ausschnitt.Core;

namespace Ausschnitt.Controls;

public sealed class MonitorPreview : Control
{
    public static readonly StyledProperty<IReadOnlyList<ScreenRect>?> ScreensProperty =
        AvaloniaProperty.Register<MonitorPreview, IReadOnlyList<ScreenRect>?>(nameof(Screens));

    public static readonly StyledProperty<ScreenRect?> AreaProperty =
        AvaloniaProperty.Register<MonitorPreview, ScreenRect?>(nameof(Area));

    public static readonly StyledProperty<bool> PenVisibleProperty =
        AvaloniaProperty.Register<MonitorPreview, bool>(nameof(PenVisible));

    public static readonly StyledProperty<double> PenXProperty =
        AvaloniaProperty.Register<MonitorPreview, double>(nameof(PenX));

    public static readonly StyledProperty<double> PenYProperty =
        AvaloniaProperty.Register<MonitorPreview, double>(nameof(PenY));

    public static readonly StyledProperty<int> RevisionProperty =
        AvaloniaProperty.Register<MonitorPreview, int>(nameof(Revision));

    private static readonly IBrush Panel = new SolidColorBrush(Color.Parse("#171512"));
    private static readonly IBrush ScreenFill = new SolidColorBrush(Color.Parse("#2A2622"));
    private static readonly IBrush ScreenEdge = new SolidColorBrush(Color.Parse("#8A7D72"));
    private static readonly IBrush AreaFill = new SolidColorBrush(Color.Parse("#55E23D2B"));
    private static readonly IBrush AreaEdge = new SolidColorBrush(Color.Parse("#E23D2B"));
    private static readonly IBrush PenBrush = new SolidColorBrush(Color.Parse("#F6F1EA"));
    private static readonly IBrush LabelBrush = new SolidColorBrush(Color.Parse("#D9D0C6"));

    static MonitorPreview()
    {
        AffectsRender<MonitorPreview>(ScreensProperty, AreaProperty, PenVisibleProperty, PenXProperty, PenYProperty, RevisionProperty);
    }

    public IReadOnlyList<ScreenRect>? Screens
    {
        get => GetValue(ScreensProperty);
        set => SetValue(ScreensProperty, value);
    }

    public ScreenRect? Area
    {
        get => GetValue(AreaProperty);
        set => SetValue(AreaProperty, value);
    }

    public bool PenVisible
    {
        get => GetValue(PenVisibleProperty);
        set => SetValue(PenVisibleProperty, value);
    }

    public double PenX
    {
        get => GetValue(PenXProperty);
        set => SetValue(PenXProperty, value);
    }

    public double PenY
    {
        get => GetValue(PenYProperty);
        set => SetValue(PenYProperty, value);
    }

    public int Revision
    {
        get => GetValue(RevisionProperty);
        set => SetValue(RevisionProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(Panel, bounds);
        var screens = Screens;
        if (screens is null || screens.Count == 0)
            return;

        var desktop = ScreenRect.Union(screens);
        if (desktop.Width <= 0 || desktop.Height <= 0)
            return;

        const double pad = 16;
        double scale = Math.Min(
            (bounds.Width - pad * 2) / desktop.Width,
            (bounds.Height - pad * 2) / desktop.Height);
        if (scale <= 0)
            return;
        double ox = pad + ((bounds.Width - pad * 2) - desktop.Width * scale) / 2;
        double oy = pad + ((bounds.Height - pad * 2) - desktop.Height * scale) / 2;

        Rect Map(ScreenRect rect) => new(
            ox + (rect.X - desktop.X) * scale,
            oy + (rect.Y - desktop.Y) * scale,
            rect.Width * scale,
            rect.Height * scale);

        for (int i = 0; i < screens.Count; i++)
        {
            Rect screen = Map(screens[i]);
            context.FillRectangle(ScreenFill, screen);
            context.DrawRectangle(new Pen(ScreenEdge, 1), screen);
            context.DrawText(new FormattedText(
                (i + 1).ToString(),
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter"),
                13,
                LabelBrush), screen.Position + new Point(8, 6));
        }

        if (Area is { IsUsable: true } area)
        {
            Rect box = Map(area);
            context.FillRectangle(AreaFill, box);
            context.DrawRectangle(new Pen(AreaEdge, 2), box);
        }

        if (PenVisible)
        {
            double x = ox + (PenX - desktop.X) * scale;
            double y = oy + (PenY - desktop.Y) * scale;
            context.DrawEllipse(PenBrush, new Pen(AreaEdge, 2), new Point(x, y), 5, 5);
        }
    }
}
