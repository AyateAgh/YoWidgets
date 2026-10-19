namespace YoWidgets.Core;

public static class Placement
{
    public static PointD Clamp(double x, double y, double w, double h, RectD a) => new(Math.Clamp(double.IsFinite(x) ? x : a.X, a.X, Math.Max(a.X, a.X + a.Width - w)), Math.Clamp(double.IsFinite(y) ? y : a.Y, a.Y, Math.Max(a.Y, a.Y + a.Height - h)));
}
public static class WallpaperLayout
{
    public static RectD Calculate(double iw, double ih, RectD a, string mode)
    {
        if (iw <= 0 || ih <= 0)
            return a;
        if (mode == "Stretch")
            return a;
        var scale = mode == "Center" || mode == "Tile" ? 1 : mode == "Fit" ? Math.Min(a.Width / iw, a.Height / ih) : Math.Max(a.Width / iw, a.Height / ih);
        var w = iw * scale;
        var h = ih * scale;
        return new(a.X + (a.Width - w) / 2, a.Y + (a.Height - h) / 2, w, h);
    }
}
