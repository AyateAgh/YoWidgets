using System.Runtime.InteropServices;
using Windows.UI.Composition;
using WinRT;
using YoWidgets;

internal static class Program
{
    [StructLayout(LayoutKind.Sequential)] struct Options { public int Size, Thread, Apartment; }
    [DllImport("CoreMessaging.dll")] static extern int CreateDispatcherQueueController(Options options, out IntPtr controller);
    [STAThread]
    static void Main()
    {
        Marshal.ThrowExceptionForHR(CreateDispatcherQueueController(new Options { Size = Marshal.SizeOf<Options>(), Thread = 2, Apartment = 2 }, out var pointer));
        var queue = MarshalInterface<Windows.System.DispatcherQueueController>.FromAbi(pointer);
        Marshal.Release(pointer);
        using var compositor = new Compositor();
        using var factory = compositor.CreateEffectFactory(new CompositionBlur(3, new CompositionEffectSourceParameter("Background")), ["SoftBlur.BlurAmount"]);
        Console.WriteLine("PASS: native effect properties, boxed values, source and animatable mapping accepted");
        using var background = compositor.CreateBackdropBrush();
        for (int i = 0; i < 100; i++)
        {
            using var brush = factory.CreateBrush();
            brush.SetSourceParameter("Background", background);
            brush.Properties.InsertScalar("SoftBlur.BlurAmount", 3);
            using var animation = compositor.CreateScalarKeyFrameAnimation();
            animation.InsertKeyFrame(1, 4.5f);
            animation.Duration = TimeSpan.FromMilliseconds(120);
            brush.StartAnimation("SoftBlur.BlurAmount", animation);
            brush.StopAnimation("SoftBlur.BlurAmount");
        }
        Console.WriteLine("PASS: 100 native source-bind / property / animation / disposal cycles");
        GC.KeepAlive(queue); // The process owns the STA queue until exit; no dispatcher loop is running.
    }
}
