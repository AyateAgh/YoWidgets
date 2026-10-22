using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using YoWidgets;

static class AgentChecks
{
    internal static AgentToolCall Call(string name,object args,string id="test")=>new(id,name,JsonSerializer.SerializeToElement(args));
    internal static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    static string Folder() {var root=Path.GetFullPath("artifacts/agent-tests/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);return root;}
    internal static string Event(string provider,string name,object args,string id="call_1")
    {
        if(provider=="Gemini")return "data: "+JsonSerializer.Serialize(new { candidates=new[]{new { content=new { parts=new object[]{new { functionCall=new { name,args },thoughtSignature="opaque-google-signature" }} },finishReason="STOP" }} })+"\n\n";
        if(provider=="Claude")return "data: "+JsonSerializer.Serialize(new { type="content_block_start",index=0,content_block=new { type="tool_use",id,name,input=args } })+"\n\ndata: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"tool_use\"}}\n\n";
        return "data: "+JsonSerializer.Serialize(new { choices=new[]{new { delta=new { tool_calls=new[]{new { index=0,id,type="function",function=new { name,arguments=JsonSerializer.Serialize(args) }} } },finish_reason="tool_calls" }} })+"\n\n";
    }
    static string Final(string provider)=>provider switch {
        "Gemini"=>"data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Done.\"}]},\"finishReason\":\"STOP\"}]}\n\n",
        "Claude"=>"data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"Done.\"}}\n\ndata: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"}}\n\n",
        _=>"data: {\"choices\":[{\"delta\":{\"content\":\"Done.\"},\"finish_reason\":\"stop\"}]}\n\n" };
    internal static async Task Run(Func<string,Func<Task>,Task> test,Action<bool> require)
    {
        await test("Tool timing: actual start offsets and durations settle on success, failure and cancellation",async()=>{
            var run=new AgentWorkspace(Folder());await Task.Delay(35);var call=Call("document_schema",new{format="docx"});await run.ExecuteCustomAsync(call,async t=>{await Task.Delay(45,t);return new(call,"{\"format\":\"docx\"}");},CancellationToken.None);
            var done=run.Steps.Single();var duration=done.Elapsed;require(done.StartedAfter.TotalMilliseconds>=25&&duration.TotalMilliseconds>=35&&done.EndedAt>=done.StartedAt&&done.Format=="docx");await Task.Delay(25);require(done.Elapsed==duration);
            await run.ExecuteCustomAsync(Call("browser_open",new{url="https://example.com"}),_=>throw new InvalidOperationException("Fixture failure"),CancellationToken.None);require(run.Steps.Last().Status=="Failed"&&run.Steps.Last().EndedAt!=null);
            using var stop=new CancellationTokenSource(35);try{await run.ExecuteCustomAsync(Call("computer_inspect",new{}),async t=>{await Task.Delay(2000,t);return new(call,"{}");},stop.Token);}catch(OperationCanceledException){}require(run.Steps.Last().Status=="Stopped"&&run.Steps.Last().EndedAt!=null);
            require(run.Steps.Zip(run.Steps.Skip(1)).All(p=>p.First.StartedAfter<=p.Second.StartedAfter));run.Finish("Stopped");var total=run.Elapsed;await Task.Delay(20);require(run.Elapsed==total);
        });
        await test("Agent paths reject escape, absolute paths, ADS, Git metadata and junctions",async()=>{
            var root=Folder();var run=new AgentWorkspace(root);require(run.Resolve(".")==root);
            foreach(var path in new[]{"../escape.txt","C:\\outside.txt","note.txt:stream",".git/config","sub/../../outside","NUL.txt"}) {try {run.Resolve(path);throw new Exception("Allowed "+path);}catch(InvalidOperationException){}}
            var outside=Folder();var link=Path.Combine(root,"link");
            try {Directory.CreateSymbolicLink(link,outside);try {run.Resolve("link/file.txt");throw new Exception("Followed symlink");}catch(InvalidOperationException){}}catch(Exception e) when(e is UnauthorizedAccessException or IOException) {Console.WriteLine("SKIP symbolic link creation: OS privilege unavailable; junction fixture checked separately");}
            var junctionRoot=Path.GetFullPath("artifacts/agent-link-fixture/root");if(Directory.Exists(Path.Combine(junctionRoot,"junction"))) {var scoped=new AgentWorkspace(junctionRoot);try{scoped.Resolve("junction/file.txt");throw new Exception("Followed junction");}catch(InvalidOperationException){}}
            if(File.Exists(Path.Combine(junctionRoot,"hard.txt"))) {var scoped=new AgentWorkspace(junctionRoot);var result=await scoped.ExecuteAsync(Call("read_file",new {path="hard.txt",start_line=1,line_count=20}),CancellationToken.None);require(result.Error&&result.Text.Contains("Hard-linked"));}
            await Task.CompletedTask;
        });
        await test("File tools require read hashes, keep exact snapshots and Undo creates/edits/deletes",async()=>{
            var root=Folder();await File.WriteAllTextAsync(Path.Combine(root,"note.txt"),"first\r\nsecond\r\n");var run=new AgentWorkspace(root);
            var denied=await run.ExecuteAsync(Call("write_file",new {path="note.txt",content="oops",encoding="utf8",expected_sha256=Hash("first\r\nsecond\r\n")}),CancellationToken.None);require(denied.Error);
            await run.ExecuteAsync(Call("read_file",new {path="note.txt",start_line=1,line_count=100}),CancellationToken.None);
            var changed=await run.ExecuteAsync(Call("edit_file",new {path="note.txt",old_text="second",new_text="updated",expected_sha256=Hash("first\r\nsecond\r\n")}),CancellationToken.None);require(!changed.Error);
            require(await File.ReadAllTextAsync(Path.Combine(root,"note.txt"))=="first\r\nupdated\r\n" && run.Changes.Single().Added==1&&run.Changes.Single().Removed==1);
            await run.ExecuteAsync(Call("create_directory",new {path="new/sub"}),CancellationToken.None);
            await run.ExecuteAsync(Call("write_file",new {path="new/sub/file.tsx",content="export const x = 1;\n",encoding="utf8",expected_sha256=""}),CancellationToken.None);
            await run.ExecuteAsync(Call("read_file",new {path="note.txt",start_line=1,line_count=100}),CancellationToken.None);
            await run.ExecuteAsync(Call("delete_file",new {path="note.txt",expected_sha256=Hash("first\r\nupdated\r\n")}),CancellationToken.None);
            require(!File.Exists(Path.Combine(root,"note.txt")));run.Finish("Completed");require(run.Undo()=="Changes undone.");
            require(await File.ReadAllTextAsync(Path.Combine(root,"note.txt"))=="first\r\nsecond\r\n"&&!Directory.Exists(Path.Combine(root,"new")));
        });
        await test("Manual edits defeat stale writes and undo never overwrites an external change",async()=>{
            var root=Folder();var run=new AgentWorkspace(root);await File.WriteAllTextAsync(Path.Combine(root,"a.txt"),"before");
            await run.ExecuteAsync(Call("read_file",new {path="a.txt",start_line=1,line_count=100}),CancellationToken.None);
            await File.WriteAllTextAsync(Path.Combine(root,"a.txt"),"manual");
            var stale=await run.ExecuteAsync(Call("write_file",new {path="a.txt",content="agent",encoding="utf8",expected_sha256=Hash("before")}),CancellationToken.None);require(stale.Error);
            await run.ExecuteAsync(Call("read_file",new {path="a.txt",start_line=1,line_count=100}),CancellationToken.None);
            await run.ExecuteAsync(Call("write_file",new {path="a.txt",content="agent",encoding="utf8",expected_sha256=Hash("manual")}),CancellationToken.None);
            await File.WriteAllTextAsync(Path.Combine(root,"a.txt"),"newer manual");run.Finish("Completed");require(run.Undo().Contains("preserved")&&await File.ReadAllTextAsync(Path.Combine(root,"a.txt"))=="newer manual");
        });
        await test("OpenAI tool argument fragments and reasoning are preserved",()=>{
            var round=new AgentRound("DeepSeek");round.Add("{\"choices\":[{\"delta\":{\"reasoning_content\":\"opaque reasoning\",\"tool_calls\":[{\"index\":0,\"id\":\"id1\",\"function\":{\"name\":\"read_file\",\"arguments\":\"{\\\"path\\\":\"}}]}}]}");
            round.Add("{\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":0,\"function\":{\"arguments\":\"\\\"a.txt\\\",\\\"start_line\\\":1,\\\"line_count\\\":20}\"}}]},\"finish_reason\":\"tool_calls\"}]}");
            require(round.ToolCalls().Single().Arguments.GetProperty("path").GetString()=="a.txt"&&round.Message()["reasoning_content"]!.GetValue<string>()=="opaque reasoning");return Task.CompletedTask;
        });
        await test("Claude tool fragments and opaque signatures survive continuation",()=>{
            var round=new AgentRound("Claude");foreach(var data in new[]{"{\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"thinking\",\"thinking\":\"\",\"signature\":\"\"}}","{\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"signature_delta\",\"signature\":\"opaque-claude\"}}","{\"type\":\"content_block_start\",\"index\":1,\"content_block\":{\"type\":\"tool_use\",\"id\":\"c1\",\"name\":\"list_directory\",\"input\":{}}}","{\"type\":\"content_block_delta\",\"index\":1,\"delta\":{\"type\":\"input_json_delta\",\"partial_json\":\"{\\\"path\\\":\\\".\\\"}\"}}"})round.Add(data);
            require(round.ToolCalls().Single().Name=="list_directory"&&round.Message().ToJsonString().Contains("opaque-claude"));return Task.CompletedTask;
        });
        foreach(var provider in AssistantProtocol.Providers) await test(provider+" native agent loop reads, edits, creates and reports actual files",async()=>{
            var root=Folder();await File.WriteAllTextAsync(Path.Combine(root,"note.txt"),"old\n");int number=0;var bodies=new List<string>();
            using var http=new HttpClient(new AgentHandler(async(r,t)=>{
                var body=await r.Content!.ReadAsStringAsync(t);bodies.Add(body);string data=number++ switch {
                    0=>Event(provider,"read_file",new {path="note.txt",start_line=1,line_count=100}),
                    1=>Event(provider,"edit_file",new {path="note.txt",old_text="old",new_text="new",expected_sha256=Hash("old\n")}),
                    2=>Event(provider,"write_file",new {path="created.ts",content="export const created = true;\n",encoding="utf8",expected_sha256=""}),_=>Final(provider)};
                return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(data,Encoding.UTF8,"text/event-stream")};}));
            var ai=new AiService(http);ai.SetWorkspace(root);await ai.SendAsync("Edit the note and create a file",new AppSettings {AiProvider=provider,AiModel=AssistantProtocol.DefaultModel(provider),AiKeyProtected=AiService.Protect("fixture")});
            require(ai.Error==""&&ai.Messages.Last().Complete&&await File.ReadAllTextAsync(Path.Combine(root,"note.txt"))=="new\n"&&File.Exists(Path.Combine(root,"created.ts")));
            var run=ai.Messages.Last().Agent!;require(run.Steps.Count==3&&run.Changes.Count==2&&run.Steps.All(s=>s.Status=="Done")&&bodies[1].Contains(Hash("old\n")));
            if(provider=="Gemini")require(bodies[1].Contains("opaque-google-signature")&&bodies[1].Contains("functionResponse"));
            if(provider=="Claude")require(bodies[1].Contains("tool_result")&&bodies[1].Contains("tool_use"));
            require(run.Undo()=="Changes undone."&&!File.Exists(Path.Combine(root,"created.ts"))&&await File.ReadAllTextAsync(Path.Combine(root,"note.txt"))=="old\n");
        });
        await test("Cancellation prevents late file-tool execution",async()=>{
            var root=Folder();var pending=new TaskCompletionSource<HttpResponseMessage>();using var http=new HttpClient(new AgentHandler((r,t)=>pending.Task));var ai=new AiService(http);ai.SetWorkspace(root);
            var work=ai.SendAsync("create",new AppSettings { AiProvider="Gemini",AiModel=AssistantProtocol.DefaultModel("Gemini"),AiKeyProtected=AiService.Protect("fixture") });ai.Cancel();pending.SetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=new StringContent(Event("Gemini","write_file",new {path="late.txt",content="late",encoding="utf8",expected_sha256=""})) });await work;require(!File.Exists(Path.Combine(root,"late.txt"))&&!ai.Busy);
        });
        await test("Truncated tool calls cannot mutate files",async()=>{
            var root=Folder();using var http=new HttpClient(new AgentHandler((r,t)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(Event("Gemini","write_file",new {path="partial.txt",content="no",encoding="utf8",expected_sha256=""}).Replace("STOP","MAX_TOKENS"))})));
            var ai=new AiService(http);ai.SetWorkspace(root);await ai.SendAsync("write",new AppSettings { AiProvider="Gemini",AiModel=AssistantProtocol.DefaultModel("Gemini"),AiKeyProtected=AiService.Protect("fixture") });require(!File.Exists(Path.Combine(root,"partial.txt"))&&ai.Error.Contains("No action"));
        });
        await test("Agent search and invalid arguments remain bounded and visible",async()=>{
            var root=Folder();Directory.CreateDirectory(Path.Combine(root,".git"));await File.WriteAllTextAsync(Path.Combine(root,".git","private.txt"),"needle secret");await File.WriteAllTextAsync(Path.Combine(root,"source.ts"),"first\nneedle visible\n");
            var run=new AgentWorkspace(root);var result=await run.ExecuteAsync(Call("search_files",new {path=".",query="needle"}),CancellationToken.None);
            require(!result.Error&&result.Text.Contains("source.ts")&&!result.Text.Contains("secret"));
            var invalid=await run.ExecuteAsync(Call("delete_file",new {path=42,expected_sha256=""}),CancellationToken.None);require(invalid.Error&&run.Steps.Last().Status=="Failed"&&File.Exists(Path.Combine(root,"source.ts")));
        });
        await test("Native image tool results preserve hashes, tool IDs and supported image blocks",()=>{
            var file=new AssistantAttachment("pixel.png","image/png","fixture-base64",null,3);
            foreach(var provider in AssistantProtocol.Providers) {
                var round=new AgentRound(provider);foreach(var data in Event(provider,"read_file",new {path="pixel.png",start_line=1,line_count=1}).Split("\n\n",StringSplitOptions.RemoveEmptyEntries))round.Add(data[6..]);
                var call=round.ToolCalls().Single();var body=new JsonObject { [provider=="Gemini"?"contents":"messages"]=new JsonArray() };
                AgentProtocol.AppendRound(body,provider,round,[new(call,"{\"sha256\":\"fixture-hash\"}",false,file)]);var json=body.ToJsonString();
                require(json.Contains(provider=="DeepSeek"?"cannot pass image":"fixture-base64"));if(provider!="DeepSeek")require(json.Contains("fixture-hash"));if(provider=="Claude")require(json.Contains("tool_use_id"));
            }return Task.CompletedTask;
        });
        await test("Agent status reflects actual phases and file paths",async()=>{
            var root=Folder();await File.WriteAllTextAsync(Path.Combine(root,"note.txt"),"old\n");int round=0;var phases=new HashSet<AssistantPhase>();var modes=new HashSet<OrbState>();bool absolute=false;
            using var client=new HttpClient(new AgentHandler((r,t)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent(round++ switch {
                0=>Event("Gemini","read_file",new {path="note.txt",start_line=1,line_count=20}),
                1=>Event("Gemini","edit_file",new {path="note.txt",old_text="old",new_text="new",expected_sha256=Hash("old\n")}),_=>Final("Gemini")})})));
            var ai=new AiService(client);ai.SetWorkspace(root);ai.Changed+=()=>{phases.Add(ai.Phase);var activity=AssistantActivity.From(ai,"Custom process");modes.Add(activity.Orb);absolute|=activity.FullPath==Path.Combine(root,"note.txt");};
            await ai.SendAsync("update",new AppSettings {AiProvider="Gemini",AiModel=AssistantProtocol.DefaultModel("Gemini"),AiKeyProtected=AiService.Protect("fixture")});
            require(phases.IsSupersetOf(new[]{AssistantPhase.Thinking,AssistantPhase.ToolCall,AssistantPhase.ToolResult,AssistantPhase.Streaming,AssistantPhase.Done})&&modes.Contains(OrbState.Reading)&&modes.Contains(OrbState.Editing)&&absolute);
        });
    }
    sealed class AgentHandler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> action):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>action(request,token);}
}
