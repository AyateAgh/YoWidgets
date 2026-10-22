using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Media.Animation;
namespace YoWidgets;

// Resizing the browser on every animation frame causes repeated layout and raster work.
// Animate a single local bitmap instead, and commit the real browser's bounds once.
internal sealed class ShellSnapshotTransition(Window owner,Func<WebView2?> getView)
{
    string readyToken="";TaskCompletionSource? rendered;Task? captureInFlight;
    internal bool Transparent {get;set;}
    internal void Ready(string token){if(token==readyToken)rendered?.TrySetResult();}
    internal async Task<bool> Run(RectD from,RectD to,Action commit,CancellationToken token)
    {
        var elapsed=System.Diagnostics.Stopwatch.StartNew();
        var view=getView();if(view?.CoreWebView2 is not {} core)return false;
        if(captureInFlight is {IsCompleted:false})return false;
        var stream=new MemoryStream();Task capture;
        try{capture=captureInFlight=core.CapturePreviewAsync(Transparent?CoreWebView2CapturePreviewImageFormat.Png:CoreWebView2CapturePreviewImageFormat.Jpeg,stream);}
        catch(Exception e)when(e is COMException or InvalidOperationException){stream.Dispose();return false;}
        try{await capture.WaitAsync(TimeSpan.FromMilliseconds(500),token);}
        catch(Exception e)when(e is TimeoutException or OperationCanceledException or COMException or InvalidOperationException){_=capture.ContinueWith(done=>{_=done.Exception;stream.Dispose();},TaskScheduler.Default);App.Trace("shell-transition-capture",e.GetType().Name);return false;}
        var captureMs=elapsed.Elapsed.TotalMilliseconds;
        BitmapImage bitmap;
        using(stream){stream.Position=0;bitmap=new();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;if(from.Width>2048)bitmap.DecodePixelWidth=2048;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();}
        token.ThrowIfCancellationRequested();
        using var visual=new ShellSnapshotWindow(owner,bitmap,from,to,view.Margin);
        var ownerHandle=Native.Handle(owner);var cloaked=false;
        async Task LiveFrame()
        {
            readyToken=Guid.NewGuid().ToString("N");var frame=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);rendered=frame;
            var json=JsonSerializer.Serialize(readyToken);
            try{
                await core.ExecuteScriptAsync($"requestAnimationFrame(()=>requestAnimationFrame(()=>window.chrome.webview.postMessage({{method:'ui.transition.ready',args:{{token:{json}}}}})))").WaitAsync(TimeSpan.FromMilliseconds(200),token);
                await frame.Task.WaitAsync(TimeSpan.FromMilliseconds(160),token);
            }catch(TimeoutException){}
        }
        try
        {
            await visual.Present(token);
            var cloak=1;cloaked=Native.DwmSetWindowAttribute(ownerHandle,13,ref cloak,4)==0;
            if(!cloaked)view.Visibility=Visibility.Hidden;
            commit();owner.UpdateLayout();
            var moving=visual.Move(token);
            // Chromium can throttle rAF while its HWND is cloaked. Start preparation
            // now, but never hold the moving image waiting for a hidden browser.
            var live=cloaked?LiveFrame():Task.CompletedTask;
            _=live.ContinueWith(done=>{_=done.Exception;},TaskScheduler.Default);
            await moving;
            token.ThrowIfCancellationRequested();
            if(cloaked){var uncloak=0;Native.DwmSetWindowAttribute(ownerHandle,13,ref uncloak,4);cloaked=false;}
            else{view.Visibility=Visibility.Visible;owner.UpdateLayout();live=LiveFrame();_=live.ContinueWith(done=>{_=done.Exception;},TaskScheduler.Default);}
            try{await live.WaitAsync(TimeSpan.FromMilliseconds(80),token);}catch(TimeoutException){}
            await visual.Reveal(token);
            App.Trace("shell-transition",$"snapshot; captureMs={captureMs:F1}; totalMs={elapsed.Elapsed.TotalMilliseconds:F1}; texture={bitmap.PixelWidth}x{bitmap.PixelHeight}");
            return true;
        }
        finally
        {
            if(cloaked){var cloak=0;Native.DwmSetWindowAttribute(ownerHandle,13,ref cloak,4);}
            readyToken="";rendered?.TrySetCanceled();rendered=null;
            if(ReferenceEquals(getView(),view)&&owner.IsVisible&&owner.WindowState!=WindowState.Minimized)view.Visibility=Visibility.Visible;
        }
    }
}

// The overlay HWND stays fixed. Only WPF render transforms and opacity are animated.
internal sealed class ShellSnapshotWindow:Window,IDisposable
{
    readonly Image image;readonly ScaleTransform scale=new();readonly TranslateTransform offset=new();
    readonly RectD union,from,to;double dpi;bool closed;
    internal ShellSnapshotWindow(Window owner,BitmapSource bitmap,RectD from,RectD to,Thickness inset=default)
    {
        union=new(Math.Min(from.X,to.X),Math.Min(from.Y,to.Y),Math.Max(from.X+from.Width,to.X+to.Width)-Math.Min(from.X,to.X),Math.Max(from.Y+from.Height,to.Y+to.Height)-Math.Min(from.Y,to.Y));
        var initialScale=Math.Max(96,Native.GetDpiForWindow(Native.Handle(owner)))/96d;
        RectD Inset(RectD rect)=>new(rect.X+inset.Left*initialScale,rect.Y+inset.Top*initialScale,Math.Max(1,rect.Width-(inset.Left+inset.Right)*initialScale),Math.Max(1,rect.Height-(inset.Top+inset.Bottom)*initialScale));
        this.from=Inset(from);this.to=Inset(to);Width=union.Width/initialScale;Height=union.Height/initialScale;Left=union.X/initialScale;Top=union.Y/initialScale;Title="YoWidgets transition";Topmost=true;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;IsHitTestVisible=false;WindowStartupLocation=WindowStartupLocation.Manual;
        image=new(){Source=bitmap,Stretch=Stretch.Fill,IsHitTestVisible=false,CacheMode=new BitmapCache()};RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.LowQuality);var transforms=new TransformGroup();transforms.Children.Add(scale);transforms.Children.Add(offset);image.RenderTransform=transforms;
        var canvas=new Canvas{ClipToBounds=true,IsHitTestVisible=false};canvas.Children.Add(image);Content=canvas;
        SourceInitialized+=(_,_)=>{
            var handle=Native.Handle(this);
            Native.SetStyle(handle,-20,new IntPtr(Native.GetStyle(handle,-20).ToInt64()|0x20|0x8000000|0x80));
            var none=unchecked((int)0xfffffffe);Native.DwmSetWindowAttribute(handle,34,ref none,4);
            var disabled=1;Native.DwmSetWindowAttribute(handle,3,ref disabled,4);Native.DwmSetWindowAttribute(handle,33,ref disabled,4);
            Native.SetWindowPos(handle,IntPtr.Zero,(int)union.X,(int)union.Y,(int)Math.Ceiling(union.Width),(int)Math.Ceiling(union.Height),0x14);
            dpi=Math.Max(96,Native.GetDpiForWindow(handle))/96d;
            image.Width=this.from.Width/dpi;image.Height=this.from.Height/dpi;Canvas.SetLeft(image,(this.from.X-union.X)/dpi);Canvas.SetTop(image,(this.from.Y-union.Y)/dpi);
            image.Clip=new RectangleGeometry(new Rect(0,0,image.Width,image.Height),14,14);
        };
    }
    internal async Task Present(CancellationToken token)
    {
        Show();UpdateLayout();
        var frame=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler? rendered=null;rendered=(_,_)=>frame.TrySetResult();CompositionTarget.Rendering+=rendered;
        try{await frame.Task.WaitAsync(TimeSpan.FromMilliseconds(120),token);}finally{CompositionTarget.Rendering-=rendered;}
    }
    internal Task Move(CancellationToken token)
    {
        var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        DoubleAnimation Animate(double start,double end)=>new(start,end,TimeSpan.FromMilliseconds(250)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseInOut},FillBehavior=FillBehavior.HoldEnd};
        var x=Animate(1,to.Width/from.Width);x.Completed+=(_,_)=>done.TrySetResult();
        scale.BeginAnimation(ScaleTransform.ScaleXProperty,x);scale.BeginAnimation(ScaleTransform.ScaleYProperty,Animate(1,to.Height/from.Height));
        offset.BeginAnimation(TranslateTransform.XProperty,Animate(0,(to.X-from.X)/dpi));offset.BeginAnimation(TranslateTransform.YProperty,Animate(0,(to.Y-from.Y)/dpi));
        return done.Task.WaitAsync(TimeSpan.FromMilliseconds(650),token);
    }
    internal Task Reveal(CancellationToken token)
    {
        var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var fade=new DoubleAnimation(1,0,TimeSpan.FromMilliseconds(100)){EasingFunction=new QuadraticEase{EasingMode=EasingMode.EaseOut}};fade.Completed+=(_,_)=>done.TrySetResult();BeginAnimation(OpacityProperty,fade);return done.Task.WaitAsync(TimeSpan.FromMilliseconds(300),token);
    }
    public void Dispose(){if(closed)return;closed=true;scale.BeginAnimation(ScaleTransform.ScaleXProperty,null);scale.BeginAnimation(ScaleTransform.ScaleYProperty,null);offset.BeginAnimation(TranslateTransform.XProperty,null);offset.BeginAnimation(TranslateTransform.YProperty,null);BeginAnimation(OpacityProperty,null);image.Source=null;image.CacheMode=null;Content=null;Close();}
}
