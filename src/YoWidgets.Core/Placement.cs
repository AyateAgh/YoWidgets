namespace YoWidgets.Core;

public static class Placement
{
    public static PointD CenterGrid(double width, double height, RectD work, int index, int count, double cellWidth, double cellHeight)
    {
        var columns = Math.Clamp((int)(work.Width / (cellWidth + 24)), 1, Math.Min(3, Math.Max(1, count)));
        var rows = (Math.Max(1, count) + columns - 1) / columns;
        var x = work.X + (work.Width - (columns * cellWidth + (columns - 1) * 24)) / 2 + index % columns * (cellWidth + 24) + (cellWidth - width) / 2;
        var y = work.Y + (work.Height - (rows * cellHeight + (rows - 1) * 24)) / 2 + index / columns * (cellHeight + 24) + (cellHeight - height) / 2;
        return Clamp(x, y, width, height, work);
    }
    public static PointD Center(double width, double height, RectD work, int index = 0) => Clamp(work.X + (work.Width - width) / 2 + index % 4 * 28, work.Y + (work.Height - height) / 2 + index % 4 * 28, width, height, work);
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
