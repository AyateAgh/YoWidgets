using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace YoWidgets;

internal sealed class ChatMessage(string role, string text, IReadOnlyList<AssistantAttachment>? attachments = null)
{
    public string Role { get; } = role;
    public string Text { get; set; } = text;
    public bool Complete { get; set; }
    public IReadOnlyList<AssistantAttachment> Attachments { get; } = attachments ?? [];
    public IReadOnlyList<string> PluginInstructions {get;init;}=[];
    public AgentWorkspace? Agent { get; set; }
    internal List<GeneratedMediaJob> Media {get;}=[];
}

internal sealed class AiService
{
    static readonly HttpClient sharedHttp = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
    readonly HttpClient transport;
    public AiService(HttpClient? transport = null) { this.transport = transport ?? sharedHttp; }
    readonly List<ChatMessage> messages = [];
    readonly List<(string Provider,string Key,IReadOnlyList<(string Role,string Text)> Turns)> pendingVoice=[];
    readonly Dictionary<string,GeneratedMedia> mediaLibrary=[];
    internal GeneratedMedia? DraftMediaReference {get;private set;}
    internal void ReferenceMedia(GeneratedMedia media,string prompt="Describe the changes you want to make")
    {if(Busy)return;mediaLibrary[media.Id]=media;DraftMediaReference=media;Draft=prompt;Changed?.Invoke();}
    internal void RemoveMediaReference(){DraftMediaReference=null;Changed?.Invoke();}
    CancellationTokenSource? request;
    int generation;
    internal int RequestVersion=>generation;
    string connection = "";
    public IReadOnlyList<ChatMessage> Messages => messages;
    public bool Busy { get; private set; }
    public string Error { get; private set; } = "";
    public string Status { get; private set; } = "";
    public AssistantPhase Phase { get; private set; }=AssistantPhase.Done;
    public AgentToolCall? CurrentTool { get; private set; }
    internal string ToolProgress {get;private set;}="";
    internal void ReportTool(string progress){ToolProgress=progress;Changed?.Invoke();}
    public BrowserAssistantBridge? Browser {get;set;}
    internal NativeBrowserTabs NativeTabs {get;}=new();
    internal BrowserPageReader LocalBrowser {get;}=new();
    internal string ControlModelName {get;private set;}="AI";
    internal void CloseBrowser()=>LocalBrowser.Dispose();
    internal void WaitingApproval(bool waiting) {if(!Busy)return;Phase=waiting?AssistantPhase.WaitingApproval:AssistantPhase.ToolCall;Changed?.Invoke();}
    public string Draft { get; set; } = "";
    public List<AssistantAttachment> DraftAttachments { get; } = [];
    public int SessionVersion { get; private set; }
    public string Answer => messages.LastOrDefault(m => m.Role == "assistant")?.Text ?? "";
    public bool CanRetry => !Busy && messages.Any(m => m.Role == "user");
    public event Action? Changed;
    internal Func<string,string,CancellationToken,Task<bool>>? ToolApproval {get;set;}
    internal bool AskBeforeControl {get;set;}
    public string? WorkspaceRoot { get; private set; }
    string? sessionRoot;
    public string? EffectiveWorkspaceRoot=>WorkspaceRoot??sessionRoot;
    public bool GoalEnabled { get; private set; }
    public bool PlanningEnabled { get; private set; }
    public string GoalText { get; private set; }="";
    public HashSet<string> Plugins { get; }=new(StringComparer.Ordinal);
    public HashSet<string> DraftPluginInstructions {get;}=new(StringComparer.Ordinal);
    public void SelectPluginInstruction(string id) {if(Busy||!PluginCatalog.Known(id))return;Plugins.Add(id);DraftPluginInstructions.Add(id);Changed?.Invoke();}
    public void RemovePluginInstruction(string id) {DraftPluginInstructions.Remove(id);Changed?.Invoke();}
    public void SetGoal(bool enabled) {if(Busy)return;GoalEnabled=enabled;if(!enabled)GoalText="";Changed?.Invoke();}
    public void SetPlanning(bool enabled) {if(Busy)return;PlanningEnabled=enabled;Changed?.Invoke();}
    public void TogglePlugin(string id) {if(Busy)return;if(!Plugins.Remove(id))Plugins.Add(id);Changed?.Invoke();}
    public void SetWorkspace(string? path)
    {
        if(path!=null)path=new AgentWorkspace(path).Root;
        if(WorkspaceRoot==path)return;
        NewChat();WorkspaceRoot=path;Changed?.Invoke();
    }
    public static string Protect(string secret) => Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes("YoWidgets.AI.v1"), DataProtectionScope.CurrentUser));
    static string Unprotect(string secret) => Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(secret), Encoding.UTF8.GetBytes("YoWidgets.AI.v1"), DataProtectionScope.CurrentUser));
    internal static string UnlockGenerationKey(string value)=>Unprotect(value);
    internal void AcceptVoiceTranscript(string provider,string protectedKey,IReadOnlyList<(string Role,string Text)> turns)
    {
        if(turns.Count==0)return;var target=provider+":"+protectedKey;if(connection.Length>0&&connection!=target)return;
        if(Busy){pendingVoice.Add((provider,protectedKey,turns.ToArray()));return;}connection=target;
        foreach(var turn in turns.Where(t=>t.Role is "user" or "assistant"&&!string.IsNullOrWhiteSpace(t.Text)))messages.Add(new ChatMessage(turn.Role,turn.Text){Complete=true});
        Changed?.Invoke();
    }
    public void NewChat()
    {
        pendingVoice.Clear();
        Cancel(); SessionVersion++; messages.Clear(); DraftAttachments.Clear();DraftPluginInstructions.Clear();DraftMediaReference=null; Error = Status = Draft = ""; connection = "";CurrentTool=null;Phase=AssistantPhase.Done;GoalText="";sessionRoot=null; Changed?.Invoke();
    }
    public void ConfigurationChanged(AppSettings? settings=null) {if(settings!=null&&connection==settings.AiProvider+":"+settings.AiKeyProtected){Cancel();Changed?.Invoke();return;}WorkspaceRoot=null;NewChat(); }
    public Task RetryAsync(AppSettings settings)
    {
        if (!CanRetry) return Task.CompletedTask;
        var last = messages.FindLastIndex(m => m.Role == "user");
        var text = messages[last].Text;
        var files = messages[last].Attachments;
        var instructions=messages[last].PluginInstructions;
        if(messages.Skip(last).Any(m=>m.Agent!=null))return SendAsync("Continue this task from its current state. Inspect existing files before further changes: "+text,settings,files,instructions);
        messages.RemoveRange(last, messages.Count - last);
        return SendAsync(text, settings, files,instructions);
    }
    public async Task SendAsync(string text, AppSettings settings, IReadOnlyList<AssistantAttachment>? attachments = null,IReadOnlyList<string>? pluginInstructions=null,IReadOnlyList<string>? taskPlugins=null)
    {
        if (Busy || string.IsNullOrWhiteSpace(text) && (attachments == null || attachments.Count == 0)) return;
        attachments = attachments?.ToArray() ?? [];
        if (attachments.Count > AssistantAttachment.MaxFiles || attachments.Sum(f => f.PayloadBytes) > AssistantAttachment.MaxTotalBytes) { Error = "Attach up to 5 files with a combined size of 12 MB including prepared media frames."; Changed?.Invoke(); return; }
        if (text.Length > AssistantProtocol.MaxInput) { Error = "This message is too long. Use fewer than 32,000 characters."; Changed?.Invoke(); return; }
        if (string.IsNullOrWhiteSpace(settings.AiKeyProtected)) { Error = "Add your API key in Settings to start chatting."; Changed?.Invoke(); return; }
        var selectedInstructions=(pluginInstructions??DraftPluginInstructions.ToArray()).Where(PluginCatalog.Known).Distinct().ToArray();foreach(var id in selectedInstructions)Plugins.Add(id);
        var mediaIntent=System.Text.RegularExpressions.Regex.IsMatch(text,@"(?is)(generat\w*|draw|render|animate|edit|create|make|\u043d\u0430\u0440\u0438\u0441\w*|\u0441\u0433\u0435\u043d\u0435\u0440\w*|\u0441\u043e\u0437\u0434\u0430\w*|\u0437\u0433\u0435\u043d\u0435\u0440\w*|\u043d\u0430\u043c\u0430\u043b\w*|\u0430\u043d\u0438\u043c\u0438\u0440\w*|\u043f\u0435\u0440\u0435\u0434\u0435\u043b\w*|\u0438\u0437\u043c\u0435\u043d\u0438\w*).{0,100}(image|picture|photo|video|gif|animation|\u0438\u0437\u043e\u0431\u0440\u0430\u0436\w*|\u043a\u0430\u0440\u0442\u0438\u043d\w*|\u0444\u043e\u0442\u043e|\u0432\u0438\u0434\u0435\u043e|\u0432\u0456\u0434\u0435\u043e|\u0430\u043d\u0456\u043c\u0430\u0446\w*|\u0430\u043d\u0438\u043c\u0430\u0446\w*)");
        mediaIntent|=System.Text.RegularExpressions.Regex.IsMatch(text,@"(?is)(generat\w*|create|make|render|speak|synthesize|\u0441\u043e\u0437\u0434\u0430\w*|\u0441\u0433\u0435\u043d\u0435\u0440\w*|\u0437\u0433\u0435\u043d\u0435\u0440\w*|\u043e\u0437\u0432\u0443\u0447\w*).{0,100}(audio|music|speech|sound|3d|\u0430\u0443\u0434\u0438\u043e|\u0430\u0443\u0434\u0456\u043e|\u043c\u0443\u0437\u044b\u043a\w*|\u043c\u0443\u0437\u0438\u043a\w*|\u0437\u0432\u0443\u043a\w*|\u0440\u0435\u0447\w*)");
        if(mediaIntent||DraftMediaReference!=null||messages.Any(m=>m.Media.Any(j=>j.Result!=null)))Plugins.Add("media");
        foreach(var file in attachments) {var plugin=Path.GetExtension(file.Name).ToLowerInvariant() switch {".docx"=>"documents",".pdf"=>"pdf",".xlsx"=>"spreadsheets",".pptx"=>"presentations",_=>null};if(plugin!=null)Plugins.Add(plugin);if(file.BrowserTabId!=null)Plugins.Add("browser");}
        AgentWorkspace? agent=null;
        try {
            if(WorkspaceRoot==null&&(GoalEnabled||PlanningEnabled||Plugins.Count>0||taskPlugins?.Count>0)) {sessionRoot??=Path.Combine(App.OwnedDirectory("Assistant"),"Sessions",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(sessionRoot);}
            if(EffectiveWorkspaceRoot!=null)agent=new AgentWorkspace(EffectiveWorkspaceRoot);
        }catch(Exception e) when(e is InvalidOperationException or IOException or UnauthorizedAccessException){Error=e is InvalidOperationException?e.Message:"Choose an available working folder.";Changed?.Invoke();return;}
        if(GoalEnabled&&GoalText.Length==0)GoalText=text.Trim();
        var identity = settings.AiProvider + ":" + settings.AiKeyProtected;
        if (connection.Length > 0 && connection != identity) messages.Clear();
        connection = identity;
        var v = ++generation;
        using var cts = new CancellationTokenSource();
        request = cts;
        Busy = true; Error = ""; Status = "Thinking…";Phase=AssistantPhase.Thinking;CurrentTool=null;ToolProgress="";ControlModelName=settings.AiModel;AskBeforeControl=settings.AiAskBeforeControl;
        messages.Add(new("user", text.Trim(), attachments) { Complete = true,PluginInstructions=selectedInstructions });
        var context = new List<AssistantTurn>();
        int length = 0; long fileBytes = 0;
        for (var i = messages.Count - 1; i >= 0; i--)
        {
            var m = messages[i];
            if (!m.Complete || m.Text.Length == 0 && m.Attachments.Count == 0) continue;
            if (m.Role == "user" && i != messages.Count - 1 && (i + 1 >= messages.Count || messages[i + 1].Role != "assistant" || !messages[i + 1].Complete)) continue;
            var content=m.Text.Length==0&&m.Attachments.Count>0?"Read the attached files.":m.Text;
            if(m.Role=="user")content=PluginCatalog.Prompt(content,m.PluginInstructions);
            if (length + content.Length > 64000 || context.Count >= 40) break;
            if (fileBytes + m.Attachments.Sum(f => f.PayloadBytes) > AssistantAttachment.MaxTotalBytes) break;
            context.Insert(0, new(m.Role,content,m.Attachments)); length += content.Length; fileBytes += m.Attachments.Sum(f => f.PayloadBytes);
        }
        while (context.Count > 0 && context[0].Role != "user") context.RemoveAt(0);
        var answer = new ChatMessage("assistant", ""); messages.Add(answer);
        var mediaReference=DraftMediaReference;DraftMediaReference=null;
        if(agent!=null) { answer.Agent=agent;agent.Changed+=NotifyAgent; }
        Draft = ""; DraftAttachments.Clear();DraftPluginInstructions.Clear(); Changed?.Invoke();
        var buffer = new StringBuilder();
        try
        {
            if(settings.AiModel.Contains("image",StringComparison.OrdinalIgnoreCase)||settings.AiModel.Contains("nano-banana",StringComparison.OrdinalIgnoreCase)||settings.AiModel.Contains("omni",StringComparison.OrdinalIgnoreCase)) {
                if(PlanningEnabled)throw new InvalidOperationException("Choose a text model with tools for Planning mode, or turn Planning off before direct media generation.");
                var motion=System.Text.RegularExpressions.Regex.Replace(text,@"(?i)\b(no|without|\u0431\u0435\u0437|\u043d\u0435)\s+(video|animation|animated|gif|\u0432\u0438\u0434\u0435\u043e|\u0432\u0456\u0434\u0435\u043e|\u0430\u043d\u0438\u043c\w*|\u0430\u043d\u0456\u043c\w*)\b","");
                var kind=System.Text.RegularExpressions.Regex.IsMatch(motion,@"(?i)\b(gif|animated|animation|animate|\u0430\u043d\u0438\u043c\w*|\u0430\u043d\u0456\u043c\w*)\b")?"animation":settings.AiModel.Contains("omni",StringComparison.OrdinalIgnoreCase)||System.Text.RegularExpressions.Regex.IsMatch(motion,@"(?i)\b(video|\u0432\u0438\u0434\u0435\u043e|\u0432\u0456\u0434\u0435\u043e)\b")?"video":"image";
                if(settings.AiModel.Contains("omni",StringComparison.OrdinalIgnoreCase)&&!System.Text.RegularExpressions.Regex.IsMatch(motion,@"(?i)\b(video|movie|clip|animation|animate|animated|gif|\u0432\u0438\u0434\u0435\u043e|\u0432\u0456\u0434\u0435\u043e|\u0430\u043d\u0438\u043c\w*|\u0430\u043d\u0456\u043c\w*)\b"))throw new InvalidOperationException("This selected model generates video. Explicitly request a video/animation, or select a text/image model.");
                var aspect=System.Text.RegularExpressions.Regex.Match(text,@"\b(1:1|3:4|4:3|9:16|16:9)\b");
                await GenerateMedia(answer,settings,kind,text,aspect.Success?aspect.Value:kind=="image"?"1:1":"16:9",mediaReference,attachments.FirstOrDefault(f=>f.MimeType.StartsWith("image/")||f.MimeType.StartsWith("video/")),cts.Token,v);
                answer.Complete=true;buffer.Append(kind=="image"?"Created an image.":"Created a video.");return;
            }
            if(answer.Agent!=null) { await RunAgentAsync(answer,context,settings,cts.Token,v,buffer,mediaReference,taskPlugins);return; }
            using var outgoing = AssistantProtocol.CreateRequest(settings.AiProvider, settings.AiModel, Unprotect(settings.AiKeyProtected), context);
            using var response = await transport.SendAsync(outgoing, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException(AssistantProtocol.ErrorMessage((int)response.StatusCode));
            if (v != generation) return;
            Status = "Generating…"; Changed?.Invoke();
            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            long lastUpdate = -100;
            string? finish = null;
            await foreach (var data in AssistantProtocol.ReadEventsAsync(stream, cts.Token))
            {
                if (v != generation) return;
                finish = AssistantProtocol.FinishReason(settings.AiProvider, data) ?? finish;
                if (data == "[DONE]") break;
                var delta = AssistantProtocol.ParseDelta(settings.AiProvider, data);
                if(delta.Length>0)Phase=AssistantPhase.Streaming;
                var remaining = AssistantProtocol.MaxAnswer - buffer.Length;
                buffer.Append(delta.AsSpan(0, Math.Min(delta.Length, remaining)));
                if (delta.Length > remaining) throw new InvalidOperationException("Response limit reached. Ask the assistant to continue in a new message.");
                if (finish != null) { answer.Text = buffer.ToString(); break; }
                if (clock.ElapsedMilliseconds - lastUpdate < 60) continue;
                answer.Text = buffer.ToString(); Status = "Writing…"; lastUpdate = clock.ElapsedMilliseconds; Changed?.Invoke();
            }
            if (v != generation) return;
            answer.Text = buffer.ToString();
            if (finish == null) throw new InvalidOperationException("Connection interrupted before the response finished. Try again.");
            if (string.IsNullOrWhiteSpace(answer.Text)) throw new InvalidOperationException("The service returned no text. Try another message or model.");
            answer.Complete = true; Status = "";
            if (finish is "MAX_TOKENS" or "max_tokens" or "length") Error = "The model reached its response limit. Ask it to continue in your next message.";
        }
        catch (OperationCanceledException) { if (v == generation) { Error = "The service interrupted this request. Try again."; Status = ""; } }
        catch (CryptographicException) { if (v == generation) Error = "Windows could not unlock the saved key. Enter the key again in Settings."; }
        catch (FormatException) { if (v == generation) Error = "The saved key is damaged. Enter it again in Settings."; }
        catch (Exception e) { if (v == generation) Error = e is InvalidOperationException or ArgumentException ? e.Message : "Connection interrupted. Check your internet connection and try again."; }
        finally
        {
            if(answer.Agent!=null) {answer.Agent.Changed-=NotifyAgent;answer.Agent.Finish(answer.Complete?(answer.Agent.Steps.Count==0?"No file actions":answer.Agent.Steps.Any(s=>s.Status=="Failed")?"Finished · review tool errors":"Completed"):cts.IsCancellationRequested?"Stopped":"Needs attention");}
            if (v == generation) { answer.Text = buffer.ToString(); request = null; Busy = false; Status = "";CurrentTool=null;Phase=answer.Complete?AssistantPhase.Done:AssistantPhase.Error;var queued=pendingVoice.ToArray();pendingVoice.Clear();foreach(var voice in queued)AcceptVoiceTranscript(voice.Provider,voice.Key,voice.Turns);Changed?.Invoke(); }
        }
    }
    void NotifyAgent()=>Changed?.Invoke();
    async Task RunAgentAsync(ChatMessage answer,IReadOnlyList<AssistantTurn> context,AppSettings settings,CancellationToken token,int version,StringBuilder buffer,GeneratedMedia? mediaReference=null,IReadOnlyList<string>? taskPlugins=null)
    {
        var workspace=answer.Agent!;var secret=Unprotect(settings.AiKeyProtected!);
        var inputs=context.SelectMany(t=>t.Attachments??[]).Distinct().ToArray();
        using var runtime=new AssistantToolRuntime(this,workspace,inputs,context.LastOrDefault(t=>t.Role=="user")?.Content??"",token,taskPlugins);
        if(ToolApproval!=null)runtime.Approve=ToolApproval;
        runtime.MediaContext="Configured generation backends: "+GenerationConnections.Describe(settings)+". Each uses its own saved credentials; never ask for keys in chat or invent account access. Generated media available in this chat: "+JsonSerializer.Serialize(messages.SelectMany(m=>m.Media).Where(j=>j.Result!=null).Select(j=>j.Result!).Append(mediaReference).Where(m=>m!=null).DistinctBy(m=>m!.Id).Select(m=>new{id=m!.Id,mime_type=m.MimeType,prompt=m.Prompt,model=m.Model}))+ (mediaReference!=null?". The user explicitly selected reference "+mediaReference.Id:"");
        runtime.GenerateMedia=async(call,t)=>{
            var args=call.Arguments;var reference=args.GetProperty("reference").GetString()??"";GeneratedMedia? prior=null;AssistantAttachment? input=null;
            if(reference.Length>0){if(reference.StartsWith("attachment:")&&int.TryParse(reference[11..],out var index)&&index>=0&&index<inputs.Length)input=inputs[index];else if(mediaLibrary.TryGetValue(reference,out var media)&&(messages.SelectMany(m=>m.Media).Any(j=>j.Result?.Id==reference)||mediaReference?.Id==reference))prior=media;else throw new InvalidOperationException("Choose a media ID from this chat or an attached file.");}
            var generated=await GenerateMedia(answer,settings,args.GetProperty("kind").GetString()??"image",args.GetProperty("prompt").GetString()??"",args.GetProperty("aspect_ratio").GetString()??"1:1",prior,input,t,version);
            return new AgentToolResult(call,JsonSerializer.Serialize(new{id=generated.Id,mime_type=generated.MimeType,model=generated.Model,status="Generated and displayed in chat. Use this ID for follow-up edits; the original is preserved."}));
        };
        var outgoingContext=new List<AssistantTurn>();foreach(var turn in context) {
            var files=new List<AssistantAttachment>();foreach(var file in turn.Attachments??[]) {
                if(settings.AiProvider=="DeepSeek"&&file.MimeType=="application/pdf"&&Plugins.Contains("pdf")) {
                    var bytes=file.Bytes();string text;try {text=await Task.Run(()=>JsonSerializer.Serialize(DocumentEngine.Inspect(file.Name,bytes,0,48000)),token);}catch(Exception e) when(e is not OperationCanceledException and not OutOfMemoryException){throw new InvalidOperationException("The PDF plugin cannot read this document. It may be damaged, encrypted or unsupported.");}files.Add(file with{MimeType="text/plain",Text=text,OriginalData=file.Data,Data=""});
                }else files.Add(file);
            }outgoingContext.Add(turn with{Attachments=files});
        }
        using var initial=AssistantProtocol.CreateRequest(settings.AiProvider,settings.AiModel,secret,outgoingContext);
        var body=await AgentProtocol.InitialBodyAsync(initial,settings.AiProvider,token,runtime.Instructions,runtime.Tools(settings.AiProvider));
        int actions=0;var prefix="";
        for(var iteration=0;iteration<32;iteration++)
        {
            token.ThrowIfCancellationRequested();if(version!=generation)return;
            if(body.ToJsonString().Length>8_000_000)throw new InvalidOperationException("This task's tool context is full. Start a new task with a smaller scope; current changes remain reviewable.");
            using var outgoing=AssistantProtocol.CreateRequest(settings.AiProvider,settings.AiModel,secret,[]);AgentProtocol.SetBody(outgoing,body);
            Status="Thinking…";Phase=AssistantPhase.Thinking;CurrentTool=null;Changed?.Invoke();
            using var response=await transport.SendAsync(outgoing,HttpCompletionOption.ResponseHeadersRead,token);
            if(!response.IsSuccessStatusCode)throw new InvalidOperationException(AssistantProtocol.ErrorMessage((int)response.StatusCode)+(response.StatusCode==System.Net.HttpStatusCode.BadRequest?" This model may not support file tools; select a tool-capable model.":""));
            var round=new AgentRound(settings.AiProvider);await using var stream=await response.Content.ReadAsStreamAsync(token);var clock=System.Diagnostics.Stopwatch.StartNew();long last=-100;
            await foreach(var data in AssistantProtocol.ReadEventsAsync(stream,token))
            {
                token.ThrowIfCancellationRequested();if(version!=generation)return;round.Add(data);
                if(round.Text.Length>0)Phase=AssistantPhase.Streaming;
                if(prefix.Length+round.Text.Length>AssistantProtocol.MaxAnswer)throw new InvalidOperationException("This task's displayed response is full. Continue in another message.");
                buffer.Clear().Append(prefix).Append(round.Text);
                if(round.Finish!=null)break;
                if(clock.ElapsedMilliseconds-last<60)continue;answer.Text=buffer.ToString();last=clock.ElapsedMilliseconds;Changed?.Invoke();
            }
            token.ThrowIfCancellationRequested();if(version!=generation)return;
            answer.Text=buffer.ToString();Changed?.Invoke();
            if(round.Finish==null)throw new InvalidOperationException("The connection ended before the model finished its step. No incomplete tool call was executed.");
            var calls=round.ToolCalls();
            if(calls.Count==0) {
                if(round.Finish is "MAX_TOKENS" or "length" or "max_tokens")throw new InvalidOperationException("The model reached its output limit. Review changes and ask it to continue.");
                if(string.IsNullOrWhiteSpace(answer.Text))throw new InvalidOperationException("The model returned no text or file actions. Choose a model that supports tools.");
                answer.Complete=true;return;
            }
            if(round.Finish is not ("STOP" or "tool_calls" or "tool_use" or "stop"))throw new InvalidOperationException("The model did not complete its tool call. No action was executed.");
            if(actions+calls.Count>96)throw new InvalidOperationException("This task reached 96 file actions. Review the changes and continue in a new message.");
            var results=new List<AgentToolResult>();
            foreach(var call in calls) {
                token.ThrowIfCancellationRequested();if(version!=generation)return;CurrentTool=call;ToolProgress="";Phase=AssistantPhase.ToolCall;Changed?.Invoke();
                try {
                    runtime.GuardMutation(call.Name);
                    results.Add(runtime.IsCustom(call.Name)?await workspace.ExecuteCustomAsync(call,t=>runtime.Execute(call,t),token):await workspace.ExecuteAsync(call,token));
                }catch(InvalidOperationException e) {results.Add(await workspace.ExecuteCustomAsync(call,_=>Task.FromResult(new AgentToolResult(call,JsonSerializer.Serialize(new{error=e.Message}),true)),token));}
                actions++;
                token.ThrowIfCancellationRequested();if(version!=generation)return;Phase=AssistantPhase.ToolResult;Changed?.Invoke();
            }
            AgentProtocol.AppendRound(body,settings.AiProvider,round,results);
            prefix=buffer.ToString();if(prefix.Length>0&&!prefix.EndsWith("\n\n"))prefix+="\n\n";
        }
        throw new InvalidOperationException("This task reached 32 model steps. Review the changes and ask the agent to continue.");
    }
    public void Cancel()
    {
        if (!Busy) return;
        generation++; request?.Cancel(); request = null; Busy = false; Status = "Stopped"; Error = "";CurrentTool=null;Phase=AssistantPhase.Stopped;foreach(var job in messages.SelectMany(m=>m.Media).Where(j=>!j.Finished)){job.Finished=true;job.Error="Stopped";job.Update("Stopped");}var queued=pendingVoice.ToArray();pendingVoice.Clear();foreach(var voice in queued)AcceptVoiceTranscript(voice.Provider,voice.Key,voice.Turns);Changed?.Invoke();
    }
    async Task<GeneratedMedia> GenerateMedia(ChatMessage answer,AppSettings settings,string kind,string prompt,string aspect,GeneratedMedia? reference,AssistantAttachment? input,CancellationToken token,int version)
    {
        if(answer.Media.Any(j=>j.Finished&&j.Result==null))throw new InvalidOperationException("The media request failed. Report the displayed error and wait for the user to retry; do not repeat generation automatically.");
        if(answer.Media.Count>=4)throw new InvalidOperationException("This request reached four media generations. Review the results before requesting more.");
        var job=new GeneratedMediaJob(kind,prompt,aspect);answer.Media.Add(job);void Update(){if(version==generation)Changed?.Invoke();}job.Changed+=Update;Update();
        try {
            var connection=GenerationConnections.Resolve(settings,kind);var model=connection.Model;
            if(GenerationConnections.Get(settings,kind).Mode=="chat"&&(kind=="image"&&(settings.AiModel.Contains("image",StringComparison.OrdinalIgnoreCase)||settings.AiModel.Contains("nano-banana",StringComparison.OrdinalIgnoreCase))||kind is "video" or "animation"&&settings.AiModel.Contains("omni",StringComparison.OrdinalIgnoreCase)))model=settings.AiModel;
            var media=await new GeneratedMediaService(transport,connection.Provider,Unprotect(connection.KeyProtected),model,model,connection.Voice).Generate(job,reference,input,token);
            token.ThrowIfCancellationRequested();if(version!=generation)throw new OperationCanceledException(token);job.Result=media;mediaLibrary[media.Id]=media;job.Finished=true;job.Update("Completed");return media;
        }catch(Exception e){job.Finished=true;job.Error=e is OperationCanceledException?"Stopped":e is InvalidOperationException?e.Message:"Media generation failed. Check the service and try again.";job.Update(job.Error);throw;}
        finally {job.Changed-=Update;Update();}
    }
    public static async Task<IReadOnlyList<string>> CheckConnectionAsync(string provider, string secret, CancellationToken token)
    {
        var models = new List<string>();
        string? next = null;
        for (var page = 0; page < 10; page++)
        {
            using var query = AssistantProtocol.ModelsRequest(provider, secret, next);
            using var response = await sharedHttp.SendAsync(query, token);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException(AssistantProtocol.ErrorMessage((int)response.StatusCode));
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(token), cancellationToken: token);
            if (provider == "Gemini")
            {
                foreach (var model in doc.RootElement.GetProperty("models").EnumerateArray())
                    if (model.TryGetProperty("supportedGenerationMethods", out var methods) && methods.EnumerateArray().Any(m => m.GetString() == "generateContent"))
                        models.Add((model.GetProperty("name").GetString() ?? "").Replace("models/", ""));
                next = doc.RootElement.TryGetProperty("nextPageToken", out var n) ? n.GetString() : null;
                if (string.IsNullOrEmpty(next)) break;
            }
            else
            {
                models.AddRange(doc.RootElement.GetProperty("data").EnumerateArray().Select(m => m.GetProperty("id").GetString() ?? "").Where(m => provider != "OpenAI" || m.StartsWith("gpt-") || m.StartsWith("o3") || m.StartsWith("o4")));
                break;
            }
        }
        return models.Distinct().OrderBy(m => m).ToArray();
    }
    public static Task<IReadOnlyList<string>> CheckSavedAsync(AppSettings settings, CancellationToken token) => CheckConnectionAsync(settings.AiProvider, Unprotect(settings.AiKeyProtected!), token);
    public async Task<string> TranscribeAsync(byte[] wav, AppSettings settings, CancellationToken token)
    {
        if (string.IsNullOrEmpty(settings.AiKeyProtected)) throw new InvalidOperationException("Add your API key in Settings to use voice input.");
        if (settings.AiProvider is not ("Gemini" or "OpenAI")) throw new InvalidOperationException("Voice input for this service needs an installed Windows speech language.");
        if (wav.Length > 2 * 1024 * 1024) throw new InvalidOperationException("This recording is too long. Record a shorter message.");
        var secret = Unprotect(settings.AiKeyProtected);
        using var request = new HttpRequestMessage(HttpMethod.Post,settings.AiProvider == "Gemini" ? "https://generativelanguage.googleapis.com/v1beta/interactions" : "https://api.openai.com/v1/audio/transcriptions") { Version = new Version(2,0), VersionPolicy = HttpVersionPolicy.RequestVersionOrLower };
        if (settings.AiProvider == "Gemini")
        {
            request.Headers.Add("x-goog-api-key",secret);
            request.Content = new StringContent(JsonSerializer.Serialize(new { model = "gemini-3.5-transcribe", input = new[] { new { type = "audio", data = Convert.ToBase64String(wav), mime_type = "audio/wav" } }, store = false }),Encoding.UTF8,"application/json");
        }
        if (settings.AiProvider == "OpenAI")
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",secret);
            var form = new MultipartFormDataContent(); var audio = new ByteArrayContent(wav); audio.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav"); form.Add(audio,"file","voice.wav"); form.Add(new StringContent("gpt-4o-mini-transcribe"),"model"); request.Content = form;
        }
        using var response = await transport.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(AssistantProtocol.ErrorMessage((int)response.StatusCode));
        if (settings.AiProvider == "OpenAI") { using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(token),cancellationToken:token); return json.RootElement.GetProperty("text").GetString() ?? ""; }
        using var result = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(token),cancellationToken:token);
        var root = result.RootElement;
        if (root.TryGetProperty("status",out var status) && status.GetString() != "completed") throw new InvalidOperationException("Voice transcription was incomplete. Try recording again.");
        var text = new StringBuilder();
        if (root.TryGetProperty("output_text",out var direct)) text.Append(direct.GetString());
        else if (root.TryGetProperty("steps",out var steps))
            foreach(var step in steps.EnumerateArray())
                if(step.TryGetProperty("type",out var type) && type.GetString()=="model_output" && step.TryGetProperty("content",out var parts))
                    foreach(var part in parts.EnumerateArray()) if(part.TryGetProperty("type",out var kind) && kind.GetString()=="text" && part.TryGetProperty("text",out var value)) text.Append(value.GetString());
        if(text.Length>AssistantProtocol.MaxInput) throw new InvalidOperationException("The transcript is too long. Record a shorter message.");
        return text.ToString().Trim();
    }
}
