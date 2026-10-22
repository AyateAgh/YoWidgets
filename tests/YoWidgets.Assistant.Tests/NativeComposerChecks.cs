using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YoWidgets;

static class NativeComposerChecks
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,Hidden)!.GetValue(value)!;
    static object? Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,Hidden)!.Invoke(value,args);
    internal static void Run(bool web)
    {
        var root=Path.GetFullPath("artifacts/composer-ui/run-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var app=new App {ShutdownMode=ShutdownMode.OnExplicitShutdown};typeof(App).GetProperty(nameof(App.DataRoot))!.SetValue(null,root);Ui.Theme();Exception? failure=null;
        app.Startup+=async(_,_)=> {try {
            var attachment=AssistantAttachment.FromBytes("sample.tsx",System.Text.Encoding.UTF8.GetBytes("export const value = 42;\n"));
            var menu=AttachmentActions.Menu(attachment);if(!menu.Items.Cast<MenuItem>().Select(i=>(string)i.Header).SequenceEqual(new[]{"Open","Copy","Download"}))throw new Exception("Attachment actions missing");
            var tile=AttachmentPreview.Tile(attachment);if(!tile.Focusable||AutomationProperties.GetName(tile)!=attachment.Name||tile.ContextMenu?.Items.Count!=3)throw new Exception("Attachment keyboard/context actions unavailable");
            if(!File.ReadAllBytes(AttachmentActions.Cache(attachment)).SequenceEqual(attachment.Bytes()))throw new Exception("Cached bytes changed");
            var paths=new System.Collections.Specialized.StringCollection {Path.Combine(root,"sample.tsx")};var data=new DataObject();data.SetFileDropList(paths);if(AssistantClipboard.Read(data).Paths.Single()!=paths[0])throw new Exception("File paste missing");Console.WriteLine("PASS native composer: attachment menus, exact file snapshots and file-drop paste");
            var ai=new AiService();using var bridge=new BrowserAssistantBridge("test-only-pairing-code",0);var anchor=new Button();var popup=new ComposerMenu(anchor,ai,bridge,()=>{},()=>{},_=>{});var rows=Field<StackPanel>(popup,"body").Children.OfType<Button>().ToArray();
            var names=rows.Select(AutomationProperties.GetName).ToArray();if(!new[]{"Files and folders","Goal","Planning mode","Draw","Documents","PDF","Spreadsheets","Presentations","Browser use","Computer use"}.All(names.Contains))throw new Exception("Composer sections missing");
            rows.Single(b=>AutomationProperties.GetName(b)=="Goal").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));rows.Single(b=>AutomationProperties.GetName(b)=="Planning mode").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));rows.Single(b=>AutomationProperties.GetName(b)=="Documents").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(!ai.GoalEnabled||!ai.PlanningEnabled||!ai.Plugins.Contains("documents"))throw new Exception("Composer controls did not change real options");Console.WriteLine("PASS native composer: real mode and plugin switches, all menu entries");
            var draw=new DrawingWindow();draw.Loaded+=async(_,_)=> {try {
                var canvas=Field<InkCanvas>(draw,"canvas");canvas.Strokes.Add(new Stroke(new StylusPointCollection(new[]{new StylusPoint(50,80),new StylusPoint(130,130),new StylusPoint(220,70)})) {DrawingAttributes=new DrawingAttributes {Color=Colors.Cyan,Width=8,Height=8}});Call(draw,"Save");Call(draw,"Restore",0);if(canvas.Strokes.Count!=0)throw new Exception("Drawing undo failed");Call(draw,"Restore",1);if(canvas.Strokes.Count!=1)throw new Exception("Drawing redo failed");
                var screenshot=new RenderTargetBitmap((int)draw.ActualWidth,(int)draw.ActualHeight,96,96,PixelFormats.Pbgra32);screenshot.Render((Visual)draw.Content);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(screenshot));using(var file=File.Create(Path.Combine(root,"drawing-ui.png")))encoder.Save(file);
                await (Task)Call(draw,"Finish",true)!;
            }catch(Exception e){failure=e;draw.Close();}};
            if(draw.ShowDialog()!=true||draw.Result is not {MimeType:"image/png"} result)throw failure??new Exception("Drawing did not produce a real PNG");File.WriteAllBytes(Path.Combine(root,"drawing.png"),result.Bytes());Console.WriteLine("PASS native drawing: strokes, undo/redo and actual PNG attachment export");
            var exported=BrowserCompanionSetup.Export();if(!File.Exists(Path.Combine(exported,"worker.js"))||File.Exists(Path.Combine(exported,"worker.test.cjs")))throw new Exception("Browser companion bundle missing or contains tests");Console.WriteLine("PASS native companion export: complete extension resources");
            if(web) {using var reader=new BrowserPageReader();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(55));var page=await reader.ReadAsync("https://example.com/",0,10000,timeout.Token);if(page.GetProperty("title").GetString()!="Example Domain"||page.GetProperty("text").GetString()!.Length<40)throw new Exception("Real WebView page read failed");Console.WriteLine("PASS native browser: actual WebView2 public page DOM read");}
            Console.WriteLine("Native composer fixture output: "+root);
        }catch(Exception e){failure=e;}finally {app.Shutdown();}};app.Run();if(failure!=null)throw failure;
    }
}
