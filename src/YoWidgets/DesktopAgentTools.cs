using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Automation;
namespace YoWidgets;

internal sealed class DesktopAgentTools
{
    sealed record Target(nint Handle,int Pid,string Title,Rect Bounds);
    readonly Dictionary<string,Target> snapshots=[];
    readonly Func<string,string,CancellationToken,Task<bool>> approve;
    readonly AgentControlSession? session;
    readonly InstalledApplications applications;
    internal Action<string>? Progress {get;set;}
    internal DesktopAgentTools(Func<string,string,CancellationToken,Task<bool>> approve,AgentControlSession? session=null,InstalledApplications? applications=null){this.approve=approve;this.session=session;this.applications=applications??new();}
    Task<bool> Allow(Target target,string detail,CancellationToken token)=>session!=null?session.Allow("window/"+target.Handle+"/"+target.Pid,target.Title,approve,token):approve("Allow this desktop action?",target.Title+"\n\n"+detail,token);
    static readonly string[] protectedProcesses=["cmd","powershell","pwsh","powershell_ise","windowsterminal","conhost","openconsole","credentialuibroker","logonui","lockapp","keepass","keepassxc","1password","bitwarden","securityhealthsystray","securityhealthservice","applicationframehost"];
    internal object List()
    {
        var windows=new List<object>();EnumWindows((hwnd,_)=>{if(windows.Count>=80)return false;var target=Describe(hwnd);if(target!=null)windows.Add(new{id=hwnd.ToInt64().ToString(),title=target.Title,pid=target.Pid,minimized=IsIconic(hwnd),width=target.Bounds.Width,height=target.Bounds.Height});return true;},0);return new{windows,note="For closed or tray-only apps, use computer_list_apps followed by computer_open_app. Do not try the Windows key or an unrelated settings window."};
    }
    internal object Apps(string query)=>new {apps=applications.Find(query).Select(a=>new{app_id=a.Id,name=a.Name}),note="Pass an exact app_id to computer_open_app; no shell commands or guessed executable paths."};
    internal async Task<object> OpenApp(string id,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();var app=applications.Get(id);
        if(!await approve("Open this installed application?",app.Name+"\n"+app.Executable+"\n\nLaunch or show this application for the current task. Inspect its real window before clicking chats or claiming success.",token))throw new InvalidOperationException("The user denied opening "+app.Name+".");
        token.ThrowIfCancellationRequested();session?.Show("Opening "+app.Name+"…");Progress?.Invoke("Opening "+app.Name+"…");using var launched=InstalledApplications.Start(app);
        var expected=Path.GetFileNameWithoutExtension(app.Executable).Equals("Update",StringComparison.OrdinalIgnoreCase)?app.Name:Path.GetFileNameWithoutExtension(app.Executable);
        var root=Path.GetDirectoryName(app.Executable)!+Path.DirectorySeparatorChar;
        for(var attempt=0;attempt<60;attempt++) {
            token.ThrowIfCancellationRequested();Target? found=null;
            EnumWindows((hwnd,_)=> {var target=Describe(hwnd);if(target==null)return true;try {using var process=Process.GetProcessById(target.Pid);var path=process.MainModule?.FileName??"";if(process.ProcessName.Equals(expected,StringComparison.OrdinalIgnoreCase)&&path.StartsWith(root,StringComparison.OrdinalIgnoreCase)){found=target;return false;}}catch(Exception e) when(e is ArgumentException or System.ComponentModel.Win32Exception){}return true;},0);
            if(found!=null) {await Focus(found.Handle,token);return new{app=app.Name,window_id=found.Handle.ToInt64().ToString(),title=found.Title,ready=true,note="The app window is open. Call computer_inspect for a fresh screenshot and controls. Opening the app does not mean a chat has been read."};}
            await Task.Delay(250,token);
        }
        throw new InvalidOperationException(app.Name+" was launched but no ready window appeared within 15 seconds. It may be updating, tray-only or awaiting sign-in. List windows again; do not open unrelated apps or claim chats were inspected.");
    }
    static Target? Describe(nint hwnd)
    {
        if(!IsWindowVisible(hwnd)||!GetWindowRect(hwnd,out var r)||!IsIconic(hwnd)&&(r.Right-r.Left<60||r.Bottom-r.Top<40))return null;GetWindowThreadProcessId(hwnd,out var pid);if(pid==Environment.ProcessId)return null;
        try {using var process=Process.GetProcessById((int)pid);if(protectedProcesses.Contains(process.ProcessName.ToLowerInvariant()))return null;}catch(ArgumentException){return null;}
        var title=new StringBuilder(600);GetWindowText(hwnd,title,title.Capacity);if(title.Length==0)return null;return new(hwnd,(int)pid,title.ToString(),new Rect(r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top));
    }
    internal async Task<AgentToolResult> Inspect(AgentToolCall call,CancellationToken token,bool documentOnly=false)
    {
        var id=call.Arguments.GetProperty("window_id").GetString()??"";if(!long.TryParse(id,out var number)||Describe((nint)number) is not { } target)throw new InvalidOperationException("Choose a visible application window. Terminals, authentication/security apps and YoWidgets are excluded.");
        if(!await Allow(target,"Inspect window text and image",token))throw new InvalidOperationException("The user denied this inspection.");session?.Show("Inspecting · "+target.Title);Progress?.Invoke("Inspecting "+target.Title+"…");
        token.ThrowIfCancellationRequested();await Focus(target.Handle,token);var current=Describe(target.Handle);if(current==null||current.Pid!=target.Pid||current.Title!=target.Title)throw new InvalidOperationException("The window changed. List windows again.");target=current;
        (List<object> Controls,string Text) controls;
        try {controls=await Task.Run(()=>ReadTree(target,documentOnly),token).WaitAsync(TimeSpan.FromSeconds(8),token);}
        catch(Exception e) when(!documentOnly&&e is ElementNotAvailableException or COMException or TimeoutException){controls=([],"This app does not expose accessibility controls. Use the actual screenshot for coordinates.");}
        var png=await Task.Run(()=>Capture(target),token).WaitAsync(TimeSpan.FromSeconds(8),token);
        var snapshot=Guid.NewGuid().ToString("N");if(snapshots.Count>=4)snapshots.Remove(snapshots.Keys.First());snapshots[snapshot]=target;
        return new(call,JsonSerializer.Serialize(new{snapshot_id=snapshot,snapshotId=snapshot,window_id=id,title=target.Title,width=target.Bounds.Width,height=target.Bounds.Height,controls=controls.Controls,text=controls.Text}),false,new(target.Title+".png","image/png",Convert.ToBase64String(png),null,png.Length));
    }
    static (List<object> Controls,string Text) ReadTree(Target target,bool documentOnly)
    {
        var root=AutomationElement.FromHandle(target.Handle);
        if(documentOnly)root=root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Document))??throw new InvalidOperationException("The page is unavailable to Windows. Use the browser companion.");
        var text="";if(documentOnly&&root.TryGetCurrentPattern(TextPattern.Pattern,out var pattern))text=((TextPattern)pattern).DocumentRange.GetText(64000);
        var nodes=new Queue<(AutomationElement Node,int Depth)>();nodes.Enqueue((root,0));var controls=new List<object>();var walk=TreeWalker.ControlViewWalker;
        while(nodes.TryDequeue(out var entry)&&controls.Count<300) {
            var info=entry.Node.Current;if(!info.IsPassword){var box=info.BoundingRectangle;var name=info.Name??"";controls.Add(new{id=controls.Count,role=info.ControlType.ProgrammaticName,name=name[..Math.Min(1000,name.Length)],x=double.IsFinite(box.X)?box.X-target.Bounds.X:0,y=double.IsFinite(box.Y)?box.Y-target.Bounds.Y:0,width=double.IsFinite(box.Width)?box.Width:0,height=double.IsFinite(box.Height)?box.Height:0});}
            if(info.IsPassword||entry.Depth>=12)continue;for(var child=walk.GetFirstChild(entry.Node);child!=null&&nodes.Count<500;child=walk.GetNextSibling(child))nodes.Enqueue((child,entry.Depth+1));
        }return (controls,text);
    }
    static byte[] Capture(Target target)
    {
        if(target.Bounds.Width*target.Bounds.Height>16_000_000)throw new InvalidOperationException("Resize this window before capturing it.");
        using var bitmap=new System.Drawing.Bitmap((int)target.Bounds.Width,(int)target.Bounds.Height);bool captured;using(var graphics=System.Drawing.Graphics.FromImage(bitmap)){var dc=graphics.GetHdc();try {captured=PrintWindow(target.Handle,dc,2);}finally {graphics.ReleaseHdc(dc);}}
        if(!captured||DarkFrame(bitmap)){
            CheckVisibleCapture(target);
            using var graphics=System.Drawing.Graphics.FromImage(bitmap);graphics.CopyFromScreen((int)target.Bounds.X,(int)target.Bounds.Y,0,0,bitmap.Size);
            CheckVisibleCapture(target);
        }
        using var stream=new MemoryStream();bitmap.Save(stream,System.Drawing.Imaging.ImageFormat.Png);if(stream.Length>8*1024*1024)throw new InvalidOperationException("This window image is too large.");return stream.ToArray();
    }
    static bool DarkFrame(System.Drawing.Bitmap bitmap)
    {for(var y=1;y<8;y++)for(var x=1;x<8;x++){var p=bitmap.GetPixel(x*bitmap.Width/8,y*bitmap.Height/8);if(p.R>8||p.G>8||p.B>8)return false;}return true;}
    static void CheckVisibleCapture(Target target)
    {
        if(GetForegroundWindow()!=target.Handle)throw new InvalidOperationException("Activate the app before capturing its rendered screen. Windowed or borderless mode works best.");
        for(var y=1;y<5;y++)for(var x=1;x<5;x++)if(GetAncestor(WindowFromPoint(new(){X=(int)(target.Bounds.X+target.Bounds.Width*x/5),Y=(int)(target.Bounds.Y+target.Bounds.Height*y/5)}),2)!=target.Handle)throw new InvalidOperationException("Another window covers the app. Inspect it again after uncovering it.");
    }
    internal async Task<object> Act(string action,JsonElement args,CancellationToken token)
    {
        var snapshot=args.GetProperty("snapshot_id").GetString()??"";if(!snapshots.TryGetValue(snapshot,out var target))throw new InvalidOperationException("Inspect this window before acting.");
        var pointer=action is "computer_click" or "computer_mouse" or "computer_fill" or "computer_scroll" or "computer_drag";
        var button=action=="computer_mouse"?args.GetProperty("button").GetString():"left";
        if(pointer&&button is not ("left" or "right" or "middle"))throw new InvalidOperationException("Choose left, right or middle mouse button.");
        var press=action=="computer_press"?PhysicalKey(args.GetProperty("key").GetString()??""):Array.Empty<ushort>();
        var duration=action=="computer_press"?args.GetProperty("duration_ms").GetInt32():0;
        if(duration is <0 or >2000)throw new InvalidOperationException("Hold a key for 0 to 2000 milliseconds, then inspect the result.");
        var detail=pointer?$"{action} at ({args.GetProperty("x")}, {args.GetProperty("y")})":action=="computer_type"?"Type this text:\n"+(args.GetProperty("text").GetString()??""):"Press "+args.GetProperty("key").GetString();
        if(!await Allow(target,detail,token))throw new InvalidOperationException("The user denied this desktop action.");var label=action switch {"computer_click"=>"Clicking the selected control","computer_mouse"=>"Clicking · "+button,"computer_fill"=>"Filling the selected field","computer_type"=>"Typing in the app","computer_key" or "computer_press"=>"Pressing "+args.GetProperty("key").GetString(),"computer_scroll"=>"Scrolling the app","computer_drag"=>"Dragging the selected control",_=>"Working with the app"};session?.Show(label+" · "+target.Title);Progress?.Invoke(label+" · "+target.Title);
        token.ThrowIfCancellationRequested();var now=Describe(target.Handle);if(now==null||now.Pid!=target.Pid||now.Title!=target.Title||now.Bounds!=target.Bounds)throw new InvalidOperationException("The window changed. Inspect it again before acting.");
        await Focus(target.Handle,token);
        using var pointerLease=pointer?session?.PointerScope():null;
        try {if(AutomationElement.FocusedElement?.Current.IsPassword==true)throw new InvalidOperationException("Complete password entry yourself.");}catch(ElementNotAvailableException){throw new InvalidOperationException("The focused control changed.");}
        if(pointer) {
            var x=args.GetProperty("x").GetDouble();var y=args.GetProperty("y").GetDouble();if(!double.IsFinite(x)||!double.IsFinite(y)||x<0||y<0||x>=target.Bounds.Width||y>=target.Bounds.Height)throw new InvalidOperationException("Click inside the inspected window.");
            var point=new PointNative{X=(int)(target.Bounds.X+x),Y=(int)(target.Bounds.Y+y)};if(GetAncestor(WindowFromPoint(point),2)!=target.Handle)throw new InvalidOperationException("Another window covers this point. Inspect again.");
            await Move(point,target.Handle,token);
            if(action=="computer_scroll")Send([new Input{Type=0,Data=new InputData{Mouse=new MouseInput{Flags=0x800,Data=unchecked((uint)Math.Clamp(args.GetProperty("delta").GetInt32(),-1200,1200))}}}]);
            else if(action=="computer_drag") {
                var endX=args.GetProperty("end_x").GetDouble();var endY=args.GetProperty("end_y").GetDouble();if(!double.IsFinite(endX)||!double.IsFinite(endY)||endX<0||endY<0||endX>=target.Bounds.Width||endY>=target.Bounds.Height)throw new InvalidOperationException("Drag inside the inspected window.");
                Send([new Input{Type=0,Data=new InputData{Mouse=new MouseInput{Flags=2}}}]);try {await Move(new PointNative{X=(int)(target.Bounds.X+endX),Y=(int)(target.Bounds.Y+endY)},target.Handle,token);}finally {Send([new Input{Type=0,Data=new InputData{Mouse=new MouseInput{Flags=4}}}]);}
            }else {
                uint down=button=="right"?8u:button=="middle"?32u:2u;
                try {Send([new Input{Type=0,Data=new InputData{Mouse=new MouseInput{Flags=down}}}]);}finally {Send([new Input{Type=0,Data=new InputData{Mouse=new MouseInput{Flags=down*2}}}]);}
                if(action=="computer_fill"){await Task.Delay(80,token);CheckFocus(target);Send([Key(17,0,0),Key(65,0,0),Key(65,0,2),Key(17,0,2)]);Type(target,args.GetProperty("text").GetString()??"",token);}
            }
        }else if(action=="computer_press") {
            var pressed=new List<ushort>();
            try {
                foreach(var key in press){token.ThrowIfCancellationRequested();CheckFocus(target);pressed.Add(key);Send([Physical(key,false)]);}
                for(var elapsed=0;elapsed<duration;elapsed+=25){await Task.Delay(Math.Min(25,duration-elapsed),token);CheckFocus(target);}
            }finally {if(pressed.Count>0)Send(pressed.AsEnumerable().Reverse().Select(key=>Physical(key,true)).ToArray());}
        }else if(action=="computer_type") {
            Type(target,args.GetProperty("text").GetString()??"",token);
        }else {
            var key=(args.GetProperty("key").GetString()??"").ToUpperInvariant();bool control=key.StartsWith("CTRL+",StringComparison.Ordinal);var name=control?key[5..]:key;
            if(!control&&name is "A" or "C" or "V" or "Z")throw new InvalidOperationException("Use Ctrl with this letter.");
            ushort code=name switch {"ENTER"=>13,"TAB"=>9,"ESC"=>27,"BACKSPACE"=>8,"DELETE"=>46,"UP"=>38,"DOWN"=>40,"LEFT"=>37,"RIGHT"=>39,"A"=>65,"C"=>67,"V"=>86,"Z"=>90,_=>throw new InvalidOperationException("Use Enter, Tab, Esc, Backspace, Delete, arrows or Ctrl+A/C/V/Z.")};
            var input=new List<Input>();if(control)input.Add(Key(17,0,0));input.Add(Key(code,0,0));input.Add(Key(code,0,2));if(control)input.Add(Key(17,0,2));Send(input.ToArray());
        }
        snapshots.Remove(snapshot);return new{performed=true,title=target.Title,action=action.Replace("computer_",""),note="The input was sent. Inspect the window again to verify the result."};
    }
    static ushort[] PhysicalKey(string value)
    {
        var parts=value.Trim().ToUpperInvariant().Split('+');if(parts.Length is <1 or >3||parts.Distinct().Count()!=parts.Length)throw new InvalidOperationException("Use a key, optionally with Ctrl and Shift.");
        var keys=new List<ushort>();foreach(var modifier in parts[..^1])keys.Add(modifier switch{"CTRL"=>17,"SHIFT"=>16,_=>throw new InvalidOperationException("Only Ctrl and Shift modifiers are supported.")});
        var name=parts[^1];ushort code=name.Length==1&&char.IsAsciiLetterOrDigit(name[0])?(ushort)name[0]:(ushort)(name switch{"SPACE"=>32,"ENTER"=>13,"TAB"=>9,"ESC"=>27,"BACKSPACE"=>8,"DELETE"=>46,"UP"=>38,"DOWN"=>40,"LEFT"=>37,"RIGHT"=>39,"SHIFT"=>16,"CTRL"=>17,_ when name.Length is 2 or 3&&name[0]=='F'&&int.TryParse(name[1..],out var f)&&f is >=1 and <=12=>(ushort)(111+f),_=>throw new InvalidOperationException("Use A-Z, 0-9, F1-F12, Space, navigation keys, Ctrl or Shift. System shortcuts are not supported.")});
        if(name=="ESC"&&keys.Count>0)throw new InvalidOperationException("Modified Escape shortcuts are not supported.");
        keys.Add(code);return keys.ToArray();
    }
    static Input Physical(ushort key,bool up)=>Key(0,(ushort)MapVirtualKey(key,0),8u|(up?2u:0u)|(key is >=33 and <=46?1u:0u));
    [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code,uint mapType);
    internal static async Task Focus(nint hwnd,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();if(IsIconic(hwnd))ShowWindowAsync(hwnd,9);else ShowWindowAsync(hwnd,5);SetForegroundWindow(hwnd);
        for(var i=0;i<8;i++){token.ThrowIfCancellationRequested();if(GetForegroundWindow()==hwnd&&!IsIconic(hwnd))return;await Task.Delay(50,token);}
        var current=GetCurrentThreadId();var foregroundThread=GetWindowThreadProcessId(GetForegroundWindow(),out _);var attached=false;
        try {if(foregroundThread!=0&&foregroundThread!=current)attached=AttachThreadInput(current,foregroundThread,true);BringWindowToTop(hwnd);SetForegroundWindow(hwnd);}
        finally {if(attached)AttachThreadInput(current,foregroundThread,false);}
        for(var i=0;i<12;i++){token.ThrowIfCancellationRequested();if(GetForegroundWindow()==hwnd&&!IsIconic(hwnd))return;await Task.Delay(50,token);}
        throw new InvalidOperationException("Windows did not activate the selected app. Its window may be elevated, blocked by a modal dialog or on another desktop. No input was sent. Restore that app and inspect it again; do not try the Windows key or a different app.");
    }
    [DllImport("user32.dll")] static extern bool ShowWindowAsync(nint hwnd,int command);
    [DllImport("user32.dll")] static extern bool BringWindowToTop(nint hwnd);
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint from,uint to,bool attach);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    static void CheckFocus(Target target){if(GetForegroundWindow()!=target.Handle||AutomationElement.FocusedElement?.Current.IsPassword==true)throw new InvalidOperationException("The input target changed or requires password entry. Inspect again.");}
    static void Type(Target target,string text,CancellationToken token)
    {if(text.Length>8000)throw new InvalidOperationException("Type at most 8,000 characters per action.");foreach(var chunk in text.Chunk(100)){token.ThrowIfCancellationRequested();CheckFocus(target);Send(chunk.SelectMany(ch=>new[]{Key(0,ch,4),Key(0,ch,6)}).ToArray());}}
    async Task Move(PointNative end,nint window,CancellationToken token)
    {
        GetCursorPos(out var start);var steps=SystemParameters.ClientAreaAnimation?18:1;
        for(var i=1;i<=steps;i++){token.ThrowIfCancellationRequested();if(GetForegroundWindow()!=window)throw new InvalidOperationException("The active window changed. Inspect again.");var t=i/(double)steps;t=t*t*(3-2*t);SetCursorPos((int)(start.X+(end.X-start.X)*t),(int)(start.Y+(end.Y-start.Y)*t));session?.RedrawPointer();await Task.Delay(12,token);}
        if(GetAncestor(WindowFromPoint(end),2)!=window)throw new InvalidOperationException("Another window covers the target.");
    }
    [DllImport("user32.dll")] static extern bool GetCursorPos(out PointNative point);
    static Input Key(ushort key,ushort scan,uint flags)=>new(){Type=1,Data=new InputData{Keyboard=new KeyInput{Key=key,Scan=scan,Flags=flags}}};
    static void Send(Input[] input) {if(SendInput((uint)input.Length,input,Marshal.SizeOf<Input>())!=input.Length)throw new InvalidOperationException("Windows blocked or only partially applied the input. Inspect the app before retrying.");}
    [StructLayout(LayoutKind.Sequential)] struct RectNative {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)] struct PointNative {public int X,Y;}
    [StructLayout(LayoutKind.Sequential)] struct Input {public uint Type;public InputData Data;}
    [StructLayout(LayoutKind.Explicit)] struct InputData {[FieldOffset(0)] public MouseInput Mouse;[FieldOffset(0)] public KeyInput Keyboard;}
    [StructLayout(LayoutKind.Sequential)] struct MouseInput {public int X,Y;public uint Data,Flags,Time;public nuint Extra;}
    [StructLayout(LayoutKind.Sequential)] struct KeyInput {public ushort Key,Scan;public uint Flags,Time;public nuint Extra;}
    delegate bool EnumProc(nint hwnd,nint parameter);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback,nint parameter);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] static extern bool GetWindowRect(nint hwnd,out RectNative rect);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(nint hwnd,StringBuilder text,int max);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint hwnd,out uint pid);
    [DllImport("user32.dll")] static extern bool PrintWindow(nint hwnd,nint dc,uint flags);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")] static extern nint WindowFromPoint(PointNative point);
    [DllImport("user32.dll")] static extern nint GetAncestor(nint hwnd,uint flags);
    [DllImport("user32.dll",SetLastError=true)] static extern uint SendInput(uint count,Input[] input,int size);
}
