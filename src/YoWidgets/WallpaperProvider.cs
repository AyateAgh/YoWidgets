using System.Runtime.InteropServices;
namespace YoWidgets;

internal sealed class WallpaperProvider : IDisposable
{
    readonly IDesktopWallpaper? desktop;
    // Only detached, widget-sized raster snapshots are retained, never full wallpaper frames.
    readonly Dictionary<string, BitmapSource> cache = [];
    readonly Dictionary<string, BitmapSource> movingCache = [];
    public int DecodeCount { get; private set; }
    public long CachedPixelBytes => cache.Values.Concat(movingCache.Values).Sum(b => (long)b.PixelWidth * b.PixelHeight * 4);
    public WallpaperProvider()
    {
        try
        {
            desktop = (IDesktopWallpaper)Activator.CreateInstance(Type.GetTypeFromCLSID(new("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD"))!)!;
        }
        catch (Exception e) { App.Log("wallpaper", e); }
    }
    public void ClearCache(bool all = false) { cache.Clear(); if(all) movingCache.Clear(); }
    public Brush GetBrush(RectD sample, bool checker = false, bool moving = false)
    {
        if (checker)
            return Checker(sample);
        try
        {
            if (desktop == null)
                return Brushes.Transparent;
            desktop.GetMonitorDevicePathCount(out var count);
            string? path = null;
            RectD screen = new(0, 0, 1920, 1080);
            var best = -1d;
            for (uint i = 0; i < count; i++)
            {
                desktop.GetMonitorDevicePathAt(i, out var id);
                desktop.GetMonitorRECT(id, out var r);
                var area = Math.Max(0, Math.Min(sample.X + sample.Width, r.Right) - Math.Max(sample.X, r.Left)) * Math.Max(0, Math.Min(sample.Y + sample.Height, r.Bottom) - Math.Max(sample.Y, r.Top));
                if (area <= best)
                    continue;
                best = area;
                desktop.GetWallpaper(id, out path);
                screen = new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
            }
            desktop.GetBackgroundColor(out var c);
            var background = Color.FromRgb((byte)c, (byte)(c >> 8), (byte)(c >> 16));
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return new SolidColorBrush(background);
            desktop.GetPosition(out var pos);
            var mode = new[] { "Center", "Tile", "Stretch", "Fit", "Fill", "Span" }[Math.Clamp(pos, 0, 5)];
            if (mode == "Span")
            {
                var a = System.Windows.Forms.SystemInformation.VirtualScreen;
                screen = new(a.X, a.Y, a.Width, a.Height);
            }
            var key = $"{path}|{File.GetLastWriteTimeUtc(path).Ticks}|{sample}|{screen}|{mode}|{c}|{moving}";
            var images = moving ? movingCache : cache;
            if (!images.TryGetValue(key, out var image))
            {
                DecodeCount++;
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.OnDemand);
                var source = decoder.Frames[0];
                var layout = WallpaperLayout.Calculate(source.PixelWidth, source.PixelHeight, screen, mode);
                var visual = new DrawingVisual();
                var scale = Math.Min(1, Math.Min((moving ? 1600d : 900d) / sample.Width, (moving ? 1000d : 650d) / sample.Height));
                var width = Math.Max(1, (int)Math.Ceiling(sample.Width * scale));
                var height = Math.Max(1, (int)Math.Ceiling(sample.Height * scale));
                using (var dc = visual.RenderOpen())
                {
                    dc.PushTransform(new ScaleTransform(width / sample.Width, height / sample.Height));
                    dc.DrawRectangle(new SolidColorBrush(background), null, new Rect(0, 0, sample.Width, sample.Height));
                    if (mode == "Tile")
                    {
                        var tiled = new ImageBrush(source) { TileMode = TileMode.Tile, ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(-sample.X, -sample.Y, source.PixelWidth, source.PixelHeight), Stretch = Stretch.Fill };
                        dc.DrawRectangle(tiled, null, new Rect(0, 0, sample.Width, sample.Height));
                    }
                    else
                    {
                        // Convert this widget's screen rectangle to a source-pixel crop before rendering.
                        var crop = WallpaperCrop.Calculate(source.PixelWidth, source.PixelHeight, layout, sample);
                        if (crop.Width > 0 && crop.Height > 0)
                        {
                            var clipped = new CroppedBitmap(source, new Int32Rect((int)crop.X, (int)crop.Y, (int)crop.Width, (int)crop.Height));
                            var sx = layout.Width / source.PixelWidth;
                            var sy = layout.Height / source.PixelHeight;
                            dc.DrawImage(clipped, new Rect(layout.X + crop.X * sx - sample.X, layout.Y + crop.Y * sy - sample.Y, crop.Width * sx, crop.Height * sy));
                        }
                    }
                    dc.Pop();
                }
                var snapshot = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                snapshot.Render(visual);
                snapshot.Freeze();
                image = snapshot;
                if (images.Count >= (moving ? 2 : 8)) images.Clear();
                images[key] = image;
                // No decoder, stream, DrawingVisual or CroppedBitmap escapes this scope.
            }
            var brush = new ImageBrush(image) { Stretch = Stretch.Fill };
            brush.Freeze();
            return brush;
        }
        catch (Exception e) { App.Log("wallpaper-crop", e); return Brushes.Transparent; }
    }
    static Brush Checker(RectD sample)
    {
        var d = new DrawingGroup();
        d.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(38, 71, 94)), null, new RectangleGeometry(new Rect(0, 0, 48, 48))));
        d.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(205, 229, 222)), null, new RectangleGeometry(new Rect(0, 0, 24, 24))));
        d.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(205, 229, 222)), null, new RectangleGeometry(new Rect(24, 24, 24, 24))));
        return new DrawingBrush(d) { TileMode = TileMode.Tile, ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(-sample.X % 48, -sample.Y % 48, 48, 48) };
    }
    public void Dispose()
    {
        ClearCache(all:true);
        if (desktop != null)
            Marshal.ReleaseComObject(desktop);
    }
}
[ComImport, Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDesktopWallpaper
{
    [PreserveSig] int SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? id, [MarshalAs(UnmanagedType.LPWStr)] string path);
    [PreserveSig] int GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? id, [MarshalAs(UnmanagedType.LPWStr)] out string path);
    [PreserveSig] int GetMonitorDevicePathAt(uint index, [MarshalAs(UnmanagedType.LPWStr)] out string id);
    [PreserveSig] int GetMonitorDevicePathCount(out uint count);
    [PreserveSig] int GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string id, out Native.RECT rect);
    [PreserveSig] int SetBackgroundColor(uint color);
    [PreserveSig] int GetBackgroundColor(out uint color);
    [PreserveSig] int SetPosition(int position);
    [PreserveSig] int GetPosition(out int position);
}
