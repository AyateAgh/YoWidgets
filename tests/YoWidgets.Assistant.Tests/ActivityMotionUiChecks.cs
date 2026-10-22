using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YoWidgets;
internal static class ActivityMotionUiChecks
{
    static IEnumerable<T> All<T>(DependencyObject root) where T:DependencyObject
    {if(root is T t)yield return t;for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in All<T>(VisualTreeHelper.GetChild(root,i)))yield return child;}
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    internal static void Run()
    {
        var root=Path.GetFullPath("artifacts/activity-motion-ui/run-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var app=new App{ShutdownMode=ShutdownMode.OnExplicitShutdown};typeof(App).GetProperty(nameof(App.DataRoot))!.SetValue(null,root);Ui.Theme();WidgetVisuals.Tone(false);Exception? failure=null;
        app.Startup+=async(_,_)=>{
            Window? window=null;AiService? ai=null;
            try{
                using var handler=new Fixture();using var http=new HttpClient(handler){Timeout=Timeout.InfiniteTimeSpan};ai=new AiService(http);ai.SelectPluginInstruction("documents");ai.SelectPluginInstruction("media");
                var settings=new AppSettings{AiProvider="Gemini",AiModel="gemini-flash-lite-latest",AiKeyProtected=AiService.Protect("fixture-chat"),ImageConnection=new(){Mode="separate",Provider="Pollinations",Model="fixture/image",KeyProtected=AiService.Protect("fixture-media")}};
                var task=ai.SendAsync("Check the Word schema, then generate an image.",settings);await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));var message=ai.Messages.Last();var run=message.Agent!;
                var journal=new AgentRunView(run,ai);var media=new GeneratedMediaView(message,ai,(_,_,_)=>{},_=>{});media.Refresh();
                var content=Ui.Stack(journal,media);window=new Window{Title="Activity and motion QA",Width=480,Height=740,Background=new SolidColorBrush(Color.FromRgb(25,33,43)),Content=new ScrollViewer{Background=new SolidColorBrush(Color.FromRgb(25,33,43)),Content=new Border{Child=content,Padding=new Thickness(22)},VerticalScrollBarVisibility=ScrollBarVisibility.Auto}};window.Show();await Task.Delay(300);window.UpdateLayout();
                var status=All<AssistantStatusView>(window).Single(s=>s.IsVisible);Require(status.State==OrbState.Generating&&status.Active,"Media did not select the gradient generation orb");Require(All<TextBlock>(window).Any(t=>t.Text.StartsWith("Documents · ")&&t.Text.Contains("into response")),"Document tool identity/timing missing");Require(run.Steps[0].EndedAt!=null&&run.Steps.Last().EndedAt==null,"Working/completed timing was fabricated");
                Snapshot(window,Path.Combine(root,"generating.png"));var first=run.Steps.Last().Elapsed;await Task.Delay(1050);Require(run.Steps.Last().Elapsed>first,"Working duration did not advance");
                var rows=All<Expander>(journal).Where(e=>e.Header is DockPanel).ToArray();Require(rows.Length>=2,"Live calls cannot expand");rows[^1].IsExpanded=true;await Task.Delay(120);Require(All<TextBlock>(journal).Any(t=>t.IsVisible&&t.Text.Contains("Started ")),"Expanded tool did not show actual start time");
                Console.WriteLine("PASS activity timeline: actual native Word tool, generating media call, live duration, offsets and expandable timing");
                window.Width=310;await Task.Delay(180);Snapshot(window,Path.Combine(root,"narrow.png"));Require(status.ActualWidth<=window.ActualWidth,"Status overflowed narrow viewport");window.Width=480;
                foreach(var visual in All<ActiveVisual>(status))visual.MotionEnabled=false;Require(All<ActiveVisual>(status).All(v=>!v.IsAnimating),"Reduced motion kept an animation subscription");Snapshot(window,Path.Combine(root,"reduced-motion.png"));foreach(var visual in All<ActiveVisual>(status))visual.MotionEnabled=true;
                Console.WriteLine("PASS status motion: narrow layout, accessible real text, and reduced-motion subscription cleanup");
                var liveDetail=All<TextBlock>(journal).Single(t=>t.IsVisible&&t.Text.Contains("Waiting for the current tool result"));var oldDetail=liveDetail.Text;await Task.Delay(1100);Require(liveDetail.Text!=oldDetail,"Expanded live tool duration became stale");
                WidgetVisuals.Tone(true);((ScrollViewer)window.Content).Background=Brushes.WhiteSmoke;await Task.Delay(140);Snapshot(window,Path.Combine(root,"bright-ink.png"));WidgetVisuals.Tone(false);((ScrollViewer)window.Content).Background=new SolidColorBrush(Color.FromRgb(25,33,43));
                var one=Draw(dc=>AssistantSpectrum.Orb(dc,new Rect(0,0,160,160),.1),160,160);var two=Draw(dc=>AssistantSpectrum.Orb(dc,new Rect(0,0,160,160),.65),160,160);Require(Hash(one)!=Hash(two),"Orb frames are static");Save(one,Path.Combine(root,"orb-a.png"));Save(two,Path.Combine(root,"orb-b.png"));
                var label=new TextBlock{Text="Generating an image…",FontFamily=Ui.BodyFont,FontSize=20,FontWeight=FontWeights.Medium};var sweepA=Draw(dc=>AssistantSpectrum.Sweep(dc,label,320,.25),320,40);var sweepB=Draw(dc=>AssistantSpectrum.Sweep(dc,label,320,.65),320,40);Require(Hash(sweepA)!=Hash(sweepB),"Text sweep frames are static");Save(sweepA,Path.Combine(root,"text-a.png"));Save(sweepB,Path.Combine(root,"text-b.png"));Console.WriteLine("PASS spectral rendering: distinct native orb frames and one-second text sweep frames");
                handler.Release.TrySetResult();await task;await Task.Delay(180);media.Refresh();window.UpdateLayout();Require(!All<AssistantStatusView>(window).Any(v=>v.IsVisible),"Completed response retained active status");Require(All<ActiveVisual>(status).All(v=>!v.IsAnimating),"Completed response kept rendering");var history=All<Expander>(journal).Single(e=>e.Header is DockPanel h&&All<TextBlock>(h).Any(t=>t.Text.StartsWith("Used ")));Require(!history.IsExpanded,"Completed response duplicated its journal");history.IsExpanded=true;await Task.Delay(120);Snapshot(window,Path.Combine(root,"completed.png"));Require(run.Steps.All(s=>s.EndedAt!=null),"Final duration not frozen");
                foreach(var (tool,format,title) in new[]{("document_create","xlsx","Spreadsheets"),("document_create","pptx","Presentations"),("document_preview","pdf","PDF"),("computer_inspect","","Computer use"),("browser_open","","Browser use")}){var identity=ToolActivity.Identity(new(tool,"attachment:0",default,format));Require(identity.Title==title,"Incorrect tool icon/category: "+tool);}
                Console.WriteLine("PASS completion: one folded history, fixed durations, correct plugin identities and no idle render subscription");
                ai.NewChat();var cancelled=ai.SendAsync("Generate another image",settings);await handler.SecondEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));ai.Cancel();await cancelled;Require(ai.Messages.Last().Agent!.Steps.Last().Status=="Stopped"&&ai.Messages.Last().Agent!.Steps.Last().EndedAt!=null,"Cancelled call did not settle its timing");Console.WriteLine("PASS cancellation: actual pending media request stops and freezes its tool duration");
                Console.WriteLine("Activity/motion UI artifacts: "+root);
            }catch(Exception e){failure=e;}finally{ai?.Cancel();window?.Close();app.Shutdown();}
        };app.Run();if(failure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
    static RenderTargetBitmap Draw(Action<DrawingContext> draw,int width,int height){var visual=new DrawingVisual();using(var dc=visual.RenderOpen())draw(dc);var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);return bitmap;}
    static string Hash(BitmapSource bitmap){var bytes=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(bytes,bitmap.PixelWidth*4,0);return Convert.ToHexString(SHA256.HashData(bytes));}
    static void Save(BitmapSource source,string path){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(source));using var file=File.Create(path);encoder.Save(file);}
    static void Snapshot(Window window,string path){window.UpdateLayout();var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render((Visual)window.Content);Save(bitmap,path);}
    sealed class Fixture:HttpMessageHandler
    {
        int round,generations;internal TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),SecondEntered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            if(request.RequestUri!.Host=="generativelanguage.googleapis.com"){
                var data=round++ switch{0=>AgentChecks.Event("Gemini","document_schema",new{format="docx"}),1=>AgentChecks.Event("Gemini","generate_media",new{kind="image",prompt="A blue circle",reference="",aspect_ratio="1:1"}),2=>"data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Created the image.\"}]},\"finishReason\":\"STOP\"}]}\n\n",_=>AgentChecks.Event("Gemini","generate_media",new{kind="image",prompt="A blue circle",reference="",aspect_ratio="1:1"})};return new(HttpStatusCode.OK){Content=new StringContent(data,Encoding.UTF8,"text/event-stream")};
            }
            if(request.Method==HttpMethod.Get)return new(HttpStatusCode.OK){Content=new StringContent("[{\"name\":\"fixture/image\",\"input_modalities\":[\"text\"],\"output_modalities\":[\"image\"]}]",Encoding.UTF8,"application/json")};
            if(generations++==0){Entered.TrySetResult();await Release.Task.WaitAsync(token);}else{SecondEntered.TrySetResult();await Task.Delay(Timeout.Infinite,token);}
            var png="iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jN1sAAAAASUVORK5CYII=";return new(HttpStatusCode.OK){Content=new StringContent("{\"data\":[{\"b64_json\":\""+png+"\",\"media_type\":\"image/png\"}]}",Encoding.UTF8,"application/json")};
        }
    }
}
