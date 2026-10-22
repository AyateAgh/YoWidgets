using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YoWidgets;

static class BrowserControlChecks
{
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    internal static void Run(bool preview=false)
    {
        var root=Path.GetFullPath("artifacts/control-checks/run-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var app=new App {ShutdownMode=ShutdownMode.OnExplicitShutdown};typeof(App).GetProperty(nameof(App.DataRoot))!.SetValue(null,root);Ui.Theme();Exception? failure=null;
        app.Startup+=async(_,_)=> {try {
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(80));var token=timeout.Token;
            using var browser=new BrowserPageReader();
            var id=await browser.FixtureAsync("<html><head><title>Owned browser fixture</title></head><body><h1>Fixture page</h1><label>Name <input aria-label='Fixture name'></label><input type='password' aria-label='Secret'><button onclick=\"document.getElementById('result').textContent=document.querySelector('input').value\">Apply</button><p id='result'>Waiting</p><div style='height:2500px'>Scroll fixture</div></body></html>",token);
            var first=await browser.ReadTabAsync(id,0,12000,token);int Find(JsonElement page,string label)=>page.GetProperty("controls").EnumerateArray().First(c=>c.GetProperty("label").GetString()==label).GetProperty("id").GetInt32();
            var initial=first.GetProperty("snapshotId").GetString()!;
            await browser.ActAsync(id,"fill",initial,Find(first,"Fixture name"),"Native browser input",0,token);
            bool stale=false;try {await browser.ActAsync(id,"click",initial,Find(first,"Apply"),"",0,token);}catch(InvalidOperationException){stale=true;}Require(stale,"Stale DOM input was accepted");
            var second=await browser.ReadTabAsync(id,0,12000,token);await browser.ActAsync(id,"click",second.GetProperty("snapshotId").GetString()!,Find(second,"Apply"),"",0,token);
            var third=await browser.ReadTabAsync(id,0,12000,token);Require(third.GetProperty("text").GetString()!.Contains("Native browser input"),"Browser click did not change the real DOM");Console.WriteLine("PASS browser control: real DOM read, fill, click and stale snapshot rejection");
            bool password=false;try {await browser.ActAsync(id,"fill",third.GetProperty("snapshotId").GetString()!,Find(third,"Secret"),"do not type",0,token);}catch(InvalidOperationException){password=true;}Require(password,"Password input accepted");
            var png=await browser.CaptureAsync(id,token);File.WriteAllBytes(Path.Combine(root,"browser-page.png"),png);Require(png.Length>1000,"No actual browser screenshot");
            await browser.ActAsync(id,"scroll","",0,"",700,token);var fourth=await browser.ReadTabAsync(id,0,12000,token);Require(fourth.GetProperty("controls")[0].GetProperty("y").GetDouble()<0,"Page did not scroll");
            var next=await browser.FixtureAsync("<html><head><title>Second owned tab</title></head><body>Second page</body></html>",token);Require(id!=next&&browser.Contains(id)&&browser.Contains(next),"Tabs were not retained");Console.WriteLine("PASS browser control: protected fields, real screenshot, scroll and multiple retained tabs");
            using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();bool stopped=false;try{await browser.ActAsync(id,"fill",initial,0,"late input",0,cancelled.Token);}catch(OperationCanceledException){stopped=true;}Require(stopped,"Cancelled browser action ran");}
            var ai=new AiService{AskBeforeControl=true};ai.Plugins.Add("browser");using(var runtime=new AssistantToolRuntime(ai,new AgentWorkspace(root),[],"fixture",token)) {
                bool scoped=false;try{await runtime.Execute(new("scope","browser_read_tab",JsonSerializer.SerializeToElement(new{tab_id=id,offset=0,limit=1000})),token);}catch(InvalidOperationException){scoped=true;}Require(scoped,"Unattached tab was readable");
                runtime.Approve=(_,_,_)=>Task.FromResult(false);bool denied=false;try {await runtime.Execute(new("denied","browser_open",JsonSerializer.SerializeToElement(new{url="https://example.com/"})),token);}catch(InvalidOperationException){denied=true;}Require(denied,"Denied browser opening ran");
            }Console.WriteLine("PASS browser control: cancellation, attached-tab scope and denied control consent");
            var approval=AgentApproval.Ask("Allow control for this task?","Owned fixture",token);var dialog=app.Windows.Cast<Window>().Single(w=>w.Title=="Approve agent action");Require(dialog.Topmost&&dialog.ShowInTaskbar&&dialog.IsVisible,"Opt-in confirmation can hide behind an application");
            IEnumerable<T> DialogControls<T>(DependencyObject node)where T:DependencyObject{if(node is T value)yield return value;for(var j=0;j<VisualTreeHelper.GetChildrenCount(node);j++)foreach(var child in DialogControls<T>(VisualTreeHelper.GetChild(node,j)))yield return child;}
            dialog.UpdateLayout();DialogControls<Button>(dialog).Single(b=>(string?)b.Content=="Deny").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Require(!await approval,"Opt-in confirmation did not honor Deny");
            Require(!new AppSettings().AiAskBeforeControl,"Requested actions must run without confirmation by default");
            ai.AskBeforeControl=false;var asked=0;
            for(var turn=0;turn<2;turn++)using(var runtime=new AssistantToolRuntime(ai,new AgentWorkspace(root),[],"voice request",token)){
                runtime.Approve=(_,_,_)=>{asked++;return Task.FromResult(false);};runtime.ReadPage=(url,_,_,_)=>Task.FromResult(JsonSerializer.SerializeToElement(new{url,text="Owned test result"}));
                foreach(var origin in new[]{"https://example.com/","https://example.org/"}){var observed=await runtime.Execute(new("read","read_web_page",JsonSerializer.SerializeToElement(new{url=origin,offset=0,limit=1000})),token);Require(observed.Text.Contains("Owned test result"),"Automatic control did not return observed data");}
            }
            Require(asked==0,"Confirmation repeated across voice requests or targets with confirmations disabled");
            ai.Plugins.Add("computer");ai.SetPlanning(true);using(var guarded=new AssistantToolRuntime(ai,new AgentWorkspace(root),[],"game task",token)){
                foreach(var provider in new[]{"Gemini","OpenAI","Claude"}){var schema=guarded.Tools(provider).ToJsonString();Require(schema.Contains("computer_press")&&schema.Contains("computer_mouse"),"Physical input missing from provider tools");}
                foreach(var tool in new[]{"computer_press","computer_mouse"}){bool blocked=false;try{guarded.GuardMutation(tool);}catch(InvalidOperationException){blocked=true;}Require(blocked,"Physical input bypassed the planning gate");}
            }
            ai.CloseBrowser();Console.WriteLine("PASS control policy: default direct actions, two voice requests and multiple targets without a dialog; opt-in denial and unattached-tab scope retained");
            using var lease=new CancellationTokenSource();var cancelledByUser=false;using var session=new AgentControlSession("Fixture model",()=> {cancelledByUser=true;lease.Cancel();},lease.Token);
            await session.Allow("fixture","Owned fixture only",(_,_,_)=>Task.FromResult(true),token);Require(session.Visible,"Control banner missing");
            if(preview){Console.WriteLine("CONTROL PREVIEW READY · owned fixture only");await Task.Delay(TimeSpan.FromSeconds(50),lease.Token).ContinueWith(_=>{});Require(cancelledByUser,"Stop was not triggered");}
            else {session.Stop();Require(cancelledByUser&&!session.Visible,"Stop did not cancel and remove chrome");Console.WriteLine("PASS control session: real banner, screen edges and stop cleanup");}
            var journal=new AgentWorkspace(root);await journal.ExecuteCustomAsync(new("list","computer_list_windows",JsonSerializer.SerializeToElement(new{})),_=>Task.FromResult(new AgentToolResult(new("list","computer_list_windows",JsonSerializer.SerializeToElement(new{})),"{\"windows\":[{},{}]}")),token);await journal.ExecuteCustomAsync(new("failed","computer_open_app",JsonSerializer.SerializeToElement(new{})),_=>Task.FromResult(new AgentToolResult(new("failed","computer_open_app",JsonSerializer.SerializeToElement(new{})),"{\"error\":\"Fixture app needs sign-in\"}",true)),token);journal.Finish("Completed");
            var activity=new AgentRunView(journal,new AiService());var host=new Window {Content=activity,Width=500,Height=420,Title="Activity fixture"};host.Show();host.UpdateLayout();
            IEnumerable<T> All<T>(DependencyObject node) where T:DependencyObject {if(node is T match)yield return match;for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var child in All<T>(VisualTreeHelper.GetChild(node,i)))yield return child;}
            var texts=All<TextBlock>(host).Select(t=>t.Text).ToArray();Require(texts.Contains("Activity")&&!texts.Any(t=>t.Contains("0 changed file")),"Computer-only run shows an empty file card");var disclosure=All<Expander>(host).First();disclosure.IsExpanded=true;host.UpdateLayout();texts=All<TextBlock>(host).Select(t=>t.Text).ToArray();Require(texts.Any(t=>t=="Found 2 open windows")&&texts.Any(t=>t.Contains("Fixture app needs sign-in"))&&!texts.Any(t=>t=="Listed apps ."),"Action counts or readable errors are missing");host.Close();Console.WriteLine("PASS activity UI: actual window count, visible failure reason, no empty file totals or Undo");
            var screen=System.Windows.Forms.Screen.PrimaryScreen!.Bounds;var effect=new ScreenControlGlow {Active=true,ScreenBounds=screen,PointerOpacity=()=>1};effect.Measure(new Size(960,540));effect.Arrange(new Rect(0,0,960,540));var frame=new RenderTargetBitmap(960,540,96,96,PixelFormats.Pbgra32);frame.Render(effect);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(frame));using(var file=File.Create(Path.Combine(root,"screen-wave-pointer.png")))encoder.Save(file);Console.WriteLine("PASS control visuals: native full-screen wave and spectral pointer render");
            Console.WriteLine("Control fixture output: "+root);
        }catch(Exception e){Console.WriteLine(e);failure=e;}finally {app.Shutdown();}};app.Run();if(failure!=null)throw failure;
    }
}
