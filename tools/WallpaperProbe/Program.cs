namespace YoWidgets;

// Runs the production provider on a real WPF dispatcher without changing wallpaper.
internal static class Program
{
    [STAThread]
    static int Main()
    {
        var result = 1;
        var application = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        application.Startup += async (_, _) =>
        {
            try
            {
                using var provider = new WallpaperProvider();
                var notifications = 0;
                provider.Changed += () => notifications++;
                var sample = new RectD(100, 100, 320, 180);
                provider.GetBrush(sample);
                provider.GetBrush(sample, moving: true);
                var decoded = provider.DecodeCount;
                if (decoded != 2 || provider.CachedPixelBytes == 0) throw new Exception("Current wallpaper crops were not cached");
                provider.RequestRefresh(); provider.RequestRefresh(); provider.RequestRefresh();
                await Task.Delay(900);
                if (notifications != 1 || provider.CachedPixelBytes != 0) throw new Exception("Burst invalidation failed to clear both caches exactly once");
                Console.WriteLine("PASS burst shell refresh clears normal and motion caches once");
                provider.GetBrush(sample);
                provider.GetBrush(sample, moving: true);
                if (provider.DecodeCount != decoded + 2) throw new Exception("Old wallpaper images were reused after refresh");
                Console.WriteLine("PASS both background paths decode fresh snapshots after invalidation");
                provider.RequestRefresh();
                provider.Dispose();
                await Task.Delay(500);
                if (notifications != 1) throw new Exception("Disposed provider delivered a pending callback");
                Console.WriteLine("PASS disposal cancels pending shell refresh and event handlers");
                result = 0;
            }
            catch (Exception e) { Console.WriteLine("FAIL " + e); }
            finally { application.Shutdown(); }
        };
        application.Run();
        return result;
    }
}
internal static class App
{
    public static void Log(string area, Exception e) => Console.WriteLine(area + ": " + e.Message);
    public static void Trace(string area, string text) => Console.WriteLine(area + ": " + text);
}
internal static class Native
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal struct RECT { public int Left, Top, Right, Bottom; }
}
