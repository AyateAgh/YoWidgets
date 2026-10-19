namespace YoWidgets.Core;

public static class WallpaperCrop
{
    public static RectD Calculate(int pixelWidth, int pixelHeight, RectD layout, RectD sample)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0 || layout.Width <= 0 || layout.Height <= 0)
            return new(0, 0, 0, 0);
        var left = Math.Clamp((int)Math.Floor((sample.X - layout.X) * (pixelWidth / layout.Width)) - 2, 0, pixelWidth);
        var top = Math.Clamp((int)Math.Floor((sample.Y - layout.Y) * (pixelHeight / layout.Height)) - 2, 0, pixelHeight);
        var right = Math.Clamp((int)Math.Ceiling((sample.X + sample.Width - layout.X) * (pixelWidth / layout.Width)) + 2, 0, pixelWidth);
        var bottom = Math.Clamp((int)Math.Ceiling((sample.Y + sample.Height - layout.Y) * (pixelHeight / layout.Height)) + 2, 0, pixelHeight);
        return new(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }
}
