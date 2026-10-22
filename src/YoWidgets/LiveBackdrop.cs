using System.Runtime.InteropServices;
using System.Numerics;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using WinRT;
using Forms = System.Windows.Forms;

namespace YoWidgets;

// Live backdrop pixels stay inside the compositor and cannot be read by the app.
// WPF text and input stay in a separate, per-pixel transparent companion HWND.
internal sealed class LiveBackdrop : Forms.NativeWindow, IDisposable
{
    [ComImport, Guid("29E691FA-4567-4DCA-B319-D0F207EB6807"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ICompositorDesktopInterop
    {
        void CreateDesktopWindowTarget(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool topmost, out IntPtr target);
    }
    [StructLayout(LayoutKind.Sequential)] struct QueueOptions { public int Size, ThreadType, Apartment; }
    [DllImport("CoreMessaging.dll")] static extern int CreateDispatcherQueueController(QueueOptions options, out IntPtr controller);
    [DllImport("dwmapi.dll")] static extern int DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)] out bool enabled);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd, int command);
    static Windows.System.DispatcherQueueController? queueController;
    Compositor? compositor;
    DesktopWindowTarget? target;
    SpriteVisual? visual;
    CompositionRoundedRectangleGeometry? geometry;
    CompositionGeometricClip? clip;
    CompositionBackdropBrush? brush;
    CompositionEffectBrush? softened;
    CompositionEffectFactory? blurFactory;
    readonly WidgetPack pack;
    bool interacting;
    bool reading;
    bool disposed;
    public static bool Supported => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) && !SystemParameters.HighContrast && DwmIsCompositionEnabled(out var enabled) == 0 && enabled;
    public LiveBackdrop(string package)
    {
        pack = WidgetPacks.Get(package);
        try
        {
            if (Windows.System.DispatcherQueue.GetForCurrentThread() == null)
            {
                Marshal.ThrowExceptionForHR(CreateDispatcherQueueController(new QueueOptions { Size = Marshal.SizeOf<QueueOptions>(), ThreadType = 2, Apartment = 2 }, out var rawQueue));
                try { queueController = MarshalInterface<Windows.System.DispatcherQueueController>.FromAbi(rawQueue); }
                finally { Marshal.Release(rawQueue); }
            }
            CreateHandle(new Forms.CreateParams { Caption = "YoWidgets · Live backdrop", Style = unchecked((int)0x80000000), ExStyle = 0x082000A0 });
            var enabled = 1;
            Marshal.ThrowExceptionForHR(Native.DwmSetWindowAttribute(Handle, 17, ref enabled, sizeof(int)));
            compositor = new Compositor();
            compositor.As<ICompositorDesktopInterop>().CreateDesktopWindowTarget(Handle, true, out var rawTarget);
            try { target = MarshalInterface<DesktopWindowTarget>.FromAbi(rawTarget); }
            finally { Marshal.Release(rawTarget); }
            brush = compositor.CreateBackdropBrush();
            blurFactory = compositor.CreateEffectFactory(new CompositionBlur(pack.Blur, new CompositionEffectSourceParameter("Background")), ["SoftBlur.BlurAmount"]);
            softened = blurFactory.CreateBrush();
            softened.SetSourceParameter("Background", brush);
            visual = compositor.CreateSpriteVisual();
            visual.Brush = softened;
            visual.Opacity = 1;
            geometry = compositor.CreateRoundedRectangleGeometry();
            clip = compositor.CreateGeometricClip(geometry);
            visual.Clip = clip;
            target.Root = visual;
        }
        catch { Dispose(); throw; }
    }
    public void Sync(WidgetWindow owner)
    {
        if (disposed || !owner.IsVisible || owner.WindowState == WindowState.Minimized) { Hide(); return; }
        var h = Native.Handle(owner);
        var dpi = Math.Max(1, Native.GetDpiForWindow(h) / 96d);
        var bounds = Native.Bounds(owner);
        var inset = 20 * dpi;
        var x = (int)Math.Round(bounds.X + inset);
        var y = (int)Math.Round(bounds.Y + inset);
        var width = Math.Max(1, (int)Math.Round(bounds.Width - inset * 2));
        var height = Math.Max(1, (int)Math.Round(bounds.Height - inset * 2));
        var size = new Vector2(width, height);
        if (visual!.Size != size)
        {
            visual.Size = geometry!.Size = size;
            geometry.CornerRadius = new Vector2((float)(Math.Min(32, Math.Min(width, height) / dpi / 4) * dpi));
        }
        Native.SetWindowPos(Handle, h, x, y, width, height, 0x250);
    }
    public void Interaction(bool active)
    {
        if (disposed || active == interacting) return;
        interacting = active;
        UpdateBlur();
    }
    // Popup content uses the same compositor material without widget optical padding.
    internal void SyncPopup(IntPtr owner,double radius,Rect? region=null)
    {
        if(disposed||!Native.GetWindowRect(owner,out var bounds))return;
        var dpi=Math.Max(1,Native.GetDpiForWindow(owner)/96d);var width=Math.Max(1,bounds.Right-bounds.Left);var height=Math.Max(1,bounds.Bottom-bounds.Top);
        var x=bounds.Left;var y=bounds.Top;if(region is Rect area){x+=(int)Math.Round(area.X*dpi);y+=(int)Math.Round(area.Y*dpi);width=Math.Max(1,(int)Math.Round(area.Width*dpi));height=Math.Max(1,(int)Math.Round(area.Height*dpi));}
        visual!.Size=geometry!.Size=new Vector2(width,height);geometry.CornerRadius=new Vector2((float)(Math.Min(radius,Math.Min(width,height)/dpi/2)*dpi));Native.SetWindowPos(Handle,owner,x,y,width,height,0x250);
    }
    internal void FadePopup(bool visible)
    {
        if(disposed)return;
        if(!SystemParameters.ClientAreaAnimation){visual!.Opacity=visible?1:0;return;}
        using var animation=compositor!.CreateScalarKeyFrameAnimation();animation.InsertKeyFrame(1,visible?1:0);animation.Duration=TimeSpan.FromMilliseconds(150);visual!.StartAnimation("Opacity",animation);
    }
    public void Readability(bool active)
    {
        if(disposed || reading==active)return;
        reading=active; UpdateBlur();
    }
    void UpdateBlur()
    {
        var amount = Math.Max(interacting ? pack.InteractionBlur : pack.Blur,reading ? 8 : 0);
        if (SystemParameters.ClientAreaAnimation)
        {
            using var animation = compositor!.CreateScalarKeyFrameAnimation();
            animation.InsertKeyFrame(1, amount);
            animation.Duration = TimeSpan.FromMilliseconds(260);
            softened!.StartAnimation("SoftBlur.BlurAmount", animation);
        }
        else { softened!.StopAnimation("SoftBlur.BlurAmount"); softened.Properties.InsertScalar("SoftBlur.BlurAmount", amount); }
    }
    public void Hide() { if (!disposed && Handle != IntPtr.Zero) ShowWindow(Handle, 0); }
    protected override void WndProc(ref Forms.Message message)
    {
        if (message.Msg == 0x84) { message.Result = new IntPtr(-1); return; }
        if (message.Msg == 0x14) { message.Result = new IntPtr(1); return; }
        base.WndProc(ref message);
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (target != null) target.Root = null;
        visual?.Dispose(); clip?.Dispose(); geometry?.Dispose(); softened?.Dispose(); blurFactory?.Dispose(); brush?.Dispose();
        target?.Dispose(); compositor?.Dispose();
        if (Handle != IntPtr.Zero) DestroyHandle();
    }
}
