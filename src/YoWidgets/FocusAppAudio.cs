using System.Diagnostics;
using System.Runtime.InteropServices;
namespace YoWidgets;

// Only explicitly selected communication processes are eligible. Never mute an endpoint,
// browser, system-sounds session, music player, or an unidentified process.
internal static class FocusAppAudio
{
    internal static readonly string[] DefaultApps=["Telegram","Discord","DiscordPTB","DiscordCanary","WhatsApp","ms-teams","Teams","Signal","Slack","Zoom","Skype","Viber"];
    internal static bool Eligible(string process,IEnumerable<string> selected)=>selected.Contains(process,StringComparer.OrdinalIgnoreCase)&&!new[]{"chrome","msedge","firefox","brave","Spotify","Music.UI","vlc","YoWidgets"}.Contains(process,StringComparer.OrdinalIgnoreCase);
    internal static FocusAudioState[] Read(string[] selected,Dictionary<string,object>? retained=null)
    {
        var states=new List<FocusAudioState>();
        Sessions((device,control)=>{try{var info=(ISessionInfo)control;Marshal.ThrowExceptionForHR(info.GetProcessId(out var pid));if(pid==0)return;using var process=Process.GetProcessById((int)pid);if(!Eligible(process.ProcessName,selected))return;Marshal.ThrowExceptionForHR(info.GetSessionInstanceIdentifier(out var id));var volume=(ISessionVolume)control;Marshal.ThrowExceptionForHR(volume.GetMute(out var muted));Marshal.ThrowExceptionForHR(info.GetSessionIdentifier(out var stable));var key=device+"\n"+id;if(retained!=null&&!retained.ContainsKey(key)){var pointer=Marshal.GetIUnknownForObject(control);try{retained[key]=Marshal.GetObjectForIUnknown(pointer);}finally{Marshal.Release(pointer);}}states.Add(new(key,muted,device+"\n"+stable,device+"\n"+stable));}catch(Exception e)when(e is ArgumentException or InvalidOperationException or COMException){}});
        return states.DistinctBy(state=>state.Device).ToArray();
    }
    internal static void SetMute(string key,bool muted,Dictionary<string,object>? retained=null,string? recoveryKey=null)
    {
        if(retained!=null&&retained.TryGetValue(key,out var owned)){Change(owned,muted);return;}
        var found=false;
        Sessions((device,control)=>{var info=(ISessionInfo)control;Marshal.ThrowExceptionForHR(info.GetSessionInstanceIdentifier(out var id));if(device+"\n"+id!=key)return;found=true;Change(control,muted);});
        if(found)return;
        string? candidate=null;var count=0;
        if(recoveryKey!=null)Sessions((device,control)=>{var info=(ISessionInfo)control;Marshal.ThrowExceptionForHR(info.GetSessionIdentifier(out var stable));if(device+"\n"+stable!=recoveryKey)return;Marshal.ThrowExceptionForHR(info.GetSessionInstanceIdentifier(out var id));candidate=device+"\n"+id;count++;});
        if(count==1&&candidate!=null){SetMute(candidate,muted,retained);return;}
        throw new InvalidOperationException("Open the call app and reconnect its audio device, then restart YoWidgets to finish Focus recovery.");
    }
    static void Change(object control,bool muted){var volume=(ISessionVolume)control;Marshal.ThrowExceptionForHR(volume.GetMute(out var current));if(muted||current){var context=Guid.Empty;Marshal.ThrowExceptionForHR(volume.SetMute(muted,ref context));}}
    static void Sessions(Action<string,object> action)
    {
        var managers=Managers();try{foreach(var entry in managers){Marshal.ThrowExceptionForHR(entry.Manager.GetSessionEnumerator(out var sessions));try{Marshal.ThrowExceptionForHR(sessions.GetCount(out var count));for(var index=0;index<count;index++){Marshal.ThrowExceptionForHR(sessions.GetSession(index,out var control));try{action(entry.Device,control);}finally{Marshal.ReleaseComObject(control);}}}finally{Marshal.ReleaseComObject(sessions);}}}finally{foreach(var entry in managers)Marshal.ReleaseComObject(entry.Manager);}
    }
    static List<(string Device,ISessionManager Manager)> Managers()
    {
        var result=new List<(string,ISessionManager)>();var enumerator=(IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(new("BCDE0395-E52F-467C-8E3D-C4579291692E"))!)!;object? collection=null;
        try{Marshal.ThrowExceptionForHR(enumerator.EnumAudioEndpoints(0,1,out var pointer));try{collection=Marshal.GetObjectForIUnknown(pointer);}finally{Marshal.Release(pointer);}var devices=(IDevices)collection;Marshal.ThrowExceptionForHR(devices.GetCount(out var count));for(uint index=0;index<count;index++){Marshal.ThrowExceptionForHR(devices.Item(index,out var device));try{Marshal.ThrowExceptionForHR(device.GetId(out var id));var iid=typeof(ISessionManager).GUID;Marshal.ThrowExceptionForHR(device.Activate(ref iid,23,IntPtr.Zero,out var manager));result.Add((id,(ISessionManager)manager));}finally{Marshal.ReleaseComObject(device);}}return result;}
        catch{foreach(var entry in result)Marshal.ReleaseComObject(entry.Item2);throw;}finally{if(collection!=null)Marshal.ReleaseComObject(collection);Marshal.ReleaseComObject(enumerator);}
    }
    internal static IDisposable Watch(Action changed)=>new Watcher(changed);
    sealed class Watcher:IDisposable
    {
        readonly ManualResetEvent stop=new(false);int disposed;
        internal Watcher(Action changed)
        {
            var thread=new Thread(()=>{
                var initialized=CoInitializeEx(IntPtr.Zero,0)>=0;var managers=new List<(string Device,ISessionManager Manager)>();var registered=new List<ISessionManager>();var callback=new SessionNotification(()=>{if(Volatile.Read(ref disposed)==0)changed();});
                try{managers=Managers();foreach(var entry in managers){Marshal.ThrowExceptionForHR(entry.Manager.RegisterSessionNotification(callback));registered.Add(entry.Manager);Marshal.ThrowExceptionForHR(entry.Manager.GetSessionEnumerator(out var sessions));try{sessions.GetCount(out _);}finally{Marshal.ReleaseComObject(sessions);}}if(Volatile.Read(ref disposed)==0)changed();stop.WaitOne();}
                catch(Exception e){App.Log("focus-audio-watch",e);}
                finally{foreach(var manager in registered)try{manager.UnregisterSessionNotification(callback);}catch(COMException){}foreach(var entry in managers)Marshal.ReleaseComObject(entry.Manager);if(initialized)CoUninitialize();stop.Dispose();}
            }){IsBackground=true,Name="YoWidgets Focus audio"};thread.SetApartmentState(ApartmentState.MTA);thread.Start();
        }
        public void Dispose(){if(Interlocked.Exchange(ref disposed,1)==0)try{stop.Set();}catch(ObjectDisposedException){}}
    }
    [ComVisible(true),ClassInterface(ClassInterfaceType.None)]sealed class SessionNotification(Action changed):INewSession
    {public int OnSessionCreated(object session){try{changed();}catch(Exception e){App.Log("focus-audio-event",e);}return 0;}}
    [DllImport("ole32.dll")]static extern int CoInitializeEx(IntPtr reserved,uint flags);
    [DllImport("ole32.dll")]static extern void CoUninitialize();
    [ComImport,Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IDevices
    {[PreserveSig]int GetCount(out uint count);[PreserveSig]int Item(uint index,out IMMDevice device);}
    [ComImport,Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface ISessionManager
    {[PreserveSig]int GetAudioSessionControl(IntPtr guid,uint flags,out IntPtr control);[PreserveSig]int GetSimpleAudioVolume(IntPtr guid,uint flags,out IntPtr volume);[PreserveSig]int GetSessionEnumerator(out ISessions sessions);[PreserveSig]int RegisterSessionNotification(INewSession callback);[PreserveSig]int UnregisterSessionNotification(INewSession callback);}
    [ComImport,Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface ISessions
    {[PreserveSig]int GetCount(out int count);[PreserveSig]int GetSession(int index,[MarshalAs(UnmanagedType.IUnknown)]out object control);}
    [ComImport,Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface ISessionInfo
    {[PreserveSig]int GetState(out int state);[PreserveSig]int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)]out string name);[PreserveSig]int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)]string name,ref Guid context);[PreserveSig]int GetIconPath([MarshalAs(UnmanagedType.LPWStr)]out string path);[PreserveSig]int SetIconPath([MarshalAs(UnmanagedType.LPWStr)]string path,ref Guid context);[PreserveSig]int GetGroupingParam(out Guid grouping);[PreserveSig]int SetGroupingParam(ref Guid grouping,ref Guid context);[PreserveSig]int RegisterAudioSessionNotification(IntPtr callback);[PreserveSig]int UnregisterAudioSessionNotification(IntPtr callback);[PreserveSig]int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)]out string id);[PreserveSig]int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)]out string id);[PreserveSig]int GetProcessId(out uint pid);}
    [ComImport,Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface ISessionVolume
    {[PreserveSig]int SetMasterVolume(float volume,ref Guid context);[PreserveSig]int GetMasterVolume(out float volume);[PreserveSig]int SetMute([MarshalAs(UnmanagedType.Bool)]bool value,ref Guid context);[PreserveSig]int GetMute([MarshalAs(UnmanagedType.Bool)]out bool value);}
    [ComVisible(true),Guid("641DD20B-4D41-49CC-ABA3-174B9477BB08"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface INewSession
    {[PreserveSig]int OnSessionCreated([MarshalAs(UnmanagedType.IUnknown)]object session);}
}
