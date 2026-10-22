using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
namespace YoWidgets;

internal interface IVoiceSocket:IDisposable
{
    Task Connect(string provider,string model,string key,CancellationToken token);
    Task Send(string json,CancellationToken token);
    Task<string?> Receive(CancellationToken token);
    void Abort();
}
internal sealed class VoiceSocket:IVoiceSocket
{
    readonly ClientWebSocket socket=new();
    public async Task Connect(string provider,string model,string key,CancellationToken token)
    {
        socket.Options.KeepAliveInterval=TimeSpan.FromSeconds(15);
        Uri uri;
        if(provider=="Gemini")uri=new("wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key="+Uri.EscapeDataString(key));
        else{socket.Options.SetRequestHeader("Authorization","Bearer "+key);uri=new("wss://api.openai.com/v1/realtime?model="+Uri.EscapeDataString(model));}
        await socket.ConnectAsync(uri,token);
    }
    public Task Send(string json,CancellationToken token)=>socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)),WebSocketMessageType.Text,true,token);
    public async Task<string?> Receive(CancellationToken token)
    {
        using var message=new MemoryStream();var buffer=new byte[16384];WebSocketReceiveResult result;
        // Gemini also delivers its JSON envelopes in binary WebSocket frames.
        do{result=await socket.ReceiveAsync(new ArraySegment<byte>(buffer),token);if(result.MessageType==WebSocketMessageType.Close){if(!string.IsNullOrEmpty(result.CloseStatusDescription))throw new InvalidOperationException(LiveVoiceProtocol.SafeError(JsonSerializer.SerializeToElement(new{message=result.CloseStatusDescription})));return null;}if(message.Length+result.Count>4*1024*1024)throw new InvalidOperationException("The voice service sent an oversized message.");message.Write(buffer,0,result.Count);}while(!result.EndOfMessage);
        return Encoding.UTF8.GetString(message.ToArray());
    }
    public void Abort()=>socket.Abort();
    public void Dispose()=>socket.Dispose();
}
internal interface IVoiceAudio:IDisposable
{
    event Action<byte[]>? Captured;
    void Start(int rate);void Pause();void Play(byte[] pcm);void Clear();
    long PlayedBytes{get;}long SubmittedBytes{get;}bool Playing{get;}double InputLevel{get;}double OutputLevel{get;}
}
internal sealed class VoiceAudio:IVoiceAudio
{
    readonly AudioRecorder mic;readonly PcmSpeaker speaker;
    public VoiceAudio(Dispatcher dispatcher){mic=new(dispatcher);speaker=new(dispatcher);mic.PcmCaptured+=pcm=>Captured?.Invoke(pcm);}
    public event Action<byte[]>? Captured;
    public void Start(int rate){speaker.Open();mic.StartStreaming(rate);if(!mic.Recording)throw new InvalidOperationException(mic.Error);}
    public void Pause()=>mic.Stop(false);
    public void Play(byte[] pcm)=>speaker.Write(pcm);
    public void Clear()=>speaker.Clear();
    public long PlayedBytes=>speaker.PlayedBytes;
    public long SubmittedBytes=>speaker.SubmittedBytes;
    public bool Playing=>speaker.Playing;
    public double InputLevel=>mic.Level;
    public double OutputLevel=>speaker.Level;
    public void Dispose(){mic.Dispose();speaker.Dispose();}
}

internal sealed class LiveVoiceSession:IDisposable
{
    static LiveVoiceSession? owner;
    internal static bool HasActiveSession=>owner?.Active==true;
    readonly string provider,model,voice,key,context;
    readonly IVoiceSocket socket;
    readonly IVoiceAudio audio;
    readonly CancellationTokenSource stop=new();
    readonly Channel<(object Value,int Epoch)> outbound=Channel.CreateBounded<(object,int)>(new BoundedChannelOptions(80){SingleReader=true,SingleWriter=false});
    readonly TaskCompletionSource ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
    int epoch;bool started,disposed,thinking,skipAudio;
    string item="";int contentIndex;long itemStart;
    readonly List<(string Role,string Text)> transcript=[];
    readonly Dictionary<string,int> transcriptItems=[];
    readonly Channel<IReadOnlyList<AgentToolCall>> toolBatches=Channel.CreateBounded<IReadOnlyList<AgentToolCall>>(8);
    readonly HashSet<string> seenCalls=[];
    readonly Dictionary<string,CancellationTokenSource> calls=[];
    readonly HashSet<string> cancelledCalls=[];
    readonly List<string> actionLog=[];
    public string Language {get;set;}="auto";
    internal Func<AgentToolCall,CancellationToken,Task<AgentToolResult>>? ExecuteTool {get;set;}
    internal string CurrentAction {get;private set;}="";
    internal IReadOnlyList<string> ActionLog=>actionLog;
    internal void ReportAction(string text){CurrentAction=text;Changed?.Invoke();}
    public bool Active{get;private set;}
    public bool Connected{get;private set;}
    public bool Muted{get;private set;}
    public string Error{get;private set;}="";
    public DateTimeOffset? ConnectedAt{get;private set;}
    public string State=>!Active?Error.Length>0?"Call unavailable":"Call ended":!Connected?"Connecting…":audio.Playing?"Speaking…":CurrentAction.Length>0?"Working…":Muted?"Microphone off":thinking?"Thinking…":"Listening…";
    public double Level=>Connected?audio.Playing?audio.OutputLevel:Muted?0:audio.InputLevel:0;
    public IReadOnlyList<(string Role,string Text)> Transcript=>transcript;
    public event Action? Changed;
    internal LiveVoiceSession(string provider,string model,string voice,string key,string context="",IVoiceSocket? socket=null,IVoiceAudio? audio=null)
    {this.provider=provider;this.model=model;this.voice=voice;this.key=key;this.context=context;this.socket=socket??new VoiceSocket();this.audio=audio??new VoiceAudio(Dispatcher.CurrentDispatcher);this.audio.Captured+=Capture;}
    public async Task Run()
    {
        if(started||disposed)return;started=true;
        if(Interlocked.CompareExchange(ref owner,this,null)!=null){Error="A voice call is already open in another widget.";Changed?.Invoke();Dispose();return;}
        Active=true;Changed?.Invoke();Task? writer=null;Task? reader=null;Task? tools=null;
        try
        {
            if(!VoiceCallOptions.Supported(provider))throw new InvalidOperationException("Live calls are available with Gemini and OpenAI chat connections.");
            using var handshake=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);handshake.CancelAfter(TimeSpan.FromSeconds(25));
            await socket.Connect(provider,model,key,handshake.Token);
            await socket.Send(JsonSerializer.Serialize(LiveVoiceProtocol.Setup(provider,model,voice,context,Language,ExecuteTool!=null)),handshake.Token);
            writer=WriteLoop();tools=ToolLoop();reader=ReadLoop();
            await ready.Task.WaitAsync(handshake.Token);stop.Token.ThrowIfCancellationRequested();
            audio.Start(provider=="Gemini"?16000:24000);stop.Token.ThrowIfCancellationRequested();ConnectedAt=DateTimeOffset.Now;Connected=true;Changed?.Invoke();
            await reader;
        }
        catch(OperationCanceledException){if(!stop.IsCancellationRequested)Error="Voice connection timed out. Check your connection and live-model access.";}
        catch(Exception e){if(!stop.IsCancellationRequested)Error=e is InvalidOperationException?e.Message:"Could not connect to the voice API. Check your chat key, internet connection and live model in Settings.";}
        finally
        {
            Stop();
            if(writer!=null)try{await writer;}catch(Exception){}
            if(reader!=null)try{await reader;}catch(Exception){}
            if(tools!=null)try{await tools;}catch(Exception){}
            Dispose();Changed?.Invoke();
        }
    }
    void Capture(byte[] pcm){if(Active&&Connected&&!Muted)Queue(LiveVoiceProtocol.Audio(provider,pcm),epoch);}
    internal void SendText(string text)
    {
        if(!Connected||text.Length is <1 or >16000)return;
        if(provider=="Gemini")Queue(new{clientContent=new{turns=new[]{new{role="user",parts=new[]{new{text}}}},turnComplete=true}});
        else{Queue(new{type="conversation.item.create",item=new{type="message",role="user",content=new[]{new{type="input_text",text}}}});Queue(new{type="response.create"});}
    }
    void Queue(object value,int audioEpoch=-1){if(Active&&!outbound.Writer.TryWrite((value,audioEpoch)))Fail("The voice connection is too slow. Start a new call.");}
    async Task WriteLoop()
    {
        try{await foreach(var value in outbound.Reader.ReadAllAsync(stop.Token)){if(value.Epoch>=0&&(value.Epoch!=epoch||Muted))continue;await socket.Send(JsonSerializer.Serialize(value.Value),stop.Token);}}
        catch(Exception){if(!stop.IsCancellationRequested)Fail("Voice connection lost. Start a new call.");}
    }
    async Task ReadLoop()
    {
        try
        {
            while(!stop.IsCancellationRequested)
            {
                var json=await socket.Receive(stop.Token);if(json==null){Fail("The voice service closed the call. Check API access or start a new call.");return;}
                foreach(var update in LiveVoiceProtocol.Parse(provider,json))
                {
                    if(stop.IsCancellationRequested)return;
                    if(update.Kind=="ready")ready.TrySetResult();
                    else if(update.Kind=="tools"){
                        var batch=new List<AgentToolCall>();foreach(var call in update.Calls??[]){if(call.Id.Length is <1 or >200||call.Arguments.GetRawText().Length>24000){Fail("The voice service returned invalid tool arguments.");return;}if(seenCalls.Count>=256){Fail("Voice action limit reached. Start a new call.");return;}if(seenCalls.Add(call.Id))batch.Add(call);}
                        if(batch.Count>0&&!toolBatches.Writer.TryWrite(batch)){Fail("Too many voice actions are pending. Start a new call.");return;}
                    }
                    else if(update.Kind=="cancel-tools"){foreach(var id in JsonSerializer.Deserialize<string[]>(update.Text)??[]){cancelledCalls.Add(id);if(calls.TryGetValue(id,out var running))running.Cancel();}}
                    else if(update.Kind=="error"){Fail(update.Text);return;}
                    else if(update.Kind=="interrupt")
                    {
                        if(provider=="OpenAI"&&update.Item.Length>0)AddTranscript("user","",false,update.Item,true);
                        if(provider=="OpenAI"&&item.Length>0&&audio.Playing)Queue(LiveVoiceProtocol.Truncate(item,contentIndex,(int)Math.Max(0,(audio.PlayedBytes-itemStart)/48)));
                        audio.Clear();item="";itemStart=0;thinking=false;skipAudio=provider=="OpenAI";
                    }
                    else if(update.Kind=="response"){skipAudio=false;thinking=true;}
                    else if(update.Kind=="thinking")thinking=true;
                    else if(update.Kind=="done")thinking=false;
                    else if(update.Kind=="audio"&&!skipAudio)
                    {
                        if(!Connected)continue;
                        if(item!=update.Item){item=update.Item;contentIndex=update.ContentIndex;itemStart=audio.SubmittedBytes;}
                        audio.Play(update.Pcm!);thinking=false;
                    }
                    else if(update.Kind is "user" or "user-final" or "assistant")AddTranscript(update.Kind.StartsWith("user")?"user":"assistant",update.Text,update.Kind=="user-final",update.Item);
                    Changed?.Invoke();
                }
            }
        }
        catch(Exception e){if(!stop.IsCancellationRequested)Fail(e is InvalidOperationException?e.Message:"The voice connection was interrupted. Start a new call.");}
    }
    async Task ToolLoop()
    {
        try{await foreach(var batch in toolBatches.Reader.ReadAllAsync(stop.Token)){
            var returned=false;
            foreach(var call in batch){
                if(stop.IsCancellationRequested)return;if(cancelledCalls.Contains(call.Id))continue;
                using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);cancellation.CancelAfter(TimeSpan.FromMinutes(5));calls[call.Id]=cancellation;
                var clock=System.Diagnostics.Stopwatch.StartNew();CurrentAction=call.Name=="widget_command"?"Using widgets…":"Working with browser and apps…";Changed?.Invoke();AgentToolResult result;var wasCancelled=false;
                try{if(ExecuteTool==null||call.Name is not("execute_task" or "widget_command"))throw new InvalidOperationException("This voice action is not enabled.");result=await ExecuteTool(call,cancellation.Token);}
                catch(OperationCanceledException){wasCancelled=true;result=new(call,JsonSerializer.Serialize(new{ok=false,error="Action cancelled. Do not claim it completed. Changes already made may remain."}),true);}
                catch(Exception e){result=new(call,JsonSerializer.Serialize(new{ok=false,error=e is InvalidOperationException?e.Message:"The voice action failed. Check its arguments or try again."}),true);}
                finally{calls.Remove(call.Id);}
                actionLog.Add((call.Name=="widget_command"?"Widgets":"Browser and apps")+" · "+(wasCancelled?"Stopped":result.Error?"Failed":"Completed")+" · "+clock.Elapsed.TotalSeconds.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+" s");if(actionLog.Count>50)actionLog.RemoveAt(0);
                if(!stop.IsCancellationRequested&&!cancelledCalls.Contains(call.Id)){Queue(LiveVoiceProtocol.ToolResponse(provider,result));returned=true;}
            }
            CurrentAction="";Changed?.Invoke();if(provider=="OpenAI"&&returned&&!stop.IsCancellationRequested)Queue(new{type="response.create"});
        }}catch(OperationCanceledException){}finally{CurrentAction="";}
    }
    void AddTranscript(string role,string text,bool final,string itemId,bool placeholder=false)
    {
        if(string.IsNullOrEmpty(text)&&!placeholder)return;
        if(itemId.Length>0){
            if(transcriptItems.TryGetValue(itemId,out var index)){var value=final?text:transcript[index].Text+text;transcript[index]=(role,value[..Math.Min(12000,value.Length)]);}
            else{transcriptItems[itemId]=transcript.Count;transcript.Add((role,text[..Math.Min(12000,text.Length)]));}
        }else if(!final&&transcript.Count>0&&transcript[^1].Role==role){var last=transcript[^1];transcript[^1]=(role,(last.Text+text)[..Math.Min(12000,last.Text.Length+text.Length)]);}
        else transcript.Add((role,text[..Math.Min(12000,text.Length)]));
        if(transcript.Count>100){transcript.RemoveAt(0);foreach(var key in transcriptItems.Keys.ToArray()){if(transcriptItems[key]==0)transcriptItems.Remove(key);else transcriptItems[key]--;}}
    }
    public void ToggleMute()
    {
        if(!Active||!Connected)return;Muted=!Muted;epoch++;
        if(Muted){audio.Pause();Queue(LiveVoiceProtocol.Mute(provider));}
        else try{audio.Start(provider=="Gemini"?16000:24000);}catch(InvalidOperationException e){Fail(e.Message);}
        Changed?.Invoke();
    }
    void Fail(string message){Error=message;ready.TrySetException(new InvalidOperationException(message));Stop();}
    public void Stop(){if(!stop.IsCancellationRequested){stop.Cancel();socket.Abort();audio.Pause();audio.Clear();}Active=false;Connected=false;Interlocked.CompareExchange(ref owner,null,this);Changed?.Invoke();}
    public void Dispose(){if(disposed)return;Stop();disposed=true;audio.Captured-=Capture;audio.Dispose();socket.Dispose();}
}
