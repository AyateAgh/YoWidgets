using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Interop;
namespace YoWidgets;

internal sealed class ShellWindow:Window
{
    readonly Grid root=new(){Background=new SolidColorBrush(Color.FromRgb(8,8,8))};
    readonly ShellLifetime lifetime=new();readonly CancellationTokenSource stop=new();readonly DispatcherTimer expiry=new();
    readonly Dictionary<string,(string Path,string Hash)> imports=[];
    readonly Stopwatch startup=Stopwatch.StartNew();WebView2? view;CancellationTokenSource? subscription;bool ready,exit,draftDirty,pendingWrites;string profile="";DateTimeOffset maintained=DateTimeOffset.MinValue;
    internal ShellWindow()
    {
        Title="YoWidgets";Width=1180;Height=800;MinWidth=860;MinHeight=600;WindowStartupLocation=WindowStartupLocation.CenterScreen;WindowStyle=WindowStyle.None;Background=root.Background;
        System.Windows.Shell.WindowChrome.SetWindowChrome(this,new(){CaptionHeight=0,ResizeBorderThickness=new Thickness(6),GlassFrameThickness=new Thickness(0),CornerRadius=new CornerRadius(24)});
        Content=root;ShowBootstrap();SourceInitialized+=(_,_)=>{var source=(HwndSource)PresentationSource.FromVisual(this);source.AddHook(Hook);var dark=1;Native.DwmSetWindowAttribute(Native.Handle(this),20,ref dark,4);var corner=2;Native.DwmSetWindowAttribute(Native.Handle(this),33,ref corner,4);};
        Loaded+=async(_,_)=>await Initialize();Closing+=ClosingWindow;Closed+=(_,_)=>{stop.Cancel();subscription?.Cancel();view?.Dispose();expiry.Stop();Application.Current.Shutdown();};
        StateChanged+=(_,_)=>{if(WindowState==WindowState.Minimized)_=Sleep(false);else if(IsVisible&&ready)Wake();};
        expiry.Interval=TimeSpan.FromMinutes(15);expiry.Tick+=(_,_)=>{expiry.Stop();if(lifetime.ShouldExit(DateTimeOffset.UtcNow))EvictHost();};
    }
    void ShowBootstrap()
    {
        root.Children.Clear();var stack=Ui.Stack(Ui.Text("◯",46,weight:FontWeights.Light),Ui.Gap(16),Ui.Text("Yo",22,weight:FontWeights.SemiBold),Ui.Gap(10),Ui.Text("Opening your space",12,"Muted"));stack.HorizontalAlignment=HorizontalAlignment.Center;stack.VerticalAlignment=VerticalAlignment.Center;root.Children.Add(stack);
    }
    async Task Initialize()
    {
        try
        {
            _=CoreWebView2Environment.GetAvailableBrowserVersionString();
            var assets=ShellAssets.Ensure();profile=Path.Combine(App.OwnedDirectory("Cache"),"UI");
            view=new WebView2{DefaultBackgroundColor=System.Drawing.Color.FromArgb(255,8,8,8),Visibility=Visibility.Hidden,Margin=new Thickness(6)};root.Children.Add(view);
            var environment=await CoreWebView2Environment.CreateAsync(null,profile);await view.EnsureCoreWebView2Async(environment);
            var core=view.CoreWebView2;core.SetVirtualHostNameToFolderMapping("yowidgets-ui.invalid",assets,CoreWebView2HostResourceAccessKind.DenyCors);
            core.Settings.AreDevToolsEnabled=false;core.Settings.AreDefaultContextMenusEnabled=false;core.Settings.AreDefaultScriptDialogsEnabled=false;core.Settings.IsStatusBarEnabled=false;core.Settings.IsZoomControlEnabled=false;
            core.Profile.PreferredColorScheme=CoreWebView2PreferredColorScheme.Dark;
            core.Profile.IsPasswordAutosaveEnabled=false;core.Profile.IsGeneralAutofillEnabled=false;
            core.NavigationStarting+=(_,e)=>{if(!e.Uri.StartsWith("https://yowidgets-ui.invalid/",StringComparison.Ordinal))e.Cancel=true;};
            core.NewWindowRequested+=(_,e)=>e.Handled=true;core.PermissionRequested+=(_,e)=>e.State=CoreWebView2PermissionState.Deny;core.DownloadStarting+=(_,e)=>e.Cancel=true;
            core.WebMessageReceived+=Message;core.ProcessFailed+=(_,_)=>{ready=false;subscription?.Cancel();ShowRecovery("The interface stopped responding. Your desktop widgets are still running.");};
            await ManagementClient.Request(App.DataRoot,"host.register",new{window=Native.Handle(this).ToInt64()},stop.Token);
            core.Navigate("https://yowidgets-ui.invalid/index.html");
        }
        catch(Exception e){App.Log("shell-init",e);ShowRecovery(e is WebView2RuntimeNotFoundException?"Microsoft WebView2 Runtime is required to open the catalog. Your widgets can keep running.":"The interface could not open. Your widgets are still running.");}
    }
    async void Message(object? sender,CoreWebView2WebMessageReceivedEventArgs e)
    {
        if(!e.Source.StartsWith("https://yowidgets-ui.invalid/",StringComparison.Ordinal))return;
        string id="";
        try
        {
            using var document=JsonDocument.Parse(e.WebMessageAsJson);var value=document.RootElement;if(e.WebMessageAsJson.Length>65536)throw new InvalidOperationException("This request is too large.");
            id=PlatformSettings.Text(value,"id");var method=PlatformSettings.Text(value,"method");var args=value.TryGetProperty("args",out var a)?a.Clone():JsonSerializer.SerializeToElement(new{});
            if(method=="ui.ready")
            {
                ready=true;lifetime.Show();foreach(var child in root.Children.Cast<UIElement>().Where(c=>!ReferenceEquals(c,view)).ToArray())root.Children.Remove(child);view!.Visibility=Visibility.Visible;
                Reply(id,new{ready=true,viewState=ReadViewState()});Subscribe();App.Trace("shell-ready",$"firstReadyMs={startup.Elapsed.TotalMilliseconds:F1}; pid={Environment.ProcessId}");return;
            }
            if(method=="window.close"){if(args.TryGetProperty("discard",out var discard)&&discard.GetBoolean())draftDirty=false;if(draftDirty){Post(new{type="closeRequested"});Reply(id,new{accepted=true});return;}Reply(id,new{accepted=true});await Sleep(true);return;}
            if(method=="window.minimize"){WindowState=WindowState.Minimized;Reply(id,new{accepted=true});return;}
            if(method=="window.maximize"){WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;Reply(id,new{accepted=true});return;}
            if(method=="window.drag"){if(GetAsyncKeyState(1)<0){try{DragMove();}catch(InvalidOperationException){}}Reply(id,new{accepted=true});return;}
            if(method=="ui.persist"){SaveViewState(args);Reply(id,new{accepted=true});return;}
            if(method=="ui.dirty"){draftDirty=args.TryGetProperty("dirty",out var dirty)&&dirty.GetBoolean();Reply(id,new{accepted=true});return;}
            if(method=="ui.saving"){pendingWrites=args.TryGetProperty("saving",out var saving)&&saving.GetBoolean();Reply(id,new{accepted=true});if(!pendingWrites&&(!IsVisible||WindowState==WindowState.Minimized))_=Sleep(false);return;}
            if(method=="ui.import.pick"){
                var picker=new Microsoft.Win32.OpenFileDialog{Title="Import widget package",Filter="YoWidgets package (*.zip)|*.zip",CheckFileExists=true,Multiselect=false};if(picker.ShowDialog(this)!=true){Reply(id,new{cancelled=true});return;}
                var path=picker.FileName;var metadata=await Task.Run(()=>{using var bundle=new LocalContentBundle(path);return new{hash=bundle.Hash,items=bundle.Catalog.Items.Where(i=>i.Type=="widget").Select(i=>new{i.Title,i.Kind,i.Pack,i.Author}).ToArray(),bytes=bundle.Bytes};});var ticket=Guid.NewGuid().ToString("N");imports.Clear();imports[ticket]=(path,metadata.hash);Reply(id,new{ticket,metadata.items,metadata.bytes});return;
            }
            if(method=="ui.import.install"){
                var ticket=PlatformSettings.Text(args,"ticket");if(!imports.Remove(ticket,out var selection))throw new InvalidOperationException("Choose the package again.");var result=await ManagementClient.Request(App.DataRoot,"content.import",new{path=selection.Path,hash=selection.Hash},stop.Token);if(!result.GetProperty("ok").GetBoolean())throw new InvalidOperationException("The import could not start.");Reply(id,result.GetProperty("result"));return;
            }
            if(method=="ui.clearCache"){await view!.CoreWebView2.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.DiskCache|CoreWebView2BrowsingDataKinds.CacheStorage);Reply(id,new{accepted=true});return;}
            if(method=="ui.external")
            {
                var target=PlatformSettings.Text(args,"target");var url=target switch{"github"=>"https://github.com/AyateAgh/YoWidgets","webview"=>"https://developer.microsoft.com/microsoft-edge/webview2/","privacy"=>"https://github.com/AyateAgh/YoWidgets/blob/main/PRIVACY.md",_=>throw new InvalidOperationException("Unknown external link.")};
                Process.Start(new ProcessStartInfo(url){UseShellExecute=true});Reply(id,new{accepted=true});return;
            }
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);timeout.CancelAfter(TimeSpan.FromSeconds(30));
            if(method is "widget.add" or "content.visibility")
            {
                var obj=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(args.GetRawText())??[];obj["window"]=JsonSerializer.SerializeToElement(Native.Handle(this).ToInt64());args=JsonSerializer.SerializeToElement(obj);
            }
            var response=await ManagementClient.Request(App.DataRoot,method,args,timeout.Token);
            if(response.GetProperty("ok").GetBoolean())Reply(id,response.GetProperty("result"));else Error(id,response.GetProperty("error").GetString()??"This action failed.");
        }
        catch(Exception ex){Error(id,ex is InvalidOperationException or InvalidDataException?ex.Message:ex is OperationCanceledException?"The action timed out. Your desktop stays unchanged.":"The native connection is unavailable. Reopen YoWidgets.");}
    }
    void Reply(string id,object result)=>Post(new{id,ok=true,result});
    void Error(string id,string message)=>Post(new{id,ok=false,error=message});
    void Post(object value){if(!ready||view?.CoreWebView2==null)return;view.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(value,CatalogJson.Options));}
    void Subscribe()
    {
        subscription?.Cancel();subscription?.Dispose();subscription=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);var token=subscription.Token;
        _=Task.Run(async()=>{
            try{await ManagementClient.Subscribe(App.DataRoot,snapshot=>Dispatcher.BeginInvoke(()=>{if(!token.IsCancellationRequested&&ready&&lifetime.HiddenAt==null)Post(snapshot);}),token);}
            catch(OperationCanceledException){}
            catch(Exception e)when(e is IOException or TimeoutException){if(!token.IsCancellationRequested)await Dispatcher.InvokeAsync(()=>Post(new{type="connectionError",error="The native connection was interrupted. Reopen YoWidgets."}));}
        },token);
    }
    async Task Sleep(bool hide)
    {
        if(pendingWrites){if(hide)Hide();expiry.Stop();return;}
        if(exit)return;var generation=lifetime.Hide(DateTimeOffset.UtcNow);subscription?.Cancel();expiry.Stop();if(!draftDirty)expiry.Start();
        if(hide)Hide();if(view?.CoreWebView2==null){EvictHost();return;}
        var core=view.CoreWebView2;
        try
        {
            // Cleanup precedes suspension; native downloads are never cancelled here.
            await core.ExecuteScriptAsync("window.dispatchEvent(new Event('yo:suspend'));document.querySelectorAll('video,audio').forEach(x=>x.pause());").WaitAsync(TimeSpan.FromSeconds(2));
            if(!lifetime.CanSuspend(generation))return;view.Visibility=Visibility.Hidden;
            if(DateTimeOffset.UtcNow-maintained>TimeSpan.FromHours(1)&&ShellAssets.ProfileNeedsMaintenance(profile))
            {maintained=DateTimeOffset.UtcNow;await core.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.DiskCache|CoreWebView2BrowsingDataKinds.CacheStorage).WaitAsync(TimeSpan.FromSeconds(3));}
            if(!lifetime.CanSuspend(generation))return;
            var suspended=await Suspend(core,generation);
            if(!lifetime.CanSuspend(generation))return;
            if(!suspended)
            {
                await core.ExecuteScriptAsync("window.dispatchEvent(new Event('yo:suspend'));");await Task.Delay(TimeSpan.FromSeconds(5),stop.Token);
                if(!lifetime.CanSuspend(generation))return;suspended=await Suspend(core,generation);
                if(!suspended&&lifetime.CanSuspend(generation))EvictHost();
            }
            App.Trace("shell-suspend",$"success={suspended}; privateBytes={Process.GetCurrentProcess().PrivateMemorySize64}");
        }
        catch(Exception e)when(e is COMException or InvalidOperationException or TimeoutException or OperationCanceledException){if(lifetime.CanSuspend(generation))EvictHost();}
    }
    async Task<bool> Suspend(CoreWebView2 core,int generation)
    {
        var attempt=core.TrySuspendAsync();
        _=attempt.ContinueWith(_=>{if(!exit&&view?.CoreWebView2==core&&!lifetime.CanSuspend(generation)&&IsVisible)core.Resume();},CancellationToken.None,TaskContinuationOptions.None,TaskScheduler.FromCurrentSynchronizationContext());
        return await attempt.WaitAsync(TimeSpan.FromSeconds(5));
    }
    void Wake()
    {
        lifetime.Show();expiry.Stop();if(!IsVisible)Show();if(WindowState==WindowState.Minimized)WindowState=WindowState.Normal;
        if(view?.CoreWebView2 is {} core&&ready){core.Resume();view.Visibility=Visibility.Visible;Post(new{type="resume"});Subscribe();}
        Activate();
    }
    void ClosingWindow(object? sender,CancelEventArgs e){if(exit)return;e.Cancel=true;if(draftDirty)Post(new{type="closeRequested"});else _=Sleep(true);}
    void EvictHost(){if(!pendingWrites&&lifetime.CanEvict(IsVisible&&WindowState!=WindowState.Minimized,draftDirty))ShutdownHost();}
    internal void ShutdownHost(){if(exit)return;exit=true;expiry.Stop();subscription?.Cancel();view?.Dispose();view=null;Close();}
    IntPtr Hook(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
    {
        if(message==ShellController.ShowMessage){handled=true;Wake();}else if(message==ShellController.ExitMessage){handled=true;ShutdownHost();}else if(message==ShellController.EvictMessage){handled=true;EvictHost();}return IntPtr.Zero;
    }
    object ReadViewState()
    {
        try{return JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(App.DataRoot,"ui-state.json")))??new(){["page"]="home"};}catch(Exception e)when(e is IOException or JsonException){return new{page="home"};}
    }
    void SaveViewState(JsonElement value)
    {
        var allowed=new HashSet<string>{"page","query","sort","category","kind","pack","status","scroll","library"};var safe=new Dictionary<string,object>();
        foreach(var property in value.EnumerateObject())if(allowed.Contains(property.Name)&&property.Value.ValueKind==JsonValueKind.String&&property.Value.GetString() is {} text&&text.Length<2048)safe[property.Name]=text;
        var file=Path.Combine(App.DataRoot,"ui-state.json");File.WriteAllText(file+".tmp",JsonSerializer.Serialize(safe));File.Move(file+".tmp",file,true);
    }
    void ShowRecovery(string message)
    {
        view?.Dispose();view=null;root.Children.Clear();var body=Ui.Stack(Ui.Text("Yo",32,weight:FontWeights.SemiBold),Ui.Gap(20),Ui.Text(message,14,"Muted"),Ui.Gap(22));body.MaxWidth=430;body.HorizontalAlignment=HorizontalAlignment.Center;body.VerticalAlignment=VerticalAlignment.Center;
        var retry=Ui.Button("Try again",async()=>{ShowBootstrap();await Initialize();},true);body.Children.Add(retry);body.Children.Add(Ui.Gap(10));
        body.Children.Add(Ui.Button("Install Microsoft WebView2",async()=>{try{retry.IsEnabled=false;await RuntimeRecovery.Install();ShowBootstrap();await Initialize();}catch(Exception e){App.Log("runtime-install",e);ShowRecovery("WebView2 could not be installed. Check your connection or your administrator's policy, then retry.");}finally{retry.IsEnabled=true;}}));
        body.Children.Add(Ui.Gap(10));body.Children.Add(Ui.Button("Close",ShutdownHost));root.Children.Add(body);
    }
    [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
}
