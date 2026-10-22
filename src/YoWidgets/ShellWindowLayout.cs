using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
namespace YoWidgets;

internal sealed record ShellWindowPlacement(RectD Bounds,bool Maximized);

// Physical window geometry is independent of CSS pixels and respects each monitor's work area.
internal sealed class ShellWindowLayout:IDisposable
{
    readonly Window window;readonly Action<object> post;readonly Action<ShellWindowPlacement>? persist;readonly ShellWindowPlacement? saved;
    HwndSource? source;IntPtr handle;RectD restore=new(0,0,1180,800);bool placing,disposed,lastMaximized;bool? targetMax;CancellationTokenSource? animation;int generation;WINDOWPLACEMENT? normalPlacement;
    internal bool IsAnimating=>animation!=null;
    internal WindowState RestoreState=>lastMaximized?WindowState.Maximized:WindowState.Normal;
    internal static ShellWindowPlacement? ParseSaved(string? json)
    {
        if(string.IsNullOrWhiteSpace(json))return null;
        try{var value=System.Text.Json.JsonSerializer.Deserialize<ShellWindowPlacement>(json,CatalogJson.Options);return value?.Bounds is {} bounds&&new[]{bounds.X,bounds.Y,bounds.Width,bounds.Height}.All(n=>double.IsFinite(n)&&Math.Abs(n)<1000000)&&bounds.Width>0&&bounds.Height>0?value:null;}catch(System.Text.Json.JsonException){return null;}
    }
    internal ShellWindowLayout(Window window,Action<object> post,ShellWindowPlacement? saved=null,Action<ShellWindowPlacement>? persist=null)
    {
        this.window=window;this.post=post;this.saved=saved;this.persist=persist;window.WindowStartupLocation=WindowStartupLocation.Manual;
        window.SourceInitialized+=Initialize;window.SizeChanged+=CaptureSize;window.LocationChanged+=CaptureLocation;window.StateChanged+=StateChanged;window.Closed+=Closed;
    }
    static RectD Rect(System.Drawing.Rectangle r)=>new(r.X,r.Y,r.Width,r.Height);
    double Scale=>Math.Max(96,Native.GetDpiForWindow(handle))/96d;
    RectD Work=>Rect(System.Windows.Forms.Screen.FromHandle(handle).WorkingArea);
    void Initialize(object? sender,EventArgs e)
    {
        handle=Native.Handle(window);source=HwndSource.FromHwnd(handle);source?.AddHook(Hook);placing=true;
        var area=saved!=null?System.Windows.Forms.Screen.FromRectangle(new((int)saved.Bounds.X,(int)saved.Bounds.Y,(int)saved.Bounds.Width,(int)saved.Bounds.Height)).WorkingArea:System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
        var work=Rect(area);var width=window.Width*Scale;var height=window.Height*Scale;var desired=saved?.Bounds??new(work.X+(work.Width-width)/2,work.Y+(work.Height-height)/2,width,height);
        restore=ShellLayoutRules.Fit(desired,work,Scale);Set(restore);Minimums();restore=ShellLayoutRules.Fit(restore,Work,Scale);Set(restore);
        if(saved?.Maximized==true)window.WindowState=WindowState.Maximized;placing=false;
    }
    void Minimums(){var work=Work;window.MinWidth=Math.Min(640,work.Width/Scale);window.MinHeight=Math.Min(420,work.Height/Scale);}
    void Set(RectD rect)=>Native.SetWindowPos(handle,IntPtr.Zero,(int)Math.Round(rect.X),(int)Math.Round(rect.Y),(int)Math.Round(rect.Width),(int)Math.Round(rect.Height),0x14);
    void Capture(){if(handle!=IntPtr.Zero&&!placing&&!IsAnimating&&window.WindowState==WindowState.Normal)restore=Native.Bounds(window);}
    void CaptureSize(object? sender,SizeChangedEventArgs e)=>Capture();
    void CaptureLocation(object? sender,EventArgs e)=>Capture();
    void StateChanged(object? sender,EventArgs e){if(window.WindowState!=WindowState.Minimized)lastMaximized=window.WindowState==WindowState.Maximized;if(!placing&&!IsAnimating&&window.WindowState!=WindowState.Minimized)Save();}
    internal void Save(){if(handle!=IntPtr.Zero&&!disposed&&!IsAnimating)persist?.Invoke(new(ShellLayoutRules.Fit(restore,Work,Scale),window.WindowState==WindowState.Minimized?lastMaximized:window.WindowState==WindowState.Maximized));}
    internal async Task ToggleAsync()
    {
        if(disposed||handle==IntPtr.Zero)return;
        var wasAnimating=IsAnimating;var maximize=!(targetMax??(window.WindowState==WindowState.Maximized));var stamp=++generation;
        animation?.Cancel();animation?.Dispose();animation=null;
        if(maximize&&!wasAnimating&&window.WindowState==WindowState.Normal){restore=Native.Bounds(window);var placement=new WINDOWPLACEMENT{Length=Marshal.SizeOf<WINDOWPLACEMENT>()};if(GetWindowPlacement(handle,ref placement))normalPlacement=placement;}
        placing=true;var from=Native.Bounds(window);if(window.WindowState!=WindowState.Normal){window.WindowState=WindowState.Normal;Set(from);}Minimums();placing=false;
        var to=maximize?Work:ShellLayoutRules.Fit(restore,Work,Scale);targetMax=maximize;
        var current=new CancellationTokenSource();animation=current;post(new{type="windowTransition",active=true});
        try{
            var completed=!SystemParameters.ClientAreaAnimation||SystemParameters.HighContrast?Immediate(to):await Animate(from,to,current.Token);
            if(!completed||stamp!=generation||disposed)return;
            placing=true;try{Set(to);if(maximize){window.WindowState=WindowState.Maximized;RestoreNativePlacement();}else restore=to;}finally{placing=false;}
        }finally{
            if(stamp==generation){animation=null;targetMax=null;current.Dispose();post(new{type="windowTransition",active=false});Save();}
        }
    }
    bool Immediate(RectD rect){Set(rect);return true;}
    // Keep the OS restore rectangle as well as our animated rectangle (Alt+Space/drag restore).
    void RestoreNativePlacement(){if(normalPlacement is {} placement){placement.ShowCommand=3;placement.Flags=0;SetWindowPlacement(handle,ref placement);}}
    Task<bool> Animate(RectD from,RectD to,CancellationToken token)
    {
        var completion=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);var clock=Stopwatch.StartNew();CancellationTokenRegistration registration=default;EventHandler? frame=null;var finished=false;
        void Finish(bool completed){if(finished)return;finished=true;CompositionTarget.Rendering-=frame;registration.Dispose();completion.TrySetResult(completed);}
        frame=(_,_)=>{if(token.IsCancellationRequested||disposed){Finish(false);return;}var progress=Math.Min(1,clock.Elapsed.TotalMilliseconds/320);var ease=1-Math.Pow(1-progress,4);double Mix(double a,double b)=>a+(b-a)*ease;Set(new(Mix(from.X,to.X),Mix(from.Y,to.Y),Mix(from.Width,to.Width),Mix(from.Height,to.Height)));if(progress>=1)Finish(true);};
        CompositionTarget.Rendering+=frame;registration=token.Register(()=>Finish(false));return completion.Task;
    }
    internal void Settle(bool complete=false){var target=targetMax;++generation;animation?.Cancel();animation?.Dispose();animation=null;targetMax=null;if(complete&&target is {} maximize&&!disposed){placing=true;try{Set(maximize?Work:ShellLayoutRules.Fit(restore,Work,Scale));window.WindowState=maximize?WindowState.Maximized:WindowState.Normal;if(maximize)RestoreNativePlacement();}finally{placing=false;}}post(new{type="windowTransition",active=false});}
    void FitVisible(){if(disposed||handle==IntPtr.Zero)return;placing=true;Minimums();if(window.WindowState==WindowState.Normal){restore=ShellLayoutRules.Fit(Native.Bounds(window),Work,Scale);Set(restore);}placing=false;Save();}
    IntPtr Hook(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
    {
        if(message==0x24){var info=Marshal.PtrToStructure<MINMAXINFO>(lParam);var screen=System.Windows.Forms.Screen.FromHandle(hwnd);var work=screen.WorkingArea;var monitor=screen.Bounds;info.MaxPosition=new(){X=work.Left-monitor.Left,Y=work.Top-monitor.Top};info.MaxSize=new(){X=work.Width,Y=work.Height};info.MinTrackSize=new(){X=(int)Math.Min(640*Scale,work.Width),Y=(int)Math.Min(420*Scale,work.Height)};Marshal.StructureToPtr(info,lParam,false);handled=true;}
        else if(message==0x231){Settle();}
        else if(message==0x232){FitVisible();}
        else if(message is 0x2e0 or 0x7e or 0x1a){Settle();window.Dispatcher.BeginInvoke(FitVisible);}
        return IntPtr.Zero;
    }
    void Closed(object? sender,EventArgs e)=>Dispose();
    public void Dispose(){if(disposed)return;Save();disposed=true;Settle();source?.RemoveHook(Hook);window.SourceInitialized-=Initialize;window.SizeChanged-=CaptureSize;window.LocationChanged-=CaptureLocation;window.StateChanged-=StateChanged;window.Closed-=Closed;}
    [StructLayout(LayoutKind.Sequential)]struct POINT{public int X,Y;}
    [StructLayout(LayoutKind.Sequential)]struct MINMAXINFO{public POINT Reserved,MaxSize,MaxPosition,MinTrackSize,MaxTrackSize;}
    [StructLayout(LayoutKind.Sequential)]struct WINDOWPLACEMENT{public int Length,Flags,ShowCommand;public POINT MinPosition,MaxPosition;public Native.RECT NormalPosition;}
    [DllImport("user32.dll")]static extern bool GetWindowPlacement(IntPtr hwnd,ref WINDOWPLACEMENT placement);
    [DllImport("user32.dll")]static extern bool SetWindowPlacement(IntPtr hwnd,ref WINDOWPLACEMENT placement);
}
