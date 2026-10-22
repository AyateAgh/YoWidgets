global using YoWidgets.Core;
global using System.Net.Http;
global using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using YoWidgets;
if(args.Contains("--widget-ipc-client")){await WidgetIpcChecks.Client(args[Array.IndexOf(args,"--widget-ipc-client")+1]);return 0;}
if(args.Contains("--voice-actions-saved-check")){Exception? failure=null;var thread=new Thread(()=>{try{VoiceActionsLiveCheck.Run();}catch(Exception e){failure=e;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(failure!=null)throw failure;return 0;}
if(args.Contains("--widget-ipc-check")){Exception? failure=null;var thread=new Thread(()=>{try{WidgetIpcChecks.Run();}catch(Exception e){failure=e;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(failure!=null)throw failure;return 0;}
if(args.Contains("--voice-ui-check")){Exception? failure=null;var thread=new Thread(()=>{try{VoiceCallUiChecks.Run();}catch(Exception e){failure=e;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(failure!=null)throw failure;return 0;}
if(args.Contains("--voice-check")){await LiveVoiceChecks.Run();return 0;}
if(args.Contains("--voice-saved-check"))return await LiveVoiceChecks.SavedCheck();
if(args.Contains("--composer-polish-ui-check")){Exception? failure=null;var thread=new Thread(()=>{try{ComposerPolishUiChecks.Run();}catch(Exception e){failure=e;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(failure!=null)throw failure;return 0;}
if(args.Contains("--activity-motion-ui-check")){Exception? failure=null;var thread=new Thread(()=>{try{ActivityMotionUiChecks.Run();}catch(Exception e){failure=e;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(failure!=null)throw failure;return 0;}
if(args.Contains("--pollinations-saved-check")){
    var settings=JsonSerializer.Deserialize<AppSettings>(await File.ReadAllTextAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"YoWidgets","settings.json")))!;settings.Normalize();
    if(settings.ImageConnection.Mode!="separate"||settings.ImageConnection.Provider!="Pollinations"){Console.WriteLine("No separate Pollinations image key has been saved. No request sent.");return 2;}
    using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=Timeout.InfiniteTimeSpan};using var stop=new CancellationTokenSource(TimeSpan.FromMinutes(5));
    try{var connection=GenerationConnections.Resolve(settings,"image");var key=AiService.UnlockGenerationKey(connection.KeyProtected);var check=await GenerationConnections.Check(http,connection,key,stop.Token);Console.WriteLine(check.Message);Console.WriteLine("Current image catalog: "+check.Models.Count+" models.");
        if(args.Contains("--generate-image")){if(!check.ModelAvailable){Console.WriteLine("Generation skipped: selected model access is unavailable.");return 1;}var job=new GeneratedMediaJob("image","A simple blue circle on a plain white background.","1:1");var file=await new GeneratedMediaService(http,connection.Provider,key,connection.Model,connection.Model).Generate(job,null,null,stop.Token);Console.WriteLine("LIVE generation succeeded: "+file.MimeType+" · "+new FileInfo(file.Path).Length+" bytes · "+file.Path);}
        else Console.WriteLine("Read-only check completed. No generation credits used by this diagnostic.");return 0;
    }catch(Exception e){Console.WriteLine(e is InvalidOperationException?e.Message:"Pollinations diagnostic failed: "+e.GetType().Name);return 1;}
}
if(args.Contains("--pollinations-catalog-check")){
    using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(25)};
    foreach(var category in GenerationConnections.Categories){var models=await PollinationsCatalog.Models(http,category.Id,CancellationToken.None);var selected=models.FirstOrDefault(m=>m.Id==GenerationConnections.DefaultModel(category.Id,"Pollinations"))??throw new Exception("Default model missing: "+category.Id);Console.WriteLine($"PASS live public catalog {category.Id}: {models.Count} models; default={selected.Id}; paid_only={selected.PaidOnly}; rates={selected.Price}");}Console.WriteLine("Public metadata only; no key authentication or generation performed.");return 0;
}
if(args.Contains("--generation-settings-ui-check")){Exception? failure=null;var thread=new Thread(()=>{try{GenerationSettingsUiChecks.Run();}catch(Exception e){failure=e;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(failure!=null)throw failure;return 0;}
if(args.Contains("--media-probe"))return await GeneratedMediaProbe.Run();
if(args.Contains("--media-ui-check")){Exception? failure=null;var thread=new Thread(()=>{try{GeneratedMediaUiChecks.Run();}catch(Exception e){failure=e;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(failure!=null)throw failure;return 0;}

if(args.Contains("--native-control-check")) {Exception? failure=null;var thread=new Thread(()=>{try{NativeControlChecks.Run();}catch(Exception e){failure=e;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(failure!=null)throw failure;return 0;}
if(args.Contains("--control-check")||args.Contains("--control-preview")) {Exception? failure=null;var thread=new Thread(()=>{try{BrowserControlChecks.Run(args.Contains("--control-preview"));}catch(Exception e){failure=e;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(failure!=null)throw failure;return 0;}
if(args.Contains("--document-check")) {Exception? failure=null;var thread=new Thread(()=>{try{DocumentChecks.Run();}catch(Exception e){failure=e;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(failure!=null)throw failure;return 0;}
if(args.Contains("--composer-ui-check")) {Exception? failure=null;var thread=new Thread(()=>{try{NativeComposerChecks.Run(args.Contains("--web-check"));}catch(Exception e){failure=e;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(failure!=null)throw failure;return 0;}
if(args.Contains("--mention-ui-check")||args.Contains("--mention-preview")) {Exception? failure=null;var thread=new Thread(()=>{try{MentionUiChecks.Run(args.Contains("--mention-preview"));}catch(Exception e){failure=e;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(failure!=null)throw failure;return 0;}
if(args.Contains("--composer-probe")) {
    var saved=JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"YoWidgets","settings.json")))!;saved.Normalize();
    var root=Path.GetFullPath("artifacts/live-composer-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var service=new AiService();service.SetWorkspace(root);service.SetGoal(true);service.SetPlanning(true);service.TogglePlugin("documents");service.TogglePlugin("pdf");int shown=0;
    service.Changed+=()=> {var run=service.Messages.LastOrDefault()?.Agent;if(run!=null&&run.Steps.Count>shown){shown=run.Steps.Count;Console.WriteLine("LIVE composer action "+shown+": "+run.Steps.Last().Tool+" "+run.Steps.Last().Path);}};
    var work=service.SendAsync("Synthetic integration test, no personal data: create report.docx and report.pdf, both a one-page report titled YoWidgets Fixture with a brief paragraph and a two-column table containing Alpha 10 and Beta 20. Use Arial 11 body, 18 bold title, A4 with 20 mm margins, header Fixture and footer Page {page}. This is a complex multi-format task: save a short structured Markdown plan before creation, inspect both saved documents, and render the PDF page for verification. Use document_schema first for each format. Do not browse, operate apps, or use outside files. Finish only when both files actually exist.",saved);
    if(await Task.WhenAny(work,Task.Delay(TimeSpan.FromSeconds(300)))!=work){service.Cancel();await work;Console.WriteLine("Diagnostic stopped after 300 seconds (harness only).");return 1;}await work;
    var good=service.Error.Length==0&&Directory.GetFiles(root,"PLAN-*.md").Length==1&&new[]{"report.docx","report.pdf"}.All(n=>File.Exists(Path.Combine(root,n))&&JsonSerializer.Serialize(DocumentEngine.Inspect(n,File.ReadAllBytes(Path.Combine(root,n)),0,48000)).Contains("Alpha"));
    Console.WriteLine($"LIVE composer: success={good}; changes={service.Messages.Last().Agent?.Changes.Count}; error={service.Error}; folder={root}");return good?0:1;
}

if(args.Contains("--agent-probe"))
{
    var saved=JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"YoWidgets","settings.json")))!;saved.Normalize();
    var root=Path.GetFullPath("artifacts/live-agent-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);await File.WriteAllTextAsync(Path.Combine(root,"note.txt"),"alpha\nbeta\n");await File.WriteAllTextAsync(Path.Combine(root,"obsolete.txt"),"test-only obsolete file\n");
    var service=new AiService();service.SetWorkspace(root);int shown=0;service.Changed+=()=> {var run=service.Messages.LastOrDefault()?.Agent;if(run!=null&&run.Steps.Count>shown){shown=run.Steps.Count;Console.WriteLine("LIVE file action "+shown+": "+run.Steps.Last().Tool+" "+run.Steps.Last().Path);} };
    var work=service.SendAsync("In this synthetic test folder, read note.txt, change only beta to gamma, create result.ts containing export const result = 42; on one line, then read and delete obsolete.txt. Use the file tools and finish with a short summary. Do not run commands.",saved);
    if(await Task.WhenAny(work,Task.Delay(TimeSpan.FromSeconds(300)))!=work){service.Cancel();await work;Console.WriteLine("Diagnostic stopped after 300 seconds (test harness only).");return 1;}
    await work;var run=service.Messages.Last().Agent!;
    var livePassed=service.Error==""&&await File.ReadAllTextAsync(Path.Combine(root,"note.txt"))=="alpha\ngamma\n"&&File.Exists(Path.Combine(root,"result.ts"))&&!File.Exists(Path.Combine(root,"obsolete.txt"));
    Console.WriteLine($"LIVE agent: success={livePassed}; steps={run.Steps.Count}; changed={run.Changes.Count}; status={service.Error}");
    var undone=run.Undo();Console.WriteLine("LIVE recovery: "+undone);return livePassed&&undone=="Changes undone."?0:1;
}
if(args.Contains("--media-check")) { MediaChecks.Run().GetAwaiter().GetResult(); return 0; }
if(args.Contains("--image-probe"))
{
    var saved=JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"YoWidgets","settings.json")))!; saved.Normalize();
    var media=AttachmentMedia.PrepareGif(MediaChecks.Gif(),CancellationToken.None); var ai=new AiService(); var clock=System.Diagnostics.Stopwatch.StartNew();
    var work=ai.SendAsync("Name the dominant color of each attached animation frame in chronological order. Answer in one short sentence.",saved,[media]);
    if(await Task.WhenAny(work,Task.Delay(TimeSpan.FromSeconds(90)))!=work) { ai.Cancel(); Console.WriteLine("Diagnostic stopped after 90 seconds; this limit exists only in the test harness.");return 1; }
    await work; Console.WriteLine($"Synthetic GIF vision ({clock.Elapsed.TotalSeconds:0.00}s): "+(ai.Error.Length>0?ai.Error:ai.Answer));return ai.Error.Length==0?0:1;
}
if(args.Contains("--asset-check"))
{
    Exception? failure=null; var thread=new Thread(()=> { try {
        var app=new App(); Ui.Theme(); WidgetVisuals.Tone(false);
        var resources=typeof(App).Assembly.GetManifestResourceNames().Where(n=>n.EndsWith(".svg")).ToArray();
        foreach(var resource in resources) { var brand=resource.Contains(".Brands."); var name=resource.Split('.')[^2]; var icon=new PremiumIcon(name,24,"WidgetInk",brand); icon.Measure(new System.Windows.Size(24,24)); icon.Arrange(new System.Windows.Rect(0,0,24,24)); var image=new System.Windows.Media.Imaging.RenderTargetBitmap(24,24,96,96,System.Windows.Media.PixelFormats.Pbgra32); image.Render(icon); }
        Console.WriteLine($"PASS native vector rendering: {resources.Length} bundled SVG assets");
    } catch(Exception e) { failure=e; } }); thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(failure!=null)throw failure;return 0;
}
if (args.Contains("--ui-preview")) { var thread = new Thread(() => AssistantPreview.Run(args)); thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join(); return 0; }
if (args.Contains("--speech-status")) { Console.WriteLine("Installed speech engines: " + string.Join(", ", System.Speech.Recognition.SpeechRecognitionEngine.InstalledRecognizers().Select(r => r.Culture.Name))); return 0; }
if (args.Contains("--voice-probe"))
{
    var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"YoWidgets","settings.json"); var saved = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path))!; saved.Normalize();
    using var wav = new MemoryStream(); using(var synth = new System.Speech.Synthesis.SpeechSynthesizer()) { var english = synth.GetInstalledVoices().FirstOrDefault(v => v.VoiceInfo.Culture.TwoLetterISOLanguageName == "en"); if (english != null) synth.SelectVoice(english.VoiceInfo.Name); synth.SetOutputToWaveStream(wav); synth.Speak(args.Contains("--voice-probe-long") ? "Please create a small desktop chat widget with a transparent glass background. Put the microphone beside the send button, and keep the conversation easy to read. The weather is sunny today." : "The weather is sunny today."); }
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
    try { var timer=System.Diagnostics.Stopwatch.StartNew(); var transcript = await new AiService().TranscribeAsync(wav.ToArray(),saved,timeout.Token); Console.WriteLine($"Synthetic voice transcription ({timer.Elapsed.TotalSeconds:0.00}s): " + transcript); return transcript.Length > 0 ? 0 : 1; }
    catch(Exception e) { Console.WriteLine("Voice probe: " + (e is InvalidOperationException ? e.Message : e.GetType().Name)); return 1; }
}
if (args.Contains("--native-probe"))
{
    var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YoWidgets", "settings.json");
    var saved = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path))!; saved.Normalize();
    var secret = (string)typeof(AiService).GetMethod("Unprotect", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, [saved.AiKeyProtected])!;
    using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(25) };
    using var request = AssistantProtocol.CreateRequest("Gemini", args.LastOrDefault(a => a.StartsWith("gemini-")) ?? saved.AiModel, secret, [new("user", "Reply OK.")]);
    request.Version = new Version(2, 0); request.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
    request.RequestUri = new Uri(request.RequestUri!.ToString().Replace(":streamGenerateContent?alt=sse", ":generateContent"));
    request.Headers.Accept.Clear();
    request.Content = new StringContent("{\"contents\":[{\"parts\":[{\"text\":\"Reply OK.\"}]}],\"generationConfig\":{\"maxOutputTokens\":128}}", Encoding.UTF8, "application/json");
    try { using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead); Console.WriteLine("Native POST HTTP " + (int)response.StatusCode); using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25)); var body = await response.Content.ReadAsStringAsync(timeout.Token); Console.WriteLine("Native body bytes=" + body.Length); if (!response.IsSuccessStatusCode) { using var doc = JsonDocument.Parse(body); Console.WriteLine("Native error=" + doc.RootElement.GetProperty("error").GetProperty("message").GetString()!.Replace(secret, "[redacted]")); } }
    catch (Exception e) { Console.WriteLine("Native POST failed: " + e.GetType().Name); }
    return 0;
}
if (args.Contains("--send-saved"))
{
    var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YoWidgets", "settings.json");
    var saved = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path))!; saved.Normalize();
    saved.AiModel = args.LastOrDefault(a => a.StartsWith("gemini-")) ?? saved.AiModel;
    var service = new AiService(); service.Changed += () => Console.WriteLine($"LIVE state: busy={service.Busy}; textLength={service.Answer.Length}; status={service.Status}");
    IReadOnlyList<AssistantAttachment>? files = args.Contains("--with-file") ? [new("verification.txt","text/plain","","Attachment verification.",24)] : null;
    await service.SendAsync("Reply with exactly OK.", saved, files);
    Console.WriteLine(service.Error.Length == 0 ? "LIVE streaming success: " + service.Answer : "LIVE streaming failed: " + service.Error); return service.Error.Length == 0 ? 0 : 1;
}
if (args.Contains("--check-saved"))
{
    var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YoWidgets", "settings.json");
    var saved = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path))!; saved.Normalize();
    try { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25)); var models = await AiService.CheckSavedAsync(saved, timeout.Token); Console.WriteLine($"LIVE {saved.AiProvider}: key accepted; {models.Count} models available; configured model available={models.Contains(saved.AiModel)}"); if(args.Contains("--models")) Console.WriteLine(string.Join("\n",models)); return 0; }
    catch (Exception e) { Console.WriteLine("LIVE connection failed: " + (e is InvalidOperationException ? e.Message : e.GetType().Name)); return 1; }
}
// Every non-live transport test owns its output/cache; never use the real app profile.
var testDataRoot=Path.GetFullPath("artifacts/assistant-tests/run-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(testDataRoot);typeof(App).GetProperty(nameof(App.DataRoot))!.SetValue(null,testDataRoot);
int passed = 0, total = 0;
async Task Test(string name, Func<Task> test) { total++; try { await test(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { Console.WriteLine("FAIL " + name + ": " + e.Message); } }
void Require(bool condition) { if (!condition) throw new Exception("contract failed"); }
AppSettings Settings() => new() { AiProvider = "Gemini", AiModel = "gemini-flash-latest", AiKeyProtected = AiService.Protect("fake-test-key") };
string Event(string text, bool finish = false) => "data: " + JsonSerializer.Serialize(new { candidates = new[] { new { content = new { parts = new[] { new { text } } }, finishReason = finish ? "STOP" : (string?)null } } }) + "\r\n\r\n";
HttpResponseMessage Response(string body, HttpStatusCode code = HttpStatusCode.OK) => new(code) { Content = new StringContent(body, Encoding.UTF8, "text/event-stream") };
await Test("UTF8 SSE framing preserves multiline data, comments and final event", async () => {
    await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(": heartbeat\r\ndata: first\r\ndata: second\r\n\r\ndata: Привет"));
    var data = new List<string>(); await foreach (var e in AssistantProtocol.ReadEventsAsync(stream, CancellationToken.None)) data.Add(e);
    Require(data.SequenceEqual(new[] { "first\nsecond", "Привет" }));
});
await Test("streamed response retains context for follow-up without duplicating turns", async () => {
    var requests = new List<string>();
    using var client = new HttpClient(new Handler(async (r,t) => { requests.Add(await r.Content!.ReadAsStringAsync(t)); return Response(Event("Hello ") + Event("world", true)); }));
    var ai = new AiService(client); var s = Settings();
    await ai.SendAsync("first", s); Require(ai.Answer == "Hello world" && ai.Error == "" && !ai.Busy);
    await ai.SendAsync("follow-up", s);
    using var j = JsonDocument.Parse(requests[1]); Require(j.RootElement.GetProperty("contents").GetArrayLength() == 3);
});
await Test("failed request preserves user prompt and retry replaces failed attempt", async () => {
    int count = 0;
    using var client = new HttpClient(new Handler((r,t) => Task.FromResult(++count == 1 ? Response("", HttpStatusCode.TooManyRequests) : Response(Event("Recovered", true)))));
    var ai = new AiService(client); var s = Settings(); await ai.SendAsync("keep me", s); Require(ai.Error.Contains("quota") && ai.CanRetry);
    await ai.RetryAsync(s); Require(ai.Messages.Count == 2 && ai.Answer == "Recovered" && ai.Messages[0].Text == "keep me");
});
await Test("cancellation cannot apply late response after a new conversation", async () => {
    var pending = new TaskCompletionSource<HttpResponseMessage>();
    using var client = new HttpClient(new Handler((r,t) => pending.Task)); var ai = new AiService(client); var work = ai.SendAsync("old", Settings());
    Require(ai.Busy); ai.NewChat(); pending.SetResult(Response(Event("stale", true))); await work;
    Require(!ai.Busy && ai.Messages.Count == 0 && ai.Error == "");
});
await Test("missing credentials keep the draft and never call the network", async () => {
    using var client = new HttpClient(new Handler((r,t) => throw new Exception("network called"))); var ai = new AiService(client) { Draft = "unsent" };
    await ai.SendAsync("unsent", new AppSettings()); Require(ai.Messages.Count == 0 && ai.Draft == "unsent" && ai.Error.Contains("API key"));
});
await Test("interrupted stream does not report a complete response", async () => {
    using var client = new HttpClient(new Handler((r,t) => Task.FromResult(Response(Event("partial"))))); var ai = new AiService(client);
    await ai.SendAsync("hello", Settings()); Require(ai.Error.Length > 0 && !ai.Messages.Last().Complete && ai.Answer == "partial");
});
await Test("new connection does not leak previous conversation to another account", async () => {
    var requests = new List<string>(); using var client = new HttpClient(new Handler(async (r,t) => { requests.Add(await r.Content!.ReadAsStringAsync(t)); return Response(Event("ok", true)); }));
    var ai = new AiService(client); var s = Settings(); await ai.SendAsync("private old account", s); s.AiKeyProtected = AiService.Protect("another-key"); await ai.SendAsync("new account", s);
    Require(!requests[1].Contains("private old account"));
});
await Test("Gemini terminal event completes without waiting for the server to close SSE", async () => {
    using var client = new HttpClient(new Handler((r,t) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new OpenTail(Encoding.UTF8.GetBytes(Event("done", true)))) })));
    var ai = new AiService(client); var pending = ai.SendAsync("hello", Settings());
    var finished = await Task.WhenAny(pending, Task.Delay(1000));
    if (finished != pending) { ai.Cancel(); await pending; throw new Exception("waited for SSE close"); }
    await pending; Require(ai.Answer == "done" && ai.Error == "");
});
await Test("a new prompt excludes incomplete previous turns from request context", async () => {
    int count = 0; string sent = "";
    using var client = new HttpClient(new Handler(async (r,t) => { sent = await r.Content!.ReadAsStringAsync(t); return ++count == 1 ? Response("", HttpStatusCode.ServiceUnavailable) : Response(Event("new", true)); }));
    var ai = new AiService(client); var s = Settings(); await ai.SendAsync("failed old prompt", s); await ai.SendAsync("different prompt", s);
    Require(!sent.Contains("failed old prompt") && sent.Contains("different prompt"));
});
await Test("attachment-only messages transmit file contents and remain retryable", async () => {
    string sent = ""; using var client = new HttpClient(new Handler(async (r,t) => { sent = await r.Content!.ReadAsStringAsync(t); return Response(Event("file read",true)); }));
    var ai = new AiService(client); var file = new AssistantAttachment("sample.txt","text/plain","","attachment payload",18);
    await ai.SendAsync("",Settings(),[file]); Require(ai.Error == "" && sent.Contains("attachment payload") && ai.Messages[0].Attachments.Count == 1);
    await ai.RetryAsync(Settings()); Require(ai.Error == "" && sent.Contains("attachment payload") && ai.Messages.Count == 2);
});
await Test("PDF and image inputs use native provider content blocks", () => {
    var pdf = new AssistantAttachment("sample.pdf","application/pdf","JVBERg==",null,4);
    var image = new AssistantAttachment("sample.png","image/png","iVBORw==",null,4);
    using var gemini = AssistantProtocol.CreateRequest("Gemini","gemini-flash-lite-latest","key",[new("user","read",[pdf,image])]);
    using var claude = AssistantProtocol.CreateRequest("Claude","claude-sonnet-4-6","key",[new("user","read",[pdf,image])]);
    using var openai = AssistantProtocol.CreateRequest("OpenAI","gpt-4.1-mini","key",[new("user","read",[pdf,image])]);
    Require(gemini.Content!.ReadAsStringAsync().Result.Contains("inline_data"));
    Require(claude.Content!.ReadAsStringAsync().Result.Contains("media_type"));
    var body = openai.Content!.ReadAsStringAsync().Result; Require(body.Contains("file_data") && body.Contains("image_url")); return Task.CompletedTask;
});
await Test("attachments incompatible with a service fail visibly before network access", async () => {
    using var client = new HttpClient(new Handler((r,t) => throw new Exception("network must not run")));
    var ai = new AiService(client); var s = Settings(); s.AiProvider="DeepSeek";s.AiModel="deepseek-chat";
    await ai.SendAsync("read",s,[new("sample.pdf","application/pdf","JVBERg==",null,4)]); Require(ai.Error.Contains("cannot read") && ai.CanRetry);
});
await Test("file reader keeps Unicode and rejects binary contents without guessing", async () => {
    var root = Path.GetFullPath("artifacts/assistant-files"); Directory.CreateDirectory(root);
    var path = Path.Combine(root,"sample.txt"); await File.WriteAllTextAsync(path,"Attachment test: Привет");
    var file = await AssistantAttachment.ReadAsync(path,CancellationToken.None); Require(file.Text == "Attachment test: Привет" && file.Name == "sample.txt");
    var binary = Path.Combine(root,"sample.bin"); await File.WriteAllBytesAsync(binary,[0,1,2,3]);
    try { await AssistantAttachment.ReadAsync(binary,CancellationToken.None); throw new Exception("binary accepted as text"); } catch(InvalidOperationException) { }
});
await Test("Office attachment text is extracted without executing macros or embedded content", async () => {
    var root = Path.GetFullPath("artifacts/assistant-files"); Directory.CreateDirectory(root); var path = Path.Combine(root,"sample.docx");
    using (var zip = new System.IO.Compression.ZipArchive(File.Create(path),System.IO.Compression.ZipArchiveMode.Create)) { using var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open()); writer.Write("<document><p><r><t>Office attachment</t></r></p></document>"); }
    var file = await AssistantAttachment.ReadAsync(path,CancellationToken.None); Require(file.Text!.Contains("Office attachment"));
});
await Test("Gemini uses dedicated private transcription outside chat history", async () => {
    string path="",body=""; using var client = new HttpClient(new Handler(async (r,t) => { path=r.RequestUri!.AbsolutePath; body=await r.Content!.ReadAsStringAsync(t); return new HttpResponseMessage(HttpStatusCode.OK) { Content=new StringContent("{\"status\":\"completed\",\"steps\":[{\"type\":\"model_output\",\"content\":[{\"type\":\"text\",\"text\":\"A spoken draft.\"}]}]}") }; }));
    var ai = new AiService(client); var transcript = await ai.TranscribeAsync([1,2,3,4],Settings(),CancellationToken.None);
    using var payload=JsonDocument.Parse(body);
    Require(path=="/v1beta/interactions" && payload.RootElement.GetProperty("model").GetString()=="gemini-3.5-transcribe" && !payload.RootElement.GetProperty("store").GetBoolean());
    Require(payload.RootElement.GetProperty("input")[0].GetProperty("data").GetString()=="AQIDBA==" && !body.Contains("fake-test-key"));
    Require(transcript == "A spoken draft." && ai.Messages.Count == 0 && !ai.Busy);
});
await Test("Incomplete Gemini transcription is never inserted as a completed draft", async () => {
    using var client=new HttpClient(new Handler((r,t)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=new StringContent("{\"status\":\"failed\",\"output_text\":\"partial\"}") })));
    try { await new AiService(client).TranscribeAsync([1,2],Settings(),CancellationToken.None); throw new Exception("Accepted incomplete transcript"); } catch(InvalidOperationException e) { Require(e.Message.Contains("incomplete")); }
});
await Test("OpenAI voice input sends multipart audio to the fixed transcription endpoint", async () => {
    string path="", body=""; using var client = new HttpClient(new Handler(async (r,t) => { path=r.RequestUri!.AbsolutePath; body=await r.Content!.ReadAsStringAsync(t); return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"text\":\"A draft\"}") }; }));
    var ai = new AiService(client); var s=Settings();s.AiProvider="OpenAI"; var text=await ai.TranscribeAsync([1,2,3,4],s,CancellationToken.None);
    Require(path=="/v1/audio/transcriptions" && body.Contains("gpt-4o-mini-transcribe") && text=="A draft" && ai.Messages.Count==0);
});
await Test("GIF storyboard reaches every vision adapter as images with chronological labels", async () => {
    var gif=new AssistantAttachment("motion.gif","image/gif","raw-gif-is-never-sent",null,10) { Frames=[new("image/jpeg","frame-red",0),new("image/jpeg","frame-blue",.5)] };
    foreach(var provider in new[]{"Gemini","OpenAI","Claude","OpenRouter"}) { using var request=AssistantProtocol.CreateRequest(provider,AssistantProtocol.DefaultModel(provider),"key",[new("user","describe",[gif])]); var body=await request.Content!.ReadAsStringAsync(); Require(body.Contains("frame-red") && body.Contains("frame-blue") && body.Contains("chronological") && !body.Contains("raw-gif-is-never-sent") && !body.Contains("image/gif")); }
});
await Test("Video stays native for Gemini and supplies visual frames to other vision services", async () => {
    var video=new AssistantAttachment("clip.mp4","video/mp4","original-video",null,10) { Frames=[new("image/jpeg","video-poster",0)] };
    using var google=AssistantProtocol.CreateRequest("Gemini",AssistantProtocol.DefaultModel("Gemini"),"key",[new("user","watch",[video])]); Require((await google.Content!.ReadAsStringAsync()).Contains("original-video"));
    using var claude=AssistantProtocol.CreateRequest("Claude",AssistantProtocol.DefaultModel("Claude"),"key",[new("user","watch",[video])]); var body=await claude.Content!.ReadAsStringAsync(); Require(body.Contains("video-poster") && body.Contains("audio is not included") && !body.Contains("original-video"));
});
await Test("Unsupported animated input fails before sending raw GIF bytes", () => {
    try { using var request=AssistantProtocol.CreateRequest("Gemini",AssistantProtocol.DefaultModel("Gemini"),"key",[new("user","watch",[new("clip.gif","image/gif","raw",null,3)])]); throw new Exception("raw GIF accepted"); } catch(InvalidOperationException e) { Require(e.Message.Contains("add it again")); } return Task.CompletedTask;
});
if(args.Contains("--slow-tests")) await Test("Provider headers may take longer than 30 seconds without local cancellation", async () => {
    using var client=new HttpClient(new Handler(async (r,t)=>{ await Task.Delay(TimeSpan.FromSeconds(31.2),t); return Response(Event("arrived after 30 seconds",true)); }));
    var ai=new AiService(client); await ai.SendAsync("wait",Settings()); Require(ai.Error=="" && ai.Answer=="arrived after 30 seconds" && ai.Messages.Last().Complete);
});
await GeneratedMediaChecks.Run(Test,Require);
await GenerationConnectionChecks.Run(Test,Require);
await AgentChecks.Run(Test,Require);
await ComposerChecks.Run(Test,Require);
await Test("Voice task browser/computer capabilities stay scoped to the request", async()=>{
    string body="";using var client=new HttpClient(new Handler(async(r,t)=>{body=await r.Content!.ReadAsStringAsync(t);return Response(Event("Observed fixture result",true));}));var ai=new AiService(client);await ai.SendAsync("Synthetic scoped task",Settings(),taskPlugins:["browser","computer"]);Require(ai.Error.Length==0&&ai.Messages.Last().Agent!=null&&body.Contains("browser_open")&&body.Contains("computer_inspect")&&!ai.Plugins.Contains("browser")&&!ai.Plugins.Contains("computer"));
});
await Test("Queued voice transcript survives cancelling its chat task without later duplication", async()=>{
    var entered=new TaskCompletionSource();using var client=new HttpClient(new Handler(async(r,t)=>{entered.SetResult();await Task.Delay(Timeout.Infinite,t);return Response("");}));var ai=new AiService(client);var settings=Settings();var work=ai.SendAsync("Synthetic cancellable task",settings);await entered.Task;ai.AcceptVoiceTranscript(settings.AiProvider,settings.AiKeyProtected!,[("user","A spoken request"),("assistant","A spoken reply")]);ai.Cancel();await work;Require(ai.Messages.Count(m=>m.Text=="A spoken reply")==1&&ai.Messages.Count(m=>m.Text=="A spoken request")==1&&!ai.Busy);
});
Console.WriteLine($"RESULT: {passed}/{total} passed"); return total == passed ? 0 : 1;
sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request, token);
}
sealed class OpenTail(byte[] bytes) : Stream
{
    int offset;
    public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false; public override long Length => throw new NotSupportedException(); public override long Position { get => offset; set => throw new NotSupportedException(); }
    public override async ValueTask<int> ReadAsync(Memory<byte> target, CancellationToken token = default)
    {
        if (offset == bytes.Length) { await Task.Delay(Timeout.Infinite, token); return 0; }
        int length = Math.Min(target.Length, bytes.Length - offset); bytes.AsMemory(offset, length).CopyTo(target); offset += length; return length;
    }
    public override int Read(byte[] buffer, int start, int count) => throw new NotSupportedException();
    public override void Flush() { } public override long Seek(long x, SeekOrigin y) => throw new NotSupportedException(); public override void SetLength(long x) => throw new NotSupportedException(); public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();
}
