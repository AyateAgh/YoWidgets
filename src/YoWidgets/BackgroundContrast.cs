using System.Runtime.InteropServices;
namespace YoWidgets;

// Four color values from transparent window margins, never an image or recording.
// One shared background job; no synchronous screen read runs in a drag/render path.
internal sealed class BackgroundContrast : IDisposable
{
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("gdi32.dll")] static extern uint GetPixel(IntPtr dc, int x, int y);
    readonly Func<WidgetWindow[]> getWindows;
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(2) };
    bool busy, disposed;
    int generation;
    public BackgroundContrast(Func<WidgetWindow[]> getWindows) { this.getWindows = getWindows; timer.Tick += Tick; }
    public void Update(bool active)
    {
        generation++;
        if (active && !disposed) { timer.Start(); Tick(null, EventArgs.Empty); } else timer.Stop();
    }
    async void Tick(object? sender, EventArgs args)
    {
        if (busy || disposed || !timer.IsEnabled) return;
        var stamp = generation;
        var windows = getWindows().Where(w => w.IsVisible && w.WindowState != WindowState.Minimized).Select(w => (Window: w, Bounds: Native.Bounds(w))).ToArray();
        if (windows.Length == 0) return;
        busy = true;
        try
        {
            var readings = await Task.Run(() =>
            {
                var result = new List<(WidgetWindow Window, RectD Bounds, double Luminance)>();
                var dc = GetDC(IntPtr.Zero);
                if (dc == IntPtr.Zero) return result;
                try
                {
                    foreach (var item in windows)
                    {
                        var b = item.Bounds;
                        var samples = new List<double>(4);
                        foreach (var point in new[] { (b.X + 4, b.Y + 4), (b.X + b.Width - 5, b.Y + 4), (b.X + 4, b.Y + b.Height - 5), (b.X + b.Width - 5, b.Y + b.Height - 5) })
                        {
                            var pixel = GetPixel(dc, (int)point.Item1, (int)point.Item2);
                            if (pixel == uint.MaxValue) continue;
                            samples.Add(ContrastPolicy.Luminance((byte)pixel, (byte)(pixel >> 8), (byte)(pixel >> 16)));
                        }
                        if (samples.Count > 0) result.Add((item.Window, b, ContrastPolicy.MergeMargins(samples)));
                    }
                }
                finally { ReleaseDC(IntPtr.Zero, dc); }
                return result;
            });
            if (disposed || stamp != generation || !timer.IsEnabled) return;
            var current = getWindows();
            foreach (var reading in readings)
                if (current.Contains(reading.Window) && reading.Window.IsVisible && Native.Bounds(reading.Window) == reading.Bounds)
                    reading.Window.AdaptInk(reading.Luminance);
        }
        catch (Exception e) { App.Log("background-contrast", e); }
        finally { busy = false; }
    }
    public void Dispose() { if (disposed) return; disposed = true; generation++; timer.Stop(); timer.Tick -= Tick; }
}
