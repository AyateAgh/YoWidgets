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

            }
            catch(Exception e){failed++;Console.WriteLine("FAIL native setup: "+e.Message);}
            finally{Console.WriteLine($"RESULT: {total-failed}/{total} passed");app.Shutdown();}
        };
        app.Run();try{Directory.Delete(root,true);}catch(IOException){}return failed==0?0:1;
    }
}
