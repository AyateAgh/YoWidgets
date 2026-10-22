using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace YoWidgets;

internal sealed class ShellController:IDisposable
{
    internal const int ShowMessage=0x8000+431;internal const int ExitMessage=0x8000+432;internal const int EvictMessage=0x8000+433;
    readonly DesktopPlatform platform;Process? process;IntPtr window;EventWaitHandle? memoryEvent;RegisteredWaitHandle? memoryWait;
    internal ShellController(DesktopPlatform platform){this.platform=platform;}
    internal void Show()
    {
        if(process is {HasExited:false}){if(window!=IntPtr.Zero)PostMessage(window,ShowMessage,IntPtr.Zero,IntPtr.Zero);return;}
        window=IntPtr.Zero;
        var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true};
        foreach(var argument in new[]{"--shell-host","--data-dir",App.DataRoot})start.ArgumentList.Add(argument);
        process=Process.Start(start)??throw new InvalidOperationException("YoWidgets could not open its interface.");
        WatchMemory();
    }
    internal void Register(IntPtr handle)
    {
        GetWindowThreadProcessId(handle,out var pid);if(process==null||pid!=process.Id)throw new InvalidOperationException("Unexpected management window.");window=handle;
    }
    internal void Select(string page){platform.RequestedPage=page=="widgets"?"home":page=="cleanup"?"settings/about":page;platform.Publish();Show();}
    internal void OpenChatSettings(string provider){platform.RequestedProvider=provider;Select("settings/connections");}
    internal void Status(string text){platform.Notice=text;platform.Publish();}
    internal void CloseHost(){if(window!=IntPtr.Zero)PostMessage(window,ExitMessage,IntPtr.Zero,IntPtr.Zero);}
    void EvictHost(){if(window!=IntPtr.Zero)PostMessage(window,EvictMessage,IntPtr.Zero,IntPtr.Zero);}
    void WatchMemory()
    {
        memoryWait?.Unregister(null);memoryEvent?.Dispose();var handle=CreateMemoryResourceNotification(0);if(handle==IntPtr.Zero)return;
        memoryEvent=new EventWaitHandle(false,EventResetMode.ManualReset);memoryEvent.SafeWaitHandle=new SafeWaitHandle(handle,true);
        memoryWait=ThreadPool.RegisterWaitForSingleObject(memoryEvent,(_,_)=>Application.Current.Dispatcher.BeginInvoke(EvictHost),null,Timeout.Infinite,true);
    }
    public void Dispose(){CloseHost();memoryWait?.Unregister(null);memoryEvent?.Dispose();process?.Dispose();}
    [DllImport("user32.dll")]static extern bool PostMessage(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr hwnd,out int pid);
    [DllImport("kernel32.dll")]static extern IntPtr CreateMemoryResourceNotification(int type);
}
