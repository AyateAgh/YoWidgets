using System.Runtime.InteropServices;
using System.Text.Json;
namespace YoWidgets;

internal sealed record FocusAudioState(string Device,bool Muted,string? RecoveryKey=null,string? GroupKey=null);
internal interface IFocusEnvironment
{
    string Profile {get;set;}
    FocusAudioState[] Audio();
    void SetMute(string device,bool value);
    IDisposable? Watch(Action changed)=>null;
    void Restore(FocusAudioState state)=>SetMute(state.Device,state.Muted);
    void ReleaseAudio(){}
}

// The lease is persisted before system changes. Restore only values still owned by this session.
internal sealed class FocusSession:IDisposable
{
    internal const string Quiet="Microsoft.QuietHoursProfile.AlarmsOnly";
    sealed record Lease(string? Profile,FocusAudioState[] Audio,DateTimeOffset End);
    readonly string path;readonly IFocusEnvironment environment;readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(1)};
    Lease? lease;
    IDisposable? audioWatch;
    bool quietAudio;
    internal DateTimeOffset? End=>lease?.End;
    internal string Error {get;private set;}="";
    internal event Action? Changed;
    internal FocusSession(string root,IFocusEnvironment? environment=null)
    {
        path=Path.Combine(root,"focus-session.json");this.environment=environment??new WindowsFocusEnvironment();
        timer.Tick+=(_,_)=>Tick(DateTimeOffset.Now);
        if(File.Exists(path))try{lease=JsonSerializer.Deserialize<Lease>(File.ReadAllText(path));Stop();}catch(Exception e){Error="Previous Focus settings need recovery.";App.Log("focus-recover",e);}
    }
    internal void Start(int minutes,bool silence)
    {
        if(minutes is <1 or >1440)throw new InvalidOperationException("Choose 1–1440 minutes.");
        if(lease!=null)throw new InvalidOperationException("End the current session first.");
        if(File.Exists(path))throw new InvalidOperationException("Previous Focus settings need recovery. Restart YoWidgets before starting again.");
        Error="";
        try{
            var original=environment.Profile;
            if(!original.StartsWith("Microsoft.QuietHoursProfile.",StringComparison.Ordinal))throw new InvalidOperationException("Windows did not return a Do Not Disturb state.");
            var audio=silence?environment.Audio():[];
            audio=Normalize(audio);
            lease=new(original,audio,DateTimeOffset.Now.AddMinutes(minutes));Write(lease);
            environment.Profile=Quiet;
            if(environment.Profile!=Quiet)throw new InvalidOperationException("Windows did not enable Do Not Disturb.");
            foreach(var device in audio.Where(d=>!d.Muted))environment.SetMute(device.Device,true);
            quietAudio=silence;
            if(silence)audioWatch=environment.Watch(()=>timer.Dispatcher.BeginInvoke(ReconcileAudio));
            timer.Start();Changed?.Invoke();
        }catch(Exception e){Error="Do Not Disturb could not start. Check Windows notification settings.";App.Log("focus-start",e);Stop();throw new InvalidOperationException(Error);}
    }
    void ReconcileAudio()
    {
        if(lease is not {} active||!quietAudio)return;
        try{var added=environment.Audio().Where(device=>!active.Audio.Any(saved=>saved.Device==device.Device)).ToArray();if(added.Length==0)return;lease=active with{Audio=Normalize(active.Audio.Concat(added).ToArray())};Write(lease);foreach(var device in added.Where(d=>!d.Muted))environment.SetMute(device.Device,true);}
        catch(Exception e){Error="Some call apps could not be silenced.";App.Log("focus-call-audio",e);Changed?.Invoke();}
    }
    internal void Tick(DateTimeOffset now){if(lease!=null&&now>=lease.End)Stop();else{ReconcileAudio();Changed?.Invoke();}}
    internal void Stop()
    {
        timer.Stop();quietAudio=false;audioWatch?.Dispose();audioWatch=null;if(lease is not {} previous){environment.ReleaseAudio();return;}
        string? profile=null;var audio=new List<FocusAudioState>();
        try{if(previous.Profile!=null&&environment.Profile==Quiet)environment.Profile=previous.Profile;}catch(Exception e){profile=previous.Profile;App.Log("focus-restore",e);}
        foreach(var device in previous.Audio.Where(d=>!d.Muted))try{environment.Restore(device);}catch(Exception e){audio.Add(device);App.Log("focus-audio-restore",e);}
        environment.ReleaseAudio();
        lease=null;
        if(profile==null&&audio.Count==0){if(File.Exists(path))File.Delete(path);}else{Write(new(profile,audio.ToArray(),previous.End));Error=audio.Count>0?"Open the call app and restart YoWidgets to finish audio recovery.":"Focus could not restore Do Not Disturb. Check Windows settings.";}
        Changed?.Invoke();
    }
    void Write(Lease value){File.WriteAllText(path+".tmp",JsonSerializer.Serialize(value));File.Move(path+".tmp",path,true);}
    internal static FocusAudioState[] Normalize(FocusAudioState[] states)=>states.Select(state=>state.RecoveryKey!=null&&states.Any(other=>(other.GroupKey??other.RecoveryKey)==(state.GroupKey??state.RecoveryKey)&&other.Muted!=state.Muted)?state with{RecoveryKey=null}:state).ToArray();
    public void Dispose()=>Stop();
}

// Windows exposes read-only Focus sessions publicly. The shell compatibility adapter
// uses the established QuietHours COM profile contract; failure is surfaced, never faked.
internal sealed class WindowsFocusEnvironment(Func<string[]>? quietApps=null):IFocusEnvironment
{
    readonly Dictionary<string,object> retained=[];
    public string Profile
    {
        get=>WithQuiet(q=>q.Profile);
        set=>WithQuiet(q=>{q.Profile=value;return value;});
    }
    static T WithQuiet<T>(Func<IQuietHours,T> action)
    {
        var cls=new Guid("F53321FA-34F8-4B7F-B9A3-361877CB94CF");var iid=typeof(IQuietHours).GUID;
        Marshal.ThrowExceptionForHR(CoCreateInstance(ref cls,IntPtr.Zero,4,ref iid,out var instance));
        try{return action((IQuietHours)instance);}finally{Marshal.FinalReleaseComObject(instance);}
    }
    public FocusAudioState[] Audio()=>FocusAppAudio.Read(quietApps?.Invoke()??FocusAppAudio.DefaultApps,retained);
    public void SetMute(string session,bool value)=>FocusAppAudio.SetMute(session,value,retained);
    public void Restore(FocusAudioState state)=>FocusAppAudio.SetMute(state.Device,state.Muted,retained,state.RecoveryKey);
    public void ReleaseAudio(){foreach(var control in retained.Values)Marshal.ReleaseComObject(control);retained.Clear();}
    public IDisposable Watch(Action changed)=>FocusAppAudio.Watch(changed);
    [DllImport("ole32.dll")]static extern int CoCreateInstance(ref Guid cls,IntPtr outer,uint context,ref Guid iid,[MarshalAs(UnmanagedType.IUnknown)]out object instance);
    [ComImport,Guid("6BFF4732-81EC-4FFB-AE67-B6C1BC29631F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IQuietHours
    {string Profile{[return:MarshalAs(UnmanagedType.LPWStr)]get;[param:MarshalAs(UnmanagedType.LPWStr)]set;}}
}

