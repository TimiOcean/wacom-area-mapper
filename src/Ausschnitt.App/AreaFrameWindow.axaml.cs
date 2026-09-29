using Avalonia;
using Avalonia.Controls;
using Ausschnitt.Core;
using Ausschnitt.Windows;

namespace Ausschnitt;

public partial class AreaFrameWindow : Window
{
    public AreaFrameWindow()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            WindowsSession.MakeClickThrough(handle);
        };
    }

    public void Place(ScreenRect rect)
    {
        double scale = 1;
        var screens = Screens?.All;
        if (screens is not null)
        {
            foreach (var screen in screens)
            {
                if (rect.X >= screen.Bounds.X && rect.X < screen.Bounds.Right
                    && rect.Y >= screen.Bounds.Y && rect.Y < screen.Bounds.Bottom)
                {
                    scale = screen.Scaling <= 0 ? 1 : screen.Scaling;
                    break;
                }
            }
        }

        Position = new PixelPoint(rect.X, rect.Y);
        Width = Math.Max(8, rect.Width / scale);
        Height = Math.Max(8, rect.Height / scale);
        if (!IsVisible)
            Show();
    }
}
