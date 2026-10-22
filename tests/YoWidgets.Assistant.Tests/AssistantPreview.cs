using System.Net;
using System.Text;
using System.Text.Json;
using System.Windows;
using YoWidgets;

static class AssistantPreview
{
    public static void Run(string[] args)
    {
        var root = Path.GetFullPath("artifacts/assistant-preview/run-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        // Isolate diagnostics from the user's actual settings and logs.
        typeof(App).GetProperty(nameof(App.DataRoot))!.SetValue(null, root);
        Ui.Theme();
        var manager = new WidgetManager(root, true);
        typeof(App).GetProperty(nameof(App.Manager))!.SetValue(app, manager);
        manager.Settings.AiProvider = "Gemini"; manager.Settings.AiModel = "gemini-flash-latest"; manager.Settings.AiKeyProtected = AiService.Protect("fixture-only");
        var agent=args.Contains("--agent-preview")||args.Contains("--agent-ui-check");
        var handler = new PreviewHandler(args.Contains("--slow"),agent,args.Contains("--agent-hold")||args.Contains("--agent-ui-check"));
        var client = new HttpClient(handler) { Timeout=Timeout.InfiniteTimeSpan };
        typeof(AiService).GetField("transport", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(manager.Data.Ai, client);
        if(agent) {var folder=Path.Combine(root,"agent-workspace-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"Greeting.tsx"),"export const Greeting = () => <h1>Before</h1>;\n");manager.Data.Ai.SetWorkspace(folder);}
        var area = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
        var state=new WidgetState { Kind="assistant",Enabled=true,Size=args.Contains("--small")?"S":"M",X=120,Y=args.Contains("--bottom")?area.Bottom-160:100 };
        manager.Settings.Widgets.Add(state);
        var widget = new WidgetWindow(manager,state,true);
        widget.Closed += (_, _) => { manager.Dispose(); client.Dispose(); app.Shutdown(); };
        app.Startup += async (_, _) => {
            if(args.Contains("--composer-preview"))widget.ShowInTaskbar=true;
            widget.Show();
            if(args.Contains("--composer-preview")) {widget.Title="Composer QA · YoWidgets";manager.Data.Ai.SetGoal(true);manager.Data.Ai.SetPlanning(true);manager.Data.Ai.TogglePlugin("documents");await Task.Delay(300);var view=Find<AssistantView>(widget)!;typeof(AssistantView).GetMethod("OpenFileMenu",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(view,null);}
            if(agent) {
                var work=manager.Data.Ai.SendAsync("Update the greeting component and add its stylesheet.",manager.Settings);
                if(args.Contains("--agent-ui-check")) {await AgentUiChecks.Run(widget,manager.Data.Ai,work,handler.Release);widget.CloseOwned();return;}
                await work;
            }
            if(args.Contains("--settings")) { widget.Hide(); var main=new MainWindow(manager); main.Show(); main.Select("settings"); }
            if(args.Contains("--voice-visual")) { var view=Find<AssistantView>(widget); if(view!=null) { var glow=(VoiceGlow)typeof(AssistantView).GetField("glow",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(view)!; glow.ReadLevel=()=>.45+.4*Math.Sin(DateTime.UtcNow.TimeOfDay.TotalSeconds*3); glow.Active=true; } }

            if(args.Contains("--drop-visual")) widget.SetAssistantDrop(true);
            if(args.Contains("--attachments") || args.Contains("--media-chat")) {
                manager.Data.Ai.DraftAttachments.Add(AttachmentMedia.PrepareGif(MediaChecks.Gif(),CancellationToken.None));
                manager.Data.Ai.DraftAttachments.Add(new AssistantAttachment("component.tsx","text/plain","","export const Greeting = () => <h1>Hello</h1>;",48));
                var view=Find<AssistantView>(widget)!; typeof(AssistantView).GetMethod("Refresh",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(view,null);
            }
            if(args.Contains("--editor-check") || args.Contains("--editor")) {
                var view=Find<AssistantView>(widget)!; var input=(System.Windows.Controls.TextBox)typeof(AssistantView).GetField("input",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(view)!;
                input.Text="First line\nSecond line\nThird line\nFourth line";
                if(args.Contains("--editor-check")) {
                    await Task.Delay(600); var fourHeight=input.ActualHeight;
                    if(input.GetLastVisibleLineIndex()-input.GetFirstVisibleLineIndex()+1!=4)throw new Exception("Four editor lines are not visible");
                    var lastGlyph=input.GetRectFromCharacterIndex(input.Text.Length);
                    if(lastGlyph.Bottom>input.ActualHeight-input.Padding.Bottom+1) throw new Exception($"Fourth line is clipped: {lastGlyph.Bottom} vs {input.ActualHeight}");
                    input.Text+="\nFifth line\nSixth line";await Task.Delay(400);
                    if(Math.Abs(input.ActualHeight-fourHeight)>1)throw new Exception("Editor exceeded the four-line cap");
                    input.Clear(); await Task.Delay(400);
                    if(Math.Abs(input.ActualHeight-34)>1)throw new Exception("Editor did not return to one line");
                    Console.WriteLine($"PASS editor: four complete lines ({fourHeight:0.0} DIP), six lines scroll without growth, cleared input returns to 34 DIP");widget.CloseOwned();return;
                }
            }
            if(args.Contains("--media-chat")) await manager.Data.Ai.SendAsync("What happens in this animation? Also check the attached component.",manager.Settings,manager.Data.Ai.DraftAttachments.ToArray());
            if (args.Contains("--geometry-check"))
            {
                await Task.Delay(100); var before = Native.Bounds(widget);
                await manager.Data.Ai.SendAsync("Write a small TypeScript greeting function.",manager.Settings);
                await Task.Delay(500); var after = Native.Bounds(widget);
                var error = args.Contains("--bottom") ? Math.Abs(before.Y + before.Height - after.Y - after.Height) : Math.Abs(before.Y-after.Y);
                if (error > 2) throw new Exception("Composer anchor moved by " + error + " physical pixels");
                Console.WriteLine($"PASS native geometry: up={widget.AssistantExpandsUp}; anchor delta={error}; compact={before.Height}; expanded={after.Height}"); widget.CloseOwned();
            }
            else if (args.Contains("--filled")) await manager.Data.Ai.SendAsync("Write a small TypeScript greeting function.", manager.Settings);
        };
        app.Run();
    }
    static T? Find<T>(System.Windows.DependencyObject root) where T:System.Windows.DependencyObject
    {
        if(root is T found)return found;
        for(var i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);i++) if(Find<T>(System.Windows.Media.VisualTreeHelper.GetChild(root,i)) is T child)return child;
        return null;
    }
    sealed class PreviewHandler(bool slow,bool agent,bool hold) : HttpMessageHandler
    {
        int round;
        readonly TaskCompletionSource release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Release()=>release.TrySetResult();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if(agent) {
                await Task.Delay(350,token);
                if(hold&&round>=4)await release.Task.WaitAsync(token);
                var agentData=round++ switch {
                    0=>AgentChecks.Event("Gemini","read_file",new {path="Greeting.tsx",start_line=1,line_count=100}),
                    1=>AgentChecks.Event("Gemini","edit_file",new {path="Greeting.tsx",old_text="Before",new_text="Hello, YoWidgets",expected_sha256=AgentChecks.Hash("export const Greeting = () => <h1>Before</h1>;\n")}),
                    2=>AgentChecks.Event("Gemini","write_file",new {path="greeting.css",content=".greeting {\n  color: #78adff;\n  padding: 16px;\n}\n",encoding="utf8",expected_sha256=""}),
                    3=>AgentChecks.Event("Gemini","write_file",new {path="preview.gif",content=MediaChecks.Gif().Data,encoding="base64",expected_sha256=""}),
                    _=>"data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Updated the greeting and added its stylesheet. The file changes are ready to review below.\"}]},\"finishReason\":\"STOP\"}]}\n\n" };
                return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(agentData,Encoding.UTF8,"text/event-stream")};
            }
            if (slow) await Task.Delay(TimeSpan.FromSeconds(30),token);
            const string text = "## A small, typed greeting\n\nUse a **default value** so the function works with or without a name.\n\n```typescript\nfunction greet(name: string = \"world\"): string {\n  return `Hello, ${name}!`;\n}\n\nconsole.log(greet(\"YoWidgets\"));\n```\n\n- The argument and return value are typed.\n- Nothing runs until you use the code.\n\nWant an async version?";
            var data = JsonSerializer.Serialize(new { candidates = new[] { new { content = new { parts = new[] { new { text } } }, finishReason = "STOP" } } });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("data: " + data + "\n\n", Encoding.UTF8, "text/event-stream") };
        }
    }
}
