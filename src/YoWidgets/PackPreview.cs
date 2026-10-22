using System.Windows.Shapes;
namespace YoWidgets;

// Small authored style previews: no system wallpaper, capture or image decoding.
internal static class PackPreview
{
    public static Brush Backdrop() => new LinearGradientBrush(new GradientStopCollection
    {
        new(Color.FromRgb(78, 85, 91), 0),
        new(Color.FromRgb(151, 143, 135), .48),
        new(Color.FromRgb(57, 70, 74), 1)
    }, new Point(0, 0), new Point(1, 1));
    public static FrameworkElement Create(WidgetPack pack)
    {
        var scene = new Grid { Width = 300, Height = 220, Background = Backdrop(), Clip = new RectangleGeometry(new Rect(0, 0, 300, 220), 22, 22) };
        var glow = new Ellipse { Width = 190, Height = 190, Fill = new SolidColorBrush(Color.FromArgb(65, 213, 174, 143)), Margin = new Thickness(-140, -80, 0, 0), Effect = new System.Windows.Media.Effects.BlurEffect { Radius = 22 } };
        scene.Children.Add(glow);
        var card = new Grid { Width = 252, Height = 148, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var surface = new GlassSurface();
        surface.Update(252, 148, pack.Id);
        card.Children.Add(surface);
        var date = WidgetVisuals.Label(DateTime.Now.ToString("ddd, d MMMM"), 10, true);
        date.HorizontalAlignment = HorizontalAlignment.Center;
        var time = WidgetVisuals.Label(DateTime.Now.ToString("HH:mm"), 43, true);
        time.FontFamily = Ui.DisplayFont;
        time.HorizontalAlignment = HorizontalAlignment.Center;
        var labels = Ui.Stack(date, Ui.Gap(4), time);
        labels.VerticalAlignment = VerticalAlignment.Center;
        card.Children.Add(labels);
        scene.Children.Add(card);
        var host = new Viewbox { Child = scene, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Height = 220 };
        System.Windows.Automation.AutomationProperties.SetName(host, pack.Name + " style preview");
        host.ToolTip = "Style preview. Desktop widgets use the current background behind them.";
        return host;
    }
}
