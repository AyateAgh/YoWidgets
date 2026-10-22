using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using YoWidgets;
using YoWidgets.Core;

internal static class Program
{
    static int total,failed;
    static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static async Task Test(string name,Func<Task> run){total++;try{await run();Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
    static JsonElement Args(object value)=>JsonSerializer.SerializeToElement(value,CatalogJson.Options);
    static int CheckFocusAppAudio()
    {
        var folder=AppContext.BaseDirectory;var source=Environment.ProcessPath!;var children=new List<System.Diagnostics.Process>();
        System.Diagnostics.Process Launch(string name){var path=Path.Combine(folder,name+".exe");File.Copy(source,path,true);var process=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path,"--focus-audio-fixture"){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,CreateNoWindow=true})!;children.Add(process);Check(process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(8)).GetAwaiter().GetResult()=="ready","Audio fixture did not open");return process;}
        try{
            Launch("YoFocusMusicFixture");var musicBefore=FocusAppAudio.Read(["YoFocusMusicFixture"]);Check(musicBefore.Length>0,"Music fixture has no audio session");
            using var change=new ManualResetEventSlim();using var watch=FocusAppAudio.Watch(()=>change.Set());Check(change.Wait(5000),"Audio watch did not initialize");change.Reset();
            Launch("YoFocusChatFixture");Check(change.Wait(5000),"New audio session did not notify Focus");var retained=new Dictionary<string,object>();var chat=FocusAppAudio.Read(["YoFocusChatFixture"],retained);Check(chat.Length>0,"Call fixture has no audio session");
            try{foreach(var state in chat)FocusAppAudio.SetMute(state.Device,true,retained);Check(FocusAppAudio.Read(["YoFocusChatFixture"]).All(state=>state.Muted),"Call app session was not muted");var musicAfter=FocusAppAudio.Read(["YoFocusMusicFixture"]);Check(musicBefore.All(before=>musicAfter.Any(after=>after.Device==before.Device&&after.Muted==before.Muted)),"Call quieting changed music");Console.WriteLine("PASS actual per-app mute leaves the separate music session unchanged");}
            finally{var exiting=children.Last();exiting.StandardInput.WriteLine("quit");Check(exiting.WaitForExit(5000),"Chat did not exit during Focus");foreach(var state in chat)FocusAppAudio.SetMute(state.Device,state.Muted,retained);foreach(var control in retained.Values)System.Runtime.InteropServices.Marshal.ReleaseComObject(control);retained.Clear();}
            Launch("YoFocusChatFixture");
            Check(FocusAppAudio.Read(["YoFocusChatFixture"]).All(after=>chat.Any(before=>after.Muted==before.Muted)),"Original call app mute state not restored after exit and relaunch");Console.WriteLine("PASS native call app state restore and new-session notification");return 0;
        }finally{foreach(var child in children){try{if(!child.HasExited){child.StandardInput.WriteLine("quit");if(!child.WaitForExit(5000))child.Kill();}}finally{child.Dispose();}}}
    }
    [STAThread]static int Main(string[] args)
    {
        if(args.Contains("--focus-audio-fixture")){using var speaker=new PcmSpeaker(System.Windows.Threading.Dispatcher.CurrentDispatcher);speaker.Write(new byte[48000*20]);Console.WriteLine("ready");Console.ReadLine();return 0;}
        if(args.Contains("--focus-app-audio-check"))return CheckFocusAppAudio();
        if(args.Contains("--focus-native-check")){
            var environment=new WindowsFocusEnvironment();var original=environment.Profile;
            try{environment.Profile=FocusSession.Quiet;Check(environment.Profile==FocusSession.Quiet,"Windows did not apply DND");Console.WriteLine("PASS native Windows Do Not Disturb set/read");_=environment.Audio();Console.WriteLine("PASS native communication audio session enumeration (no audio mutation)");}
            finally{environment.Profile=original;Check(environment.Profile==original,"Windows did not restore DND");Console.WriteLine("PASS original Windows Do Not Disturb restored");}return 0;
        }
        var root=Path.Combine(Path.GetTempPath(),"YoPlatformNative-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        typeof(App).GetProperty(nameof(App.DataRoot),BindingFlags.Static|BindingFlags.Public)!.SetValue(null,root);
        var app=new App{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        if(args.Contains("--widget-material-fixture")){Console.WriteLine(root);app.Startup+=(_,_)=>WidgetMaterialFixture.Show(app,root);app.Run();return 0;}
        app.Startup+=async(_,_)=>{
            Ui.Theme();using var manager=new WidgetManager(root,true);using var platform=new DesktopPlatform(manager,root,false);using var shell=new ShellController(platform);using var server=new ManagementServer(platform,shell,root,app.Dispatcher);
            try
            {
                await Test("management snapshot never exposes saved or decrypted keys",async()=>{
                    await platform.Execute("settings.chat.save",Args(new{provider="OpenAI",model="gpt-test",key="test-only"}),CancellationToken.None);
                    var json=JsonSerializer.Serialize(platform.Snapshot(),CatalogJson.Options);
                    Check(!json.Contains("test-only")&&!json.Contains(manager.Settings.AiKeyProtected!),"A saved secret entered the UI snapshot");
                });
                await Test("switching services cannot reuse another provider's credential",async()=>{
                    var rejected=false;try{await platform.Execute("settings.chat.save",Args(new{provider="Gemini",model="gemini-test",key=""}),CancellationToken.None);}catch(InvalidOperationException){rejected=true;}
                    Check(rejected,"An unrelated provider key was silently reused");Check(manager.Settings.AiProvider=="OpenAI","Rejected change was not rolled back");
                });
                await Test("saving preferences in a new profile cannot trigger legacy pack installation on restart",()=>{var legacyCandidate=File.Exists(Path.Combine(root,"settings.json"))&&!File.Exists(Path.Combine(root,"Content","installed.json"));Check(!legacyCandidate&&platform.Content.Installed.Count==0,"An empty modern profile would be migrated as legacy after saving settings");return Task.CompletedTask;});
                await Test("native pipe responds while a UI event subscription is connected",async()=>{
                    using var stop=new CancellationTokenSource();var count=0;var subscription=ManagementClient.Subscribe(root,_=>Interlocked.Increment(ref count),stop.Token);
                    var result=await ManagementClient.Request(root,"settings.general",new{city="Lviv",hour24=false},CancellationToken.None);
                    Check(result.GetProperty("ok").GetBoolean(),"Preferences failed with a subscriber connected");Check(manager.Settings.City=="Lviv","Native settings were not saved");
                    await Task.Delay(100);Check(count>0,"No initial/current snapshot was delivered");stop.Cancel();try{await subscription;}catch(OperationCanceledException){}
                });
                await Test("reconnected UI receives the latest local state",async()=>{
                    var result=await ManagementClient.Request(root,"state",new{},CancellationToken.None);var state=result.GetProperty("result");
                    Check(state.GetProperty("settings").GetProperty("city").GetString()=="Lviv","Reconnection restored stale state");Check(state.GetProperty("installed").GetArrayLength()==0,"Clean native profile auto-installed content");
                });
                await Test("malformed settings do not replace previous preferences",async()=>{
                    try{await platform.Execute("settings.general",Args(new{city="",interfaceVolume=2}),CancellationToken.None);}catch(InvalidOperationException){}
                    Check(manager.Settings.City=="Lviv","Invalid form lost the previous city");Check(manager.Settings.InterfaceVolume==.12,"Invalid sound volume was saved");
                });
                await Test("real package resources enable independent widget instances",async()=>{
                    var repository=Directory.GetCurrentDirectory();var catalog=JsonSerializer.Deserialize<CatalogDocument>(File.ReadAllText(Path.Combine(repository,"catalog","v1","catalog.json")),CatalogJson.Options)!;
                    await platform.Content.InstallAsync(catalog,"glass-clock",(p,ct)=>Task.FromResult<Stream>(File.OpenRead(Path.Combine(repository,"catalog","v1","packages",p.Id+".zip"))),null,CancellationToken.None);
                    await platform.Execute("widget.add",Args(new{id="glass-clock"}),CancellationToken.None);await platform.Execute("widget.add",Args(new{id="glass-clock"}),CancellationToken.None);
                    var first=manager.Settings.Widgets[0];var second=manager.Settings.Widgets[1];manager.Disable(first);await platform.Execute("widget.enable",Args(new{id=first.Id}),CancellationToken.None);
                    Check(first.Enabled&&second.Enabled,"Showing one instance disabled the other");
                    await platform.Execute("widget.remove",Args(new{id=first.Id}),CancellationToken.None);Check(manager.Settings.Widgets.Count==1,"Removing an instance removed the whole content item");Check(manager.ActiveCount==1,"Removing one instance increased or corrupted the desktop count");Check(platform.Content.IsInstalled("glass-clock"),"Removing an instance uninstalled its resources");
                });
                await Test("failed removal preserves active instances during installation",async()=>{
                    var catalog=JsonSerializer.Deserialize<CatalogDocument>(File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(),"catalog","v1","catalog.json")),CatalogJson.Options)!;
                    var active=manager.Settings.Widgets.Single();var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);using var stop=new CancellationTokenSource();
                    var pending=platform.Content.InstallAsync(catalog,"glass-music",async(p,ct)=>{started.SetResult();await Task.Delay(Timeout.Infinite,ct);return Stream.Null;},null,stop.Token);
                    await started.Task;try{var rejected=false;try{await platform.Execute("content.remove",Args(new{id="glass-clock"}),CancellationToken.None);}catch(InvalidOperationException){rejected=true;}Check(rejected&&active.Enabled&&platform.Content.IsReady("glass-clock"),"Rejected removal changed the active desktop");}finally{stop.Cancel();try{await pending;}catch(OperationCanceledException){}}
                });
                await Test("null saved catalog falls back to the packaged seed",()=>{
                    File.WriteAllText(Path.Combine(root,"catalog-cache.json"),"null");using var recovered=new DesktopPlatform(manager,root,false);var snapshot=JsonSerializer.SerializeToElement(recovered.Snapshot(),CatalogJson.Options);Check(snapshot.GetProperty("catalog").GetProperty("items").GetArrayLength()>0,"No catalog seed recovered");return Task.CompletedTask;
                });

                await Test("Library visibility restores exactly the saved instance group",async()=>{
                    var existing=manager.Settings.Widgets.Single();var x=existing.X;var y=existing.Y;
                    await platform.Execute("widget.add",Args(new{id="glass-clock"}),CancellationToken.None);var hidden=manager.Settings.Widgets.Last();manager.Disable(hidden);
                    await platform.Execute("content.visibility",Args(new{id="glass-clock",enabled=false}),CancellationToken.None);Check(!existing.Enabled&&!hidden.Enabled,"Hidden group remained active");
                    await platform.Execute("content.visibility",Args(new{id="glass-clock",enabled=true}),CancellationToken.None);Check(existing.Enabled&&!hidden.Enabled,"Visibility enabled an unrelated hidden instance");Check(existing.X==x&&existing.Y==y,"Restored instance moved");
                });

                await Test("accent preferences persist and reject unsupported color modes",async()=>{
                    await platform.Execute("settings.general",Args(new{interfaceAccent="violet"}),CancellationToken.None);Check(manager.Settings.InterfaceAccent=="violet","Accent was not saved");var rejected=false;try{await platform.Execute("settings.general",Args(new{interfaceAccent="injected-color"}),CancellationToken.None);}catch(InvalidOperationException){rejected=true;}Check(rejected&&manager.Settings.InterfaceAccent=="violet","Invalid accent replaced the saved choice");
                });
                await Test("management appearance persists light, custom gradient and transparency atomically",async()=>{
                    await platform.Execute("settings.general",Args(new{appearance=new{mode="custom",colors=new[]{"#ffffff","#000000"},transparent=true}}),CancellationToken.None);
                    var saved=Args(platform.Snapshot()).GetProperty("settings").GetProperty("appearance");Check(saved.GetProperty("mode").GetString()=="custom"&&saved.GetProperty("transparent").GetBoolean(),"Appearance was not saved");Check(saved.GetProperty("colors").GetArrayLength()==2,"Gradient was lost");
                    var original=JsonSerializer.Serialize(manager.Settings);foreach(var bad in new[]{new{mode="custom",colors=new[]{"url(https://invalid)"},transparent=false},new{mode="unknown",colors=new[]{"#FFFFFF"},transparent=false},new{mode="light",colors=Array.Empty<string>(),transparent=false}}){var rejected=false;try{await platform.Execute("settings.general",Args(new{appearance=bad}),CancellationToken.None);}catch(InvalidOperationException){rejected=true;}Check(rejected&&JsonSerializer.Serialize(manager.Settings)==original,"Invalid appearance changed preferences");}
                    manager.Settings.Normalize();Check(Args(platform.Snapshot()).GetProperty("settings").GetProperty("appearance").GetProperty("mode").GetString()=="custom","Normalization reset the saved theme");
                });
                await Test("approximate city parsing never exposes IP or coordinates",()=>{Check(ApproximateCity.Parse(Args(new{city="Lviv",ip="private-fixture",latitude=50}))=="Lviv","City response was not minimized");var rejected=false;try{ApproximateCity.Parse(Args(new{error=true,city=""}));}catch(InvalidOperationException){rejected=true;}Check(rejected,"An error response was accepted as a city");return Task.CompletedTask;});
                await Test("native management maximize and interrupted restore stay inside work area",async()=>{foreach(var bad in new string?[]{null,"null","{}","{\"bounds\":null}","{\"bounds\":{\"x\":1e20,\"y\":0,\"width\":800,\"height\":600}}"})Check(ShellWindowLayout.ParseSaved(bad)==null,"Malformed saved geometry was accepted");var fixture=new Window{Width=720,Height=480,WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=System.Windows.Media.Brushes.Black,ShowInTaskbar=false};System.Windows.Shell.WindowChrome.SetWindowChrome(fixture,new(){CaptionHeight=0,ResizeBorderThickness=new Thickness(6),GlassFrameThickness=new Thickness(0),CornerRadius=new CornerRadius(24)});ShellWindowPlacement? savedPlacement=null;using var layout=new ShellWindowLayout(fixture,_=>{},persist:p=>savedPlacement=p);fixture.Show();var start=Native.Bounds(fixture);var resizeEvents=0;SizeChangedEventHandler countResize=(_,_)=>resizeEvents++;fixture.SizeChanged+=countResize;var samples=new System.Collections.Generic.List<RectD>();EventHandler sample=(_,_)=>samples.Add(Native.Bounds(fixture));System.Windows.Media.CompositionTarget.Rendering+=sample;try{await layout.ToggleAsync();}finally{System.Windows.Media.CompositionTarget.Rendering-=sample;fixture.SizeChanged-=countResize;}for(var index=1;index<samples.Count;index++){Check(samples[index].X<=samples[index-1].X+1&&samples[index].Y<=samples[index-1].Y+1&&samples[index].Width>=samples[index-1].Width-1&&samples[index].Height>=samples[index-1].Height-1,"Maximize geometry changed direction mid-flight");}var maximized=Native.Bounds(fixture);var work=System.Windows.Forms.Screen.FromHandle(Native.Handle(fixture)).WorkingArea;Check(maximized.X>=work.Left&&maximized.Y>=work.Top&&maximized.X+maximized.Width<=work.Right&&maximized.Y+maximized.Height<=work.Bottom,"Maximize covers the taskbar");await layout.ToggleAsync();var restored=Native.Bounds(fixture);Check(Math.Abs(restored.Width-start.Width)<3&&Math.Abs(restored.Height-start.Height)<3,"Restore lost normal bounds");await layout.ToggleAsync();fixture.WindowState=WindowState.Minimized;layout.Save();Check(savedPlacement?.Maximized==true&&layout.RestoreState==WindowState.Maximized,"Minimize forgot maximization");fixture.WindowState=layout.RestoreState;Check(fixture.WindowState==WindowState.Maximized,"Minimize restored to wrong state");fixture.WindowState=WindowState.Normal;var osRestore=Native.Bounds(fixture);Check(Math.Abs(osRestore.Width-start.Width)<3&&Math.Abs(osRestore.Height-start.Height)<3,"OS restore lost its normal bounds");var first=layout.ToggleAsync();var second=layout.ToggleAsync();await Task.WhenAll(first,second);Check(!layout.IsAnimating&&fixture.WindowState==WindowState.Normal,"Rapid toggle left an active animation or wrong state");fixture.Close();Check(resizeEvents<=4,"Live interface was resized on animation frames: "+resizeEvents);});
                await Test("local retries retain native source and history clearing preserves installed content",async()=>{
                    var privatePath=Path.Combine(root,"private-source-not-present.zip");var response=Args(await platform.Execute("content.import",Args(new{path=privatePath,hash=new string('a',64)}),CancellationToken.None));var first=response.GetProperty("jobId").GetString()!;
                    async Task Wait(string id){for(var i=0;i<100;i++){var state=Args(platform.Snapshot());if(state.GetProperty("jobs").EnumerateArray().Any(j=>j.GetProperty("id").GetString()==id&&j.GetProperty("stage").GetString()=="failed"))return;await Task.Delay(10);}throw new Exception("Import did not finish");}
                    await Wait(first);var retry=Args(await platform.Execute("content.retry",Args(new{id=first}),CancellationToken.None)).GetProperty("jobId").GetString()!;Check(retry!=first,"Retry reused the failed job");await Wait(retry);Check(!JsonSerializer.Serialize(platform.Snapshot(),CatalogJson.Options).Contains("private-source-not-present"),"A local path entered the UI snapshot");await platform.Execute("content.clearFinished",Args(new{}),CancellationToken.None);Check(Args(platform.Snapshot()).GetProperty("jobs").GetArrayLength()==0,"Finished history was not cleared");Check(platform.Content.IsReady("glass-clock")&&manager.Settings.Widgets.Any(w=>w.Enabled),"Clearing history removed desktop content");
                });

                await Test("snapshot animation keeps its HWND fixed and releases its passive overlay",async()=>{
                    var fixture=new Window{Width=720,Height=480,WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(1,0,0,0)),ShowInTaskbar=false};fixture.Show();
                    try{
                        var bitmap=new System.Windows.Media.Imaging.WriteableBitmap(64,64,96,96,System.Windows.Media.PixelFormats.Pbgra32,null);bitmap.Freeze();var from=Native.Bounds(fixture);var area=System.Windows.Forms.Screen.FromHandle(Native.Handle(fixture)).WorkingArea;var to=new RectD(area.Left,area.Top,area.Width,area.Height);
                        using(var overlay=new ShellSnapshotWindow(fixture,bitmap,from,to)){
                            await overlay.Present(CancellationToken.None);var bounds=Native.Bounds(overlay);var moved=false;EventHandler sample=(_,_)=>{if(Native.Bounds(overlay)!=bounds)moved=true;};System.Windows.Media.CompositionTarget.Rendering+=sample;
                            try{var styles=Native.GetStyle(Native.Handle(overlay),-20).ToInt64();Check((styles&0x20)!=0&&(styles&0x8000000)!=0,"Snapshot can steal focus or input");await overlay.Move(CancellationToken.None);await overlay.Reveal(CancellationToken.None);}finally{System.Windows.Media.CompositionTarget.Rendering-=sample;}
                            Check(!moved,"Snapshot HWND was resized on animation frames");
                        }
                        Check(!app.Windows.Cast<Window>().Any(w=>w is ShellSnapshotWindow),"Snapshot overlay leaked");
                        using var stop=new CancellationTokenSource();using(var cancelled=new ShellSnapshotWindow(fixture,bitmap,from,to)){await cancelled.Present(CancellationToken.None);var moving=cancelled.Move(stop.Token);stop.Cancel();try{await moving;}catch(OperationCanceledException){}}
                        Check(!app.Windows.Cast<Window>().Any(w=>w is ShellSnapshotWindow),"Cancelled snapshot overlay leaked");
                    }finally{fixture.Close();}
                });
                await Test("a failed optional visual cannot poison later window transitions",async()=>{
                    var fixture=new Window{Width=720,Height=480,WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=System.Windows.Media.Brushes.Black,ShowInTaskbar=false};var calls=0;
                    using var layout=new ShellWindowLayout(fixture,_=>{},transition:(_,_,commit,_)=>{calls++;if(calls==1)throw new InvalidOperationException("Fixture render failure");commit();return Task.FromResult(true);});fixture.Show();
                    try{await layout.ToggleAsync();Check(fixture.WindowState==WindowState.Maximized,"Render failure prevented geometry fallback");await layout.ToggleAsync();Check(fixture.WindowState==WindowState.Normal&&!layout.IsAnimating,"Previous render failure poisoned restore");}finally{fixture.Close();}
                });
                await Test("widget action pills never reuse a parented label",()=>{foreach(var label in new[]{"Choose photo","Refresh","Wi-Fi","DND","Sound / mute"}){var button=WidgetVisuals.Pill(label,()=>{});Check(button.Content is FrameworkElement,"Missing pill content");}return Task.CompletedTask;});
                await Test("every installed native widget can construct without changing system settings",()=>{foreach(var pack in new[]{"glass","liquid"})foreach(var kind in new[]{"photo","focus","quick","notifications"}){var content=WidgetContentFactory.Create(manager,new(){Kind=kind,Pack=pack},preview:true);Check(content!=null,"Missing widget "+kind);}return Task.CompletedTask;});
                await Test("Controls slider thumb drag changes value continuously",()=>{
                    var slider=new System.Windows.Controls.Slider{Minimum=0,Maximum=100,Value=40,Width=300,Height=32};slider.SetResourceReference(FrameworkElement.StyleProperty,"ControlCenterSlider");
                    var host=new Window{Content=slider,Width=340,Height=90,ShowInTaskbar=false};host.Show();host.UpdateLayout();
                    try{var track=(System.Windows.Controls.Primitives.Track)slider.Template.FindName("PART_Track",slider);var before=slider.Value;track.Thumb.RaiseEvent(new System.Windows.Controls.Primitives.DragDeltaEventArgs(60,0));Check(slider.Value>before+10,"Dragging the thumb did not move the slider value");}finally{host.Close();}return Task.CompletedTask;
                });
                await Test("Focus and Controls inherit transparent pack surfaces and adaptive ink",()=>{
                    foreach(var pack in new[]{"glass","liquid"})foreach(var kind in new[]{"focus","quick"}){var content=(System.Windows.Controls.Border)WidgetContentFactory.Create(manager,new(){Kind=kind,Pack=pack},preview:true);Check(content.Background==null||content.Background==System.Windows.Media.Brushes.Transparent,"Opaque inner panel on "+kind);Check(!content.Resources.Contains("WidgetInk"),"Widget overrides adaptive ink on "+kind);}return Task.CompletedTask;
                });
                await Test("photo corner resizing anchors opposite edges and survives restart",()=>{
                    var folder=Path.Combine(root,"photo-resize");Directory.CreateDirectory(folder);using var owner=new WidgetManager(folder,true,focusEnvironment:new FakeFocusEnvironment());var state=new WidgetState{Kind="photo",Enabled=true,PhotoWidth=420,PhotoHeight=280};owner.Settings.Widgets.Add(state);
                    var photo=new WidgetWindow(owner,state,true);photo.Show();photo.UpdateLayout();
                    try{var before=Native.Bounds(photo);photo.ResizePhoto(30,20,true,true);photo.UpdateLayout();var after=Native.Bounds(photo);Check(Math.Abs((before.X+before.Width)-(after.X+after.Width))<2&&Math.Abs((before.Y+before.Height)-(after.Y+after.Height))<2,"Opposite photo edges moved");Check(photo.ActualWidth<420&&photo.ActualHeight<280,"Photo did not resize");
                        var surface=(System.Windows.Controls.Grid)photo.Content;var image=surface.Children.OfType<System.Windows.Controls.Grid>().Single(element=>element is not GlassSurface);Check(image.Margin==new Thickness(0)&&image.Effect==null,"Photo has an inset frame or image shadow");surface.Children.OfType<System.Windows.Controls.Primitives.Thumb>().First().RaiseEvent(new System.Windows.Controls.Primitives.DragCompletedEventArgs(30,20,false));
                        var saved=new SettingsStore(Path.Combine(folder,"settings.json")).Load().Widgets.Single();Check(Math.Abs(saved.PhotoWidth!.Value-photo.ActualWidth)<1&&Math.Abs(saved.PhotoHeight!.Value-photo.ActualHeight)<1,"Displayed photo dimensions not persisted");var restored=new WidgetWindow(owner,saved,true);try{Check(Math.Abs(restored.Width-photo.Width)<1&&Math.Abs(restored.Height-photo.Height)<1,"Photo size not restored");}finally{restored.CloseOwned();}
                        photo.ResizePhoto(-10000,-10000,false,false);photo.UpdateLayout();Check(photo.ActualWidth>=223&&photo.ActualHeight>=151,"Photo resized below its minimum");
                    }finally{photo.CloseOwned();}return Task.CompletedTask;
                });
                await Test("failed window creation cannot leave enabled or counted instances",()=>{
                    var failedRoot=Path.Combine(root,"failing-window");Directory.CreateDirectory(failedRoot);using var failing=new WidgetManager(failedRoot,true,(_,_)=>throw new InvalidOperationException("Fixture construction failure"));
                    var count=failing.Settings.Widgets.Count;try{failing.Add("photo","glass");}catch(InvalidOperationException){}Check(failing.Settings.Widgets.Count==count&&failing.ActiveCount==0,"Failed Add created a ghost instance");
                    var state=new WidgetState{Kind="quick",Enabled=false};failing.Settings.Widgets.Add(state);try{failing.Enable(state);}catch(InvalidOperationException){}Check(!state.Enabled&&failing.ActiveCount==0,"Failed Enable advanced visibility");return Task.CompletedTask;
                });
                await Test("Focus restores its lease on stop, expiry, restart and failed mute",()=>{
                    var folder=Path.Combine(root,"focus-tests");Directory.CreateDirectory(folder);var env=new FakeFocusEnvironment();using(var focus=new FocusSession(folder,env)){focus.Start(37,true);Check(focus.End>DateTimeOffset.Now.AddMinutes(36)&&env.Profile==FocusSession.Quiet&&env.Outputs.Values.All(v=>v),"Focus did not apply settings");Check(File.Exists(Path.Combine(folder,"focus-session.json")),"Focus changes have no recovery lease");focus.Stop();Check(env.Profile=="Microsoft.QuietHoursProfile.Unrestricted"&&!env.Outputs["speaker"]&&env.Outputs["headset"],"Focus lost original profile or mute state");focus.Start(2,false);focus.Tick(DateTimeOffset.Now.AddMinutes(3));Check(focus.End==null&&env.Profile.EndsWith("Unrestricted"),"Expiry did not restore");focus.Start(5,true);using(var recovered=new FocusSession(folder,env))Check(recovered.End==null&&!env.Outputs["speaker"],"Restart did not restore");focus.Stop();env.FailMute=true;try{focus.Start(5,true);}catch(InvalidOperationException){}Check(focus.End==null&&env.Profile.EndsWith("Unrestricted")&&!env.Outputs["speaker"],"Failed start left system changes");env.FailMute=false;focus.Start(1,false);env.Profile="Microsoft.QuietHoursProfile.PriorityOnly";focus.Stop();Check(env.Profile.EndsWith("PriorityOnly"),"Stop overwrote a manual notification choice");}return Task.CompletedTask;
                });
                await Test("partial Focus recovery retains only unfinished restoration",()=>{
                    var folder=Path.Combine(root,"focus-partial");Directory.CreateDirectory(folder);var env=new FakeFocusEnvironment();env.Outputs["headset"]=false;using var focus=new FocusSession(folder,env);focus.Start(5,true);env.Disconnected="headset";focus.Stop();Check(!env.Outputs["speaker"],"Available speaker not restored");env.Outputs["speaker"]=true;env.Disconnected="";using var recovery=new FocusSession(folder,env);Check(env.Outputs["speaker"]&&!env.Outputs["headset"],"Recovery changed an already restored speaker or lost pending headset");return Task.CompletedTask;
                });
                await Test("Disable all ends Focus and restores its system lease",()=>{var folder=Path.Combine(root,"focus-disable");Directory.CreateDirectory(folder);var env=new FakeFocusEnvironment();using var owner=new WidgetManager(folder,true,focusEnvironment:env);owner.Data.StartFocus(45);owner.DisableAll();Check(owner.Data.FocusEnd==null&&env.Profile.EndsWith("Unrestricted")&&!env.Outputs["speaker"],"Disable all left Focus active");return Task.CompletedTask;});
                await Test("Focus never selects a music player or browser for call quieting",()=>{Check(FocusAppAudio.Eligible("Discord",FocusAppAudio.DefaultApps)&&FocusAppAudio.Eligible("Telegram",FocusAppAudio.DefaultApps),"Call apps not selected");foreach(var name in new[]{"Spotify","chrome","msedge","firefox","vlc","YoWidgets"})Check(!FocusAppAudio.Eligible(name,[name]),"Music or browser eligible for muting: "+name);Check(!FocusAppAudio.Eligible("unknown",FocusAppAudio.DefaultApps),"Unknown process muted");return Task.CompletedTask;});
                await Test("Focus discovers later call sessions and restores their original state",()=>{var folder=Path.Combine(root,"focus-new-session");Directory.CreateDirectory(folder);var env=new FakeFocusEnvironment();using var focus=new FocusSession(folder,env);focus.Start(30,true);env.Outputs["late-call"]=false;focus.Tick(DateTimeOffset.Now);Check(env.Outputs["late-call"],"Late call not quieted");focus.Stop();Check(!env.Outputs["late-call"]&&!env.Outputs["speaker"]&&env.Outputs["headset"],"Late call changed original mute state");return Task.CompletedTask;});
                await Test("later conflicting app instances disable ambiguous crash recovery",()=>{var states=FocusSession.Normalize([new("first",false,"app","app"),new("second",true,"app","app")]);Check(states.All(state=>state.RecoveryKey==null),"Conflicting app instances retained broad recovery");states=FocusSession.Normalize(states.Append(new("third",false,"app","app")).ToArray());Check(states.All(state=>state.RecoveryKey==null),"Later app instance re-enabled ambiguous recovery");return Task.CompletedTask;});
                await Test("pending-write hide records the lifecycle needed for a later reveal",async()=>{var fixture=new ShellWindow();typeof(ShellWindow).GetField("pendingWrites",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(fixture,true);await (Task)typeof(ShellWindow).GetMethod("Sleep",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(fixture,new object[]{false})!;var lifetime=(ShellLifetime)typeof(ShellWindow).GetField("lifetime",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(fixture)!;Check(lifetime.HiddenAt!=null,"Pending preferences hid the presentation without recording a future reveal");});
            }
            catch(Exception e){failed++;Console.WriteLine("FAIL native setup: "+e.Message);}
            finally{Console.WriteLine($"RESULT: {total-failed}/{total} passed");app.Shutdown();}
        };
        app.Run();try{Directory.Delete(root,true);}catch(IOException){}return failed==0?0:1;
    }
}

internal sealed class FakeFocusEnvironment:IFocusEnvironment
{
    public string Profile {get;set;}="Microsoft.QuietHoursProfile.Unrestricted";
    public Dictionary<string,bool> Outputs {get;}=new(){{"speaker",false},{"headset",true}};
    public bool FailMute;public string Disconnected="";
    public FocusAudioState[] Audio()=>Outputs.Select(pair=>new FocusAudioState(pair.Key,pair.Value)).ToArray();
    public void SetMute(string device,bool value){if(device==Disconnected)throw new InvalidOperationException("Fixture disconnected output");if(value&&FailMute)throw new InvalidOperationException("Fixture mute failure");Outputs[device]=value;}
}
