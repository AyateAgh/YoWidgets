namespace YoWidgets.Core;

public record RectD(double X, double Y, double Width, double Height);
public record PointD(double X, double Y);
public record LyricLine(double Seconds, string Text);
public sealed class WidgetState
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N"); public string Kind { get; set; } = "clock"; public bool Enabled
    {
        get; set;
    }
    public double X { get; set; } = 80; public double Y { get; set; } = 80; public string Size { get; set; } = "M"; public string? ImagePath
    {
        get; set;
    }
    public string Monitor { get; set; } = "";
}
public sealed class AppSettings
{
    public List<WidgetState> Widgets { get; set; } = []; public string City { get; set; } = "Kyiv"; public double GlassStrength { get; set; } = 1; public double Opacity { get; set; } = .2; public double Blur { get; set; } = 12; public bool Light
    {
        get; set;
    }
    public bool Seconds
    {
        get; set;
    }
    public bool Analog
    {
        get; set;
    }
    public bool Fahrenheit
    {
        get; set;
    }
    public bool Hour24 { get; set; } = true; public bool Topmost
    {
        get; set;
    }
    public bool DesktopMode { get; set; } = true; public bool LyricsEnabled
    {
        get; set;
    }
    public bool NotificationsEnabled
    {
        get; set;
    }
    public string AiEndpoint { get; set; } = "https://api.openai.com/v1"; public string AiModel { get; set; } = ""; public string? AiKeyProtected
    {
        get; set;
    }
    public void DisableIntegrations()
    {
        NotificationsEnabled = false;
        LyricsEnabled = false;
    }
    public void Normalize()
    {
        GlassStrength = Finite(GlassStrength, 1, 0, 2);
        Opacity = Finite(Opacity, .2, 0, .85);
        Blur = Finite(Blur, 12, 0, 28);
        City = string.IsNullOrWhiteSpace(City) ? "Kyiv" : City.Trim()[..Math.Min(City.Trim().Length, 120)];
        Widgets ??= [];
        Widgets = Widgets.Where(w => w != null && new[] { "clock", "calendar", "weather", "music", "photo", "focus", "quick", "notifications", "assistant" }.Contains(w.Kind)).Take(24).ToList();
        var ids = new HashSet<string>();
        foreach (var w in Widgets)
        {
            if (!ids.Add(w.Id))
            {
                w.Id = Guid.NewGuid().ToString("N");
                ids.Add(w.Id);
            }
            if (!double.IsFinite(w.X))
                w.X = 80;
            if (!double.IsFinite(w.Y))
                w.Y = 80;
            if (!new[] { "S", "M", "L" }.Contains(w.Size))
                w.Size = "M";
            if (string.IsNullOrEmpty(w.Id))
                w.Id = Guid.NewGuid().ToString("N");
        }
    }
    private static double Finite(double v, double fallback, double min, double max) => double.IsFinite(v) ? Math.Clamp(v, min, max) : fallback;
}
