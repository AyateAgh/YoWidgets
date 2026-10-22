using System.Text.Json;
using System.Threading.Channels;
using YoWidgets;
internal static class LiveVoiceChecks
{
    internal static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    internal static async Task Until(Func<bool> predicate){using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(4));while(!predicate())await Task.Delay(10,stop.Token);}
    internal sealed class Socket:IVoiceSocket
    {
        readonly Channel<string> inbound=Channel.CreateUnbounded<string>();
        internal readonly List<string> Sent=[];internal bool Aborted;internal bool AutoReady=true;
        string provider="";
        public Task Connect(string p,string model,string key,CancellationToken token){provider=p;return Task.CompletedTask;}
        public Task Send(string json,CancellationToken token){lock(Sent)Sent.Add(json);if(AutoReady&&(json.Contains("\"setup\"")||json.Contains("session.update")))Emit(provider=="Gemini"?"{\"setupComplete\":{}}":"{\"type\":\"session.updated\"}");return Task.CompletedTask;}
        internal void Emit(string json)=>inbound.Writer.TryWrite(json);
        public async Task<string?> Receive(CancellationToken token)=>await inbound.Reader.ReadAsync(token);
        internal bool SentType(string text){lock(Sent)return Sent.Any(s=>s.Contains(text));}
        internal string[] Snapshot(){lock(Sent)return Sent.ToArray();}
        public void Abort()=>Aborted=true;public void Dispose(){}
    }
    internal sealed class Audio:IVoiceAudio
    {
        public event Action<byte[]>? Captured;
        internal bool Recording,Disposed;internal int Rate,Starts,Pauses,Clears;internal byte[] Last=[];
        public long PlayedBytes{get;set;}
        public long SubmittedBytes{get;private set;}
        public bool Playing=>SubmittedBytes>PlayedBytes;
        public double InputLevel=>.35;public double OutputLevel=>.55;
        public void Start(int rate){Rate=rate;Starts++;Recording=true;}
        public void Pause(){Pauses++;Recording=false;}
        public void Play(byte[] pcm){Last=pcm;SubmittedBytes+=pcm.Length;}
        public void Clear(){Clears++;SubmittedBytes=PlayedBytes=0;}
        public void Dispose(){Disposed=true;Recording=false;}
        internal void Capture(byte[] pcm)=>Captured?.Invoke(pcm);
    }
    internal static async Task Run()
    {
        Require(VoiceCallOptions.Languages.SequenceEqual(new[]{"uk","en","auto"}),"Voice language choices must be Ukrainian, English and Auto only");
        var migrated=new VoiceCallOptions{Language="ru"};migrated.Normalize();Require(migrated.Language=="auto"&&new VoiceCallOptions().Language=="auto","Removed language must migrate to Auto");
        foreach(var provider in new[]{"Gemini","OpenAI"}){var migratedSetup=JsonSerializer.Serialize(LiveVoiceProtocol.Setup(provider,"fixture","fixture",language:"ru"));Require(migratedSetup.Contains("Conversation language: Auto")&&!migratedSetup.Contains("\"language\":\"ru\""),"Removed language leaked into live protocol");}
        var options=new VoiceCallOptions{GeminiModel="../../invalid?key=secret",GeminiVoice="unknown",OpenAIVoice="unknown"};options.Normalize();Require(options.GeminiModel=="gemini-3.8-live"&&options.GeminiVoice=="Aoede"&&options.OpenAIVoice=="marin","Invalid voice preferences were retained");
        Require(VoiceCallOptions.EmptyDraft(" \n",0,false)&&!VoiceCallOptions.EmptyDraft("a",0,false)&&!VoiceCallOptions.EmptyDraft("",1,false)&&!VoiceCallOptions.EmptyDraft("",0,true),"Start call gating ignored draft state");Console.WriteLine("PASS voice: draft gating and safe per-provider preference defaults");
        foreach(var provider in new[]{"Gemini","OpenAI"})
        {
            var setup=JsonSerializer.Serialize(LiveVoiceProtocol.Setup(provider,options.ModelFor(provider),options.VoiceFor(provider)));
            using var parsed=JsonDocument.Parse(setup);Require(!setup.Contains("secret")&&setup.Contains(options.VoiceFor(provider)),"Setup did not include chosen voice");
            if(provider=="Gemini")Require(parsed.RootElement.GetProperty("setup").GetProperty("generationConfig").GetProperty("responseModalities")[0].GetString()=="AUDIO","Wrong Gemini setup schema");
            else Require(parsed.RootElement.GetProperty("session").GetProperty("audio").GetProperty("input").GetProperty("format").GetProperty("rate").GetInt32()==24000,"Wrong OpenAI input format");
            var socket=new Socket{AutoReady=false};var audio=new Audio();using var call=new LiveVoiceSession(provider,options.ModelFor(provider),options.VoiceFor(provider),"fixture",socket:socket,audio:audio);var run=call.Run();await Task.Delay(30);Require(!audio.Recording&&!call.Connected,"Microphone opened before setup acknowledgement");socket.Emit(provider=="Gemini"?"{\"setupComplete\":{}}":"{\"type\":\"session.updated\"}");await Until(()=>call.Connected);Require(audio.Rate==(provider=="Gemini"?16000:24000),"Wrong capture sample rate");
            audio.Capture([1,0,2,0]);await Until(()=>socket.SentType("AQACAA=="));
            socket.Emit(provider=="Gemini"?"{\"serverContent\":{\"inputTranscription\":{\"text\":\"Hello\"},\"outputTranscription\":{\"text\":\"Hi\"},\"modelTurn\":{\"parts\":[{\"inlineData\":{\"mimeType\":\"audio/pcm;rate=24000\",\"data\":\"AQACAA==\"}}]}}}":"{\"type\":\"response.output_audio.delta\",\"item_id\":\"reply-1\",\"content_index\":0,\"delta\":\"AQACAA==\"}");await Until(()=>audio.Last.Length>0);Require(audio.Last.SequenceEqual(new byte[]{1,0,2,0}),"Output PCM was changed");
            call.ToggleMute();Require(call.Muted&&!audio.Recording,"Mute did not stop capture immediately");var count=socket.Sent.Count;audio.Capture([3,0]);await Task.Delay(30);Require(!socket.SentType("AwA="),"Muted microphone data leaked into outbound stream");call.ToggleMute();Require(!call.Muted&&audio.Recording&&audio.Starts==2,"Unmute did not reopen capture");
            socket.Emit(provider=="Gemini"?"{\"serverContent\":{\"interrupted\":true}}":"{\"type\":\"input_audio_buffer.speech_started\"}");await Until(()=>audio.Clears>0);if(provider=="OpenAI")await Until(()=>socket.SentType("conversation.item.truncate"));
            call.Stop();Require(!audio.Recording&&!audio.Playing&&socket.Aborted,"Stop left audio or connection active");await run;Require(audio.Disposed&&!call.Active&&call.Error.Length==0,"Cancellation was reported as an API failure");Console.WriteLine("PASS voice "+provider+": acknowledged setup, PCM stream, immediate mute/unmute, interruption and teardown");
        }
        {
            var socket=new Socket{AutoReady=false};var audio=new Audio();using var call=new LiveVoiceSession("Gemini","gemini-3.8-live","Aoede","fixture",socket:socket,audio:audio);var run=call.Run();call.Stop();socket.Emit("{\"setupComplete\":{}}");await run;Require(audio.Starts==0&&audio.Disposed,"Late acknowledgement reopened a canceled microphone");Console.WriteLine("PASS voice: cancel during connection never opens microphone");
        }
        {
            var socket=new Socket();var audio=new Audio();using var call=new LiveVoiceSession("Gemini","gemini-3.8-live","Aoede","fixture",socket:socket,audio:audio);var run=call.Run();await Until(()=>call.Connected);socket.Emit("{\"error\":{\"status\":\"RESOURCE_EXHAUSTED\",\"message\":\"quota secret-key\"}}");await run;Require(call.Error.Contains("quota")&&!call.Error.Contains("secret-key")&&audio.Disposed&&!audio.Recording,"Quota failure leaked data or retained microphone");Console.WriteLine("PASS voice: actionable quota errors redact server request data and release audio");
        }
        {
            var socket=new Socket();var audio=new Audio();using var call=new LiveVoiceSession("OpenAI","gpt-realtime-2.1","marin","fixture",socket:socket,audio:audio);var run=call.Run();await Until(()=>call.Connected);
            socket.Emit("{\"type\":\"input_audio_buffer.speech_started\",\"item_id\":\"question-1\"}");socket.Emit("{\"type\":\"response.created\"}");socket.Emit("{\"type\":\"response.output_audio_transcript.delta\",\"item_id\":\"reply-1\",\"delta\":\"Answer\"}");socket.Emit("{\"type\":\"conversation.item.input_audio_transcription.completed\",\"item_id\":\"question-1\",\"transcript\":\"Question\"}");await Until(()=>call.Transcript.Count==2&&call.Transcript[0].Text=="Question");Require(call.Transcript[1].Text=="Answer","Delayed input ASR changed turn ordering");call.Stop();await run;Console.WriteLine("PASS voice: delayed OpenAI transcription preserves user/assistant turn order");
        }
        {
            var firstSocket=new Socket();var firstAudio=new Audio();using var first=new LiveVoiceSession("OpenAI","gpt-realtime-2.1","marin","fixture",socket:firstSocket,audio:firstAudio);var run=first.Run();await Until(()=>first.Connected);var otherAudio=new Audio();using var other=new LiveVoiceSession("Gemini","gemini-3.8-live","Aoede","fixture",socket:new Socket(),audio:otherAudio);await other.Run();Require(other.Error.Contains("another widget")&&otherAudio.Starts==0&&first.Connected,"Concurrent calls captured the same microphone");first.Stop();await run;Console.WriteLine("PASS voice: only one widget can own a live microphone session");
        }
        var ai=new AiService();ai.AcceptVoiceTranscript("Gemini","encrypted",[("user","Hello"),("assistant","Hi")]);Require(ai.Messages.Count==2&&ai.Messages.All(m=>m.Complete),"Voice transcript did not become chat context");ai.AcceptVoiceTranscript("OpenAI","another",[("user","private")]);Require(ai.Messages.Count==2,"Another provider's voice transcript leaked into chat");Console.WriteLine("PASS voice: in-memory transcripts continue the same provider chat only");
        await Actions();
    }
    static async Task Actions()
    {
        foreach(var status in new[]{"cancelled","incomplete"}){var events=LiveVoiceProtocol.Parse("OpenAI",JsonSerializer.Serialize(new{type="response.done",response=new{status,output=new[]{new{type="function_call",call_id="partial",name="execute_task",arguments="{unfinished"}}}})).ToArray();Require(events.All(e=>e.Kind!="tools"),"Interrupted OpenAI arguments executed or broke the call");}Console.WriteLine("PASS voice actions: interrupted OpenAI function arguments are ignored without ending the call");
        foreach(var provider in new[]{"Gemini","OpenAI"}){
            using var setup=JsonDocument.Parse(JsonSerializer.Serialize(LiveVoiceProtocol.Setup(provider,"fixture","fixture","","uk",true)));var root=setup.RootElement;var instructions=provider=="Gemini"?root.GetProperty("setup").GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString()!:root.GetProperty("session").GetProperty("instructions").GetString()!;Require(instructions.Contains("Conversation language: Ukrainian")&&!instructions.Contains("conversation only"),"Ukrainian language anchor missing");if(provider=="OpenAI")Require(root.GetProperty("session").GetProperty("audio").GetProperty("input").GetProperty("transcription").GetProperty("language").GetString()=="uk","OpenAI transcription language missing");else Require(!root.GetRawText().Contains("languageCode"),"Unsupported Gemini languageCode was sent");
            var socket=new Socket();var audio=new Audio();var invoked=0;var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);using var call=new LiveVoiceSession(provider,"fixture","fixture","fixture",socket:socket,audio:audio){ExecuteTool=async(c,t)=>{invoked++;await release.Task.WaitAsync(t);return new(c,"{\"verified\":42}");}};var run=call.Run();await Until(()=>call.Connected);
            string Batch()=>provider=="Gemini"?JsonSerializer.Serialize(new{toolCall=new{functionCalls=new[]{new{id="tool-1",name="widget_command",args=new{action="state",arguments_json="{}"}},new{id="tool-2",name="widget_command",args=new{action="calendar_list",arguments_json="{}"}}}}}):JsonSerializer.Serialize(new{type="response.done",response=new{status="completed",output=new[]{new{type="function_call",call_id="tool-1",name="widget_command",arguments="{\"action\":\"state\",\"arguments_json\":\"{}\"}"},new{type="function_call",call_id="tool-2",name="widget_command",arguments="{\"action\":\"calendar_list\",\"arguments_json\":\"{}\"}"}}}});
            socket.Emit(Batch());socket.Emit(Batch());await Until(()=>invoked==1);audio.Capture([9,0]);await Until(()=>socket.SentType("CQA="));Require(call.CurrentAction.Length>0,"Action state not reported while tool pending");release.SetResult();await Until(()=>invoked==2&&call.ActionLog.Count==2);await Until(()=>socket.SentType("verified"));if(provider=="OpenAI"){await Until(()=>socket.SentType("response.create"));Require(socket.Snapshot().Count(s=>s.Contains("response.create"))==1,"OpenAI resumed more than once for a batch");}call.Stop();await run;Require(invoked==2,"Duplicate tool calls ran more than once");Console.WriteLine("PASS voice actions "+provider+": native function schema, Ukrainian anchor, streaming during tools, batch continuation and duplicate suppression");
        }
        {
            var socket=new Socket();var audio=new Audio();var entered=false;var cancelled=false;using var call=new LiveVoiceSession("Gemini","fixture","fixture","fixture",socket:socket,audio:audio){ExecuteTool=async(c,t)=>{entered=true;try{await Task.Delay(Timeout.Infinite,t);}catch(OperationCanceledException){cancelled=true;throw;}return new(c,"{}");}};var run=call.Run();await Until(()=>call.Connected);socket.Emit("{\"toolCall\":{\"functionCalls\":[{\"id\":\"cancel-me\",\"name\":\"execute_task\",\"args\":{\"task\":\"synthetic\"}}]}}");await Until(()=>entered);socket.Emit("{\"toolCallCancellation\":{\"ids\":[\"cancel-me\"]}}");await Until(()=>cancelled);call.Stop();await run;Require(!socket.Snapshot().Any(s=>s.Contains("functionResponses")),"Cancelled Gemini tool returned a late result");Console.WriteLine("PASS voice actions: Gemini cancellation stops in-flight work and suppresses stale tool output");
        }
    }
    internal static async Task<int> SavedCheck()
    {
        var path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"YoWidgets","settings.json");var settings=JsonSerializer.Deserialize<AppSettings>(await File.ReadAllTextAsync(path))!;settings.Normalize();var provider=settings.AiProvider;if(!VoiceCallOptions.Supported(provider)||settings.AiKeyProtected==null){Console.WriteLine("No supported saved voice connection.");return 2;}
        using var socket=new VoiceSocket();using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(40));var options=settings.VoiceCall;
        try{
            await socket.Connect(provider,options.ModelFor(provider),AiService.UnlockGenerationKey(settings.AiKeyProtected),stop.Token);await socket.Send(JsonSerializer.Serialize(LiveVoiceProtocol.Setup(provider,options.ModelFor(provider),options.VoiceFor(provider))),stop.Token);
            var ready=false;var bytes=0;while(!stop.IsCancellationRequested){var json=await socket.Receive(stop.Token);if(json==null)throw new InvalidOperationException("Live API closed the test session.");foreach(var update in LiveVoiceProtocol.Parse(provider,json)){
                if(update.Kind=="error")throw new InvalidOperationException(update.Text);
                if(update.Kind=="ready"&&!ready){ready=true;Console.WriteLine("LIVE "+provider+" handshake accepted · "+options.ModelFor(provider)+" · "+options.VoiceFor(provider));
                    if(provider=="Gemini")await socket.Send(JsonSerializer.Serialize(new{clientContent=new{turns=new[]{new{role="user",parts=new[]{new{text="Synthetic voice integration test. Say only: Hello."}}}},turnComplete=true}}),stop.Token);
                    else{await socket.Send(JsonSerializer.Serialize(new{type="conversation.item.create",item=new{type="message",role="user",content=new[]{new{type="input_text",text="Synthetic voice integration test. Say only: Hello."}}}}),stop.Token);await socket.Send("{\"type\":\"response.create\"}",stop.Token);}}
                if(update.Kind=="audio")bytes+=update.Pcm!.Length;if(update.Kind=="done"&&bytes>0){Console.WriteLine("LIVE speech response verified: "+bytes+" PCM bytes. No microphone, speaker or recording file used.");return 0;}
            }}return 1;
        }catch(Exception e){Console.WriteLine(e is InvalidOperationException?e.Message:"Live voice check failed: "+e.GetType().Name+". No microphone was opened.");return 1;}finally{socket.Abort();}
    }
}
