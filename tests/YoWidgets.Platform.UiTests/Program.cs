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
    [STAThread]static int Main()
    {
        var root=Path.Combine(Path.GetTempPath(),"YoPlatformNative-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        typeof(App).GetProperty(nameof(App.DataRoot),BindingFlags.Static|BindingFlags.Public)!.SetValue(null,root);
        var app=new App{ShutdownMode=ShutdownMode.OnExplicitShutdown};
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
                    await platform.Execute("widget.remove",Args(new{id=first.Id}),CancellationToken.None);Check(manager.Settings.Widgets.Count==1,"Removing an instance removed the whole content item");Check(platform.Content.IsInstalled("glass-clock"),"Removing an instance uninstalled its resources");
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
                await Test("native management maximize and interrupted restore stay inside work area",async()=>{foreach(var bad in new string?[]{null,"null","{}","{\"bounds\":null}","{\"bounds\":{\"x\":1e20,\"y\":0,\"width\":800,\"height\":600}}"})Check(ShellWindowLayout.ParseSaved(bad)==null,"Malformed saved geometry was accepted");var fixture=new Window{Width=720,Height=480,WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=System.Windows.Media.Brushes.Black,ShowInTaskbar=false};System.Windows.Shell.WindowChrome.SetWindowChrome(fixture,new(){CaptionHeight=0,ResizeBorderThickness=new Thickness(6),GlassFrameThickness=new Thickness(0),CornerRadius=new CornerRadius(24)});ShellWindowPlacement? savedPlacement=null;using var layout=new ShellWindowLayout(fixture,_=>{},persist:p=>savedPlacement=p);fixture.Show();var start=Native.Bounds(fixture);await layout.ToggleAsync();var maximized=Native.Bounds(fixture);var work=System.Windows.Forms.Screen.FromHandle(Native.Handle(fixture)).WorkingArea;Check(maximized.X>=work.Left&&maximized.Y>=work.Top&&maximized.X+maximized.Width<=work.Right&&maximized.Y+maximized.Height<=work.Bottom,"Maximize covers the taskbar");await layout.ToggleAsync();var restored=Native.Bounds(fixture);Check(Math.Abs(restored.Width-start.Width)<3&&Math.Abs(restored.Height-start.Height)<3,"Restore lost normal bounds");await layout.ToggleAsync();fixture.WindowState=WindowState.Minimized;layout.Save();Check(savedPlacement?.Maximized==true&&layout.RestoreState==WindowState.Maximized,"Minimize forgot maximization");fixture.WindowState=layout.RestoreState;Check(fixture.WindowState==WindowState.Maximized,"Minimize restored to wrong state");fixture.WindowState=WindowState.Normal;var osRestore=Native.Bounds(fixture);Check(Math.Abs(osRestore.Width-start.Width)<3&&Math.Abs(osRestore.Height-start.Height)<3,"OS restore lost its normal bounds");var first=layout.ToggleAsync();var second=layout.ToggleAsync();await Task.WhenAll(first,second);Check(!layout.IsAnimating&&fixture.WindowState==WindowState.Normal,"Rapid toggle left an active animation or wrong state");fixture.Close();});
                await Test("local retries retain native source and history clearing preserves installed content",async()=>{
                    var privatePath=Path.Combine(root,"private-source-not-present.zip");var response=Args(await platform.Execute("content.import",Args(new{path=privatePath,hash=new string('a',64)}),CancellationToken.None));var first=response.GetProperty("jobId").GetString()!;
                    async Task Wait(string id){for(var i=0;i<100;i++){var state=Args(platform.Snapshot());if(state.GetProperty("jobs").EnumerateArray().Any(j=>j.GetProperty("id").GetString()==id&&j.GetProperty("stage").GetString()=="failed"))return;await Task.Delay(10);}throw new Exception("Import did not finish");}
                    await Wait(first);var retry=Args(await platform.Execute("content.retry",Args(new{id=first}),CancellationToken.None)).GetProperty("jobId").GetString()!;Check(retry!=first,"Retry reused the failed job");await Wait(retry);Check(!JsonSerializer.Serialize(platform.Snapshot(),CatalogJson.Options).Contains("private-source-not-present"),"A local path entered the UI snapshot");await platform.Execute("content.clearFinished",Args(new{}),CancellationToken.None);Check(Args(platform.Snapshot()).GetProperty("jobs").GetArrayLength()==0,"Finished history was not cleared");Check(platform.Content.IsReady("glass-clock")&&manager.Settings.Widgets.Any(w=>w.Enabled),"Clearing history removed desktop content");
                });

            }
            catch(Exception e){failed++;Console.WriteLine("FAIL native setup: "+e.Message);}
            finally{Console.WriteLine($"RESULT: {total-failed}/{total} passed");app.Shutdown();}
        };
        app.Run();try{Directory.Delete(root,true);}catch(IOException){}return failed==0?0:1;
    }
}
