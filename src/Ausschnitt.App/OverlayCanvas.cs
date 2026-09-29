using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Ausschnitt.Core;

namespace Ausschnitt;

public sealed class OverlayCanvas : Control
{
    public PixelPoint Origin { get; set; }
    public double Scale { get; set; } = 1;
    public ScreenRect? Draft { get; set; }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(160, 10, 9, 8)), bounds);
        if (Draft is not { IsUsable: true } rect || Scale <= 0)
            return;

        var box = new Rect(
            (rect.X - Origin.X) / Scale,
            (rect.Y - Origin.Y) / Scale,
            rect.Width / Scale,
            rect.Height / Scale);
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(50, 226, 61, 43)), box);
        context.DrawRectangle(new Pen(Brushes.White, 1.5), box);
        context.DrawRectangle(new Pen(new SolidColorBrush(Color.Parse("#E23D2B")), 3), box);

        string label = $"{rect.Width} × {rect.Height}";
        var text = new FormattedText(
            label,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter"),
            18,
            Brushes.White);
        var spot = new Point(box.X + 12, Math.Max(8, box.Y - 32));
        context.FillRectangle(new SolidColorBrush(Color.Parse("#E23D2B")), new Rect(spot.X - 8, spot.Y - 4, text.Width + 16, text.Height + 8));
        context.DrawText(text, spot);
    }
}
