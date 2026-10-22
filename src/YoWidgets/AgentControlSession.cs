using System.Runtime.InteropServices;
using System.Windows.Interop;
namespace YoWidgets;

// A control lease belongs to one request. The stop surface outlives individual
// tool calls, including the time spent waiting for the provider's next action.
internal sealed class AgentControlSession : IDisposable
{
    readonly Action cancel;
    readonly string model;
    readonly HashSet<string> grants=[];
    readonly List<Window> edges=[];
    Window? banner;
    TextBlock? action;
    HwndSource? source;
    CancellationTokenRegistration registration;
    bool disposed,hotkey;
    bool pointerActive;
    DateTime pointerEnded=DateTime.MinValue;
    const int StopHotkey=0x594f;
    internal bool Visible=>banner?.IsVisible==true;
    internal AgentControlSession(string model,Action cancel,CancellationToken token)
    {this.model=string.IsNullOrWhiteSpace(model)?"AI":model;this.cancel=cancel;registration=token.Register(()=>Application.Current?.Dispatcher.BeginInvoke(Dispose));}
    internal async Task<bool> Allow(string scope,string description,Func<string,string,CancellationToken,Task<bool>> ask,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();if(disposed)throw new OperationCanceledException(token);
        if(!grants.Contains(scope)) {
            if(!await ask("Allow control for this task?",description+"\n\nThe selected model may inspect this target and click, scroll and type while completing your prompt. Stop at any time using the on-screen button or Esc. Review the target before allowing.",token))return false;
            token.ThrowIfCancellationRequested();grants.Add(scope);
        }
        Show(description);return true;
    }
    internal void Show(string step)
    {
        if(disposed)return;
        if(banner==null) {
            foreach(var screen in System.Windows.Forms.Screen.AllScreens) {
                var glow=new ScreenControlGlow {Active=true,ScreenBounds=screen.Bounds,PointerOpacity=()=>pointerActive?1:SystemParameters.ClientAreaAnimation?Math.Clamp(1-(DateTime.UtcNow-pointerEnded).TotalMilliseconds/350,0,1):0};
                var edge=new Window {Title="YoWidgets control edge",WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=Brushes.Transparent,ShowInTaskbar=false,ShowActivated=false,Topmost=true,IsHitTestVisible=false,Content=glow};
                edge.SourceInitialized+=(_,_)=> {var handle=new WindowInteropHelper(edge).Handle;var ex=GetWindowLong(handle,-20);SetWindowLong(handle,-20,ex|0x08000000|0x20|0x80);var r=screen.Bounds;SetWindowPos(handle,0,r.X,r.Y,r.Width,r.Height,0x14);};
                edge.Show();edges.Add(edge);
            }
            var title=new TextBlock {Text=model+" · using your computer",FontSize=13,FontWeight=FontWeights.SemiBold,Foreground=Brushes.White,MaxWidth=460,TextTrimming=TextTrimming.CharacterEllipsis,ToolTip=model};
            action=new TextBlock {FontSize=11,Foreground=new SolidColorBrush(Color.FromRgb(205,217,235)),TextTrimming=TextTrimming.CharacterEllipsis,MaxWidth=460};
            var labels=new StackPanel {VerticalAlignment=VerticalAlignment.Center};labels.Children.Add(title);labels.Children.Add(action);
            var row=new DockPanel {LastChildFill=true};var stop=Ui.Button("Esc to cancel",Stop,true);stop.Padding=new Thickness(13,8,13,8);stop.Margin=new Thickness(18,0,0,0);stop.MinHeight=34;DockPanel.SetDock(stop,Dock.Right);row.Children.Add(stop);row.Children.Add(labels);
            banner=new Window {Title="AI control · YoWidgets",SizeToContent=SizeToContent.WidthAndHeight,MaxWidth=760,WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=Brushes.Transparent,ShowInTaskbar=false,ShowActivated=false,Topmost=true,Content=new Border {Background=new SolidColorBrush(Color.FromArgb(245,20,30,48)),BorderBrush=new SolidColorBrush(Color.FromArgb(180,106,180,255)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(17),Padding=new Thickness(17,10,12,10),Child=row}};
            banner.SourceInitialized+=(_,_)=> {source=HwndSource.FromHwnd(new WindowInteropHelper(banner).Handle);source.AddHook(Messages);hotkey=RegisterHotKey(source.Handle,StopHotkey,0x4000,27);if(!hotkey)stop.Content="Stop control";};
            banner.Loaded+=(_,_)=> {var area=SystemParameters.WorkArea;banner.Left=area.Left+(area.Width-banner.ActualWidth)/2;banner.Top=area.Top+14;};banner.Show();
        }
        action!.Text=step.Replace('\n',' ');
    }
    nint Messages(nint hwnd,int message,nint w,nint l,ref bool handled)
    {if(message==0x312&&w.ToInt32()==StopHotkey){handled=true;Stop();}return 0;}
    internal void Stop(){if(disposed)return;cancel();Dispose();}
    internal void RedrawPointer(){foreach(var edge in edges)if(edge.Content is ScreenControlGlow glow)glow.InvalidateVisual();}
    internal IDisposable PointerScope(){pointerActive=true;RedrawPointer();return new PointerLease(this);}
    sealed class PointerLease(AgentControlSession owner):IDisposable {public void Dispose(){owner.pointerActive=false;owner.pointerEnded=DateTime.UtcNow;owner.RedrawPointer();}}
    public void Dispose()
    {
        if(disposed)return;disposed=true;registration.Unregister();grants.Clear();
        if(source!=null){if(hotkey)UnregisterHotKey(source.Handle,StopHotkey);source.RemoveHook(Messages);source=null;}
        banner?.Close();banner=null;foreach(var edge in edges)edge.Close();edges.Clear();
    }
    [DllImport("user32.dll")] static extern bool RegisterHotKey(nint hwnd,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(nint hwnd,int id);
    [DllImport("user32.dll")] static extern int GetWindowLong(nint hwnd,int index);
    [DllImport("user32.dll")] static extern int SetWindowLong(nint hwnd,int index,int value);
    [DllImport("user32.dll")] static extern bool SetWindowPos(nint hwnd,nint after,int x,int y,int width,int height,uint flags);
}

internal sealed class ScreenControlGlow : ActiveVisual
{
    internal System.Drawing.Rectangle ScreenBounds {get;set;}
    internal Func<double>? PointerOpacity {get;set;}
    protected override void OnRender(DrawingContext dc)
    {
        if(!Active||ActualWidth<1||ActualHeight<1)return;
        var brush=new LinearGradientBrush {StartPoint=new(0,0),EndPoint=new(1,1),RelativeTransform=new RotateTransform(Time*25,.5,.5)};
        var colors=new[]{Color.FromRgb(80,195,255),Color.FromRgb(146,116,255),Color.FromRgb(255,122,168),Color.FromRgb(93,231,196),Color.FromRgb(80,195,255)};
        for(var i=0;i<colors.Length;i++)brush.GradientStops.Add(new(colors[i],i/4d));
        foreach(var (width,opacity) in new[]{(32d,.045),(20d,.08),(10d,.16),(3d,.95)}){dc.PushOpacity(opacity);dc.DrawRoundedRectangle(null,new Pen(brush,width),new Rect(1.5,1.5,Math.Max(0,ActualWidth-3),Math.Max(0,ActualHeight-3)),12,12);dc.Pop();}
        // Soft waves travel inward from the full display edge, never from the widget.
        for(var i=0;i<3;i++) {
            var phase=(Time*.48+i/3d)%1;var inset=phase*40;var rect=new Rect(inset,inset,Math.Max(0,ActualWidth-inset*2),Math.Max(0,ActualHeight-inset*2));
            dc.PushOpacity(.22*Math.Pow(1-phase,2));dc.DrawRoundedRectangle(null,new Pen(brush,3+phase*10),rect,12+inset*.25,12+inset*.25);dc.Pop();
        }
        var wave=new RadialGradientBrush {Center=new(.5,0),GradientOrigin=new(.5,0),RadiusX=.55,RadiusY=1};wave.GradientStops.Add(new(Color.FromArgb(75,117,158,255),0));wave.GradientStops.Add(new(Colors.Transparent,1));
        var reach=22+10*Math.Sin(Time*2);dc.DrawRectangle(wave,null,new Rect(0,0,ActualWidth,reach));dc.PushTransform(new RotateTransform(180,ActualWidth/2,ActualHeight/2));dc.DrawRectangle(wave,null,new Rect(0,0,ActualWidth,reach));dc.Pop();
        var pointerAlpha=PointerOpacity?.Invoke()??0;
        if(pointerAlpha>0&&ScreenBounds.Width>0&&GetCursorPos(out var point)&&ScreenBounds.Contains(point.X,point.Y)) {
            var x=(point.X-ScreenBounds.X)*ActualWidth/ScreenBounds.Width;var y=(point.Y-ScreenBounds.Y)*ActualHeight/ScreenBounds.Height;
            dc.PushOpacity(pointerAlpha);var halo=new RadialGradientBrush(Color.FromArgb(110,128,165,255),Colors.Transparent);dc.DrawEllipse(halo,null,new Point(x+5,y+8),35,35);
            dc.PushTransform(new TranslateTransform(x,y));var arrow=Geometry.Parse("M 0,0 L 1,25 L 8,19 L 14,31 L 20,28 L 13,16 L 26,14 Z");dc.DrawGeometry(brush,new Pen(Brushes.White,1.3),arrow);dc.Pop();dc.Pop();
        }
    }
    [StructLayout(LayoutKind.Sequential)] struct CursorPoint {public int X,Y;}
    [DllImport("user32.dll")] static extern bool GetCursorPos(out CursorPoint point);
}
