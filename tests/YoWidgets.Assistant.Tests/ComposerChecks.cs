using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using YoWidgets;

static class ComposerChecks
{
    static string Folder(){var path=Path.GetFullPath("artifacts/composer-tests/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);return path;}
    static AgentToolCall Call(string name,object args)=>new("fixture",name,JsonSerializer.SerializeToElement(args));
    internal static async Task Run(Func<string,Func<Task>,Task> test,Action<bool> require)
    {
        await test("Computer module declares installed-app launch and URL tools without requiring a second chip",()=>{
            var ai=new AiService();ai.Plugins.Add("computer");using var runtime=new AssistantToolRuntime(ai,new AgentWorkspace(Folder()),[],"Open Telegram and inspect https://example.com/");
            foreach(var provider in AssistantProtocol.Providers){var json=runtime.Tools(provider).ToJsonString();require(json.Contains("computer_list_apps")&&json.Contains("computer_open_app")&&json.Contains("browser_open"));}return Task.CompletedTask;
        });
        await test("External action journal records actual counts, targets and readable errors",async()=>{
            var run=new AgentWorkspace(Folder());await run.ExecuteCustomAsync(Call("computer_list_windows",new{}),_=>Task.FromResult(new AgentToolResult(Call("computer_list_windows",new{}),"{\"windows\":[{\"title\":\"Fixture\"}]}")),CancellationToken.None);
            await run.ExecuteCustomAsync(Call("computer_inspect",new{window_id="123"}),_=>Task.FromResult(new AgentToolResult(Call("computer_inspect",new{}),"{\"title\":\"Telegram fixture\",\"controls\":[{},{}]}")),CancellationToken.None);
            await run.ExecuteCustomAsync(Call("computer_key",new{key="WIN"}),_=>Task.FromResult(new AgentToolResult(Call("computer_key",new{}),"{\"error\":\"Unsupported key; use the app launcher.\"}",true)),CancellationToken.None);
            require(run.Steps[0].Detail=="Found 1 open windows"&&run.Steps[1].Target=="Telegram fixture"&&run.Steps[1].Detail=="Inspected 2 controls"&&run.Steps[2].Detail=="Unsupported key; use the app launcher.");
            require(!InstalledApplications.AllowedExecutable(@"C:\Windows\System32\cmd.exe")&&!InstalledApplications.AllowedExecutable(@"C:\tmp\test.cmd")&&!InstalledApplications.AllowedExecutable("relative.exe"));
        });
        await test("Mention parser supports typed tools while excluding email addresses and completed words",()=>{
            require(MentionQuery.At("@",1) is {Start:0,Length:1,Query:""});require(MentionQuery.At("Make this @doc",14) is {Start:10,Query:"doc"});require(MentionQuery.At("hello\n@pdf",10) is {Start:6,Query:"pdf"});
            require(MentionQuery.At("a@example.com",5)==null&&MentionQuery.At("@doc ",5)==null&&MentionQuery.At("@@doc",5)==null);return Task.CompletedTask;
        });
        await test("Selected plugin instructions reach the native API and remain attached to retry",async()=>{
            var bodies=new List<string>();int attempt=0;using var http=new HttpClient(new Reply(async r=>{bodies.Add(await r.Content!.ReadAsStringAsync());return attempt++==0?new(HttpStatusCode.ServiceUnavailable){Content=new StringContent("")}:Final();}));var ai=new AiService(http);ai.SetWorkspace(Folder());ai.SelectPluginInstruction("documents");ai.SelectPluginInstruction("documents");
            var settings=new AppSettings {AiProvider="Gemini",AiModel=AssistantProtocol.DefaultModel("Gemini"),AiKeyProtected=AiService.Protect("fixture")};await ai.SendAsync("Create my report",settings);
            require(ai.DraftPluginInstructions.Count==0&&ai.Messages[0].PluginInstructions.SequenceEqual(new[]{"documents"})&&ai.Messages[0].Text=="Create my report");await ai.RetryAsync(settings);
            require(bodies.Count==2&&bodies.All(b=>b.Contains("Use the Documents tools for this request")&&b.Contains("Create my report"))&&ai.Messages.Last(m=>m.Role=="user").PluginInstructions.Contains("documents"));
            ai.SelectPluginInstruction("pdf");ai.RemovePluginInstruction("pdf");require(ai.DraftPluginInstructions.Count==0);ai.SelectPluginInstruction("not-a-plugin");require(ai.DraftPluginInstructions.Count==0);ai.NewChat();require(ai.DraftPluginInstructions.Count==0);
        });
        await test("Goal stays pinned across follow-ups and resets its objective on a new chat",async()=>{
            var bodies=new List<string>();using var http=new HttpClient(new Reply(async r=>{bodies.Add(await r.Content!.ReadAsStringAsync());return Final();}));var ai=new AiService(http);ai.SetWorkspace(Folder());ai.SetGoal(true);
            var settings=new AppSettings {AiProvider="Gemini",AiModel=AssistantProtocol.DefaultModel("Gemini"),AiKeyProtected=AiService.Protect("fixture")};await ai.SendAsync("Build the target",settings);await ai.SendAsync("Use a darker theme",settings);
            require(ai.GoalText=="Build the target"&&bodies.Count==2&&bodies.All(b=>b.Contains("user-defined goal")&&b.Contains("Build the target")));ai.NewChat();require(ai.GoalEnabled&&ai.GoalText=="");ai.SetGoal(false);require(!ai.GoalEnabled);
        });
        await test("Planning gates mutations and writes a real Markdown plan for complex work",async()=>{
            var root=Folder();var ai=new AiService();ai.SetWorkspace(root);ai.SetPlanning(true);var workspace=new AgentWorkspace(root);using var runtime=new AssistantToolRuntime(ai,workspace,[],"Implement a large multi-file game");bool blocked=false;try{runtime.GuardMutation("write_file");}catch(InvalidOperationException){blocked=true;}require(blocked);
            var plan="# Implementation plan\n\n## Scope\nBuild a small playable prototype.\n\n## Steps\n1. Inspect the existing files and agree on constraints.\n2. Implement core mechanics with clear separation.\n3. Verify input, scoring and restart behavior.\n\n## Verification\nCheck all the requested behaviors and document limits.\n";
            var result=await runtime.Execute(Call("planning_decision",new{required=true,reason="Several connected subsystems need an ordered implementation.",research_required=false,plan_markdown=plan}),CancellationToken.None);
            runtime.GuardMutation("write_file");var saved=Directory.GetFiles(root,"PLAN-*.md").Single();require(!result.Error&&File.ReadAllText(saved).Contains("Verification")&&workspace.Changes.Count==1);
        });
        await test("Planning skips the plan file for a narrow task and requires real reference reads",async()=>{
            var ai=new AiService();ai.SetWorkspace(Folder());ai.SetPlanning(true);var workspace=new AgentWorkspace(ai.WorkspaceRoot!);using var small=new AssistantToolRuntime(ai,workspace,[],"Fix one spelling mistake");await small.Execute(Call("planning_decision",new{required=false,reason="A narrow single-line correction does not need a plan.",research_required=false,plan_markdown=""}),CancellationToken.None);small.GuardMutation("edit_file");require(workspace.Changes.Count==0);
            using var reference=new AssistantToolRuntime(ai,workspace,[],"Make a game like Roblox");var call=Call("planning_decision",new{required=true,reason="A product reference must be researched before planning.",research_required=true,plan_markdown="# Plan\n\n## Steps\n1. Inspect reference requirements and user scope.\n2. Build a limited prototype with documented constraints.\n3. Verify the main flows and report what remains.\n\n## Verification\nCheck each core interaction.\n"});
            bool blocked=false;try{await reference.Execute(call,CancellationToken.None);}catch(InvalidOperationException){blocked=true;}require(blocked&&workspace.Changes.Count==0);
            reference.ReadPage=(url,offset,limit,t)=>Task.FromResult(JsonSerializer.SerializeToElement(new{url,text="Synthetic reference page"}));await reference.Execute(Call("read_web_page",new{url="https://reference.test/",offset=0,limit=1000}),CancellationToken.None);await reference.Execute(call,CancellationToken.None);require(File.ReadAllText(Directory.GetFiles(workspace.Root,"PLAN-*.md").Single()).Contains("https://reference.test/"));
        });
        await test("Optional tool schemas use each native provider format and disabled modules refuse work",async()=>{
            var ai=new AiService();ai.SetWorkspace(Folder());ai.SetPlanning(true);ai.Plugins.UnionWith(["documents","browser","computer"]);using var runtime=new AssistantToolRuntime(ai,new AgentWorkspace(ai.WorkspaceRoot!),[],"Task");
            foreach(var provider in AssistantProtocol.Providers){var definitions=runtime.Tools(provider).ToJsonString();require(new[]{"planning_decision","computer_inspect","computer_drag","computer_scroll","document_create","browser_open","browser_navigate","browser_screenshot","browser_fill"}.All(definitions.Contains));if(provider=="Claude")require(definitions.Contains("input_schema"));if(provider=="Gemini")require(!definitions.Contains("additionalProperties"));}
            var none=new AiService();none.SetWorkspace(Folder());using var disabled=new AssistantToolRuntime(none,new AgentWorkspace(none.WorkspaceRoot!),[],"Task");bool denied=false;try{await disabled.Execute(Call("document_schema",new{format="docx"}),CancellationToken.None);}catch(InvalidOperationException){denied=true;}require(denied);
        });
        await test("Generated binary snapshots support large-file undo and protect manual edits",()=>{
            var root=Folder();var workspace=new AgentWorkspace(root);var bytes=Enumerable.Repeat((byte)91,2*1024*1024).ToArray();workspace.WriteDocument("large.bin",bytes,"",CancellationToken.None);var read=workspace.ReadDocument("large.bin");workspace.WriteDocument("large.bin",[1,2,3],read.Sha256,CancellationToken.None);workspace.Finish("Completed");require(workspace.Undo()=="Changes undone."&&!File.Exists(Path.Combine(root,"large.bin")));
            File.WriteAllBytes(Path.Combine(root,"original.bin"),bytes);var next=new AgentWorkspace(root);var original=next.ReadDocument("original.bin");next.WriteDocument("original.bin",[2,3],original.Sha256,CancellationToken.None);File.WriteAllBytes(Path.Combine(root,"original.bin"),[9]);next.Finish("Completed");require(next.Undo().Contains("preserved")&&File.ReadAllBytes(Path.Combine(root,"original.bin")).SequenceEqual(new byte[]{9}));return Task.CompletedTask;
        });
        await test("Spreadsheet creation blocks network formulas but preserves literal equals text",()=>{
            var blocked="{\"sheets\":[{\"name\":\"Sheet1\",\"rows\":[[{\"formula\":\"WEBSERVICE(\\\"https://example.test\\\")\"}]]}]}";bool rejected=false;try{DocumentEngine.Create("xlsx",blocked,_=>[]);}catch(InvalidOperationException){rejected=true;}require(rejected);
            var safe=DocumentEngine.Create("xlsx","{\"sheets\":[{\"name\":\"Sheet1\",\"rows\":[[\"=literal text\"]]}]}",_=>[]);require(JsonSerializer.Serialize(DocumentEngine.Inspect("x.xlsx",safe,0,1000)).Contains("=literal text"));return Task.CompletedTask;
        });
        await test("Public browser network policy rejects loopback and private address ranges",()=>{
            foreach(var address in new[]{"127.0.0.1","10.1.2.3","192.168.1.1","172.16.1.1","169.254.169.254","::1","fc00::1","::ffff:192.168.1.2"})require(!BrowserPageReader.PublicAddress(IPAddress.Parse(address)));require(BrowserPageReader.PublicAddress(IPAddress.Parse("93.184.216.34"))&&!BrowserAssistantBridge.ValidUrl("file:///C:/secret")&&!BrowserAssistantBridge.ValidUrl("https://user:secret@example.com"));return Task.CompletedTask;
        });
        await test("Browser companion identity matches the packaged public key",()=>{
            using var manifest=JsonDocument.Parse(File.ReadAllText("integrations/assistant-browser-bridge/manifest.json"));var key=Convert.FromBase64String(manifest.RootElement.GetProperty("key").GetString()!);var hash=SHA256.HashData(key);var id=string.Concat(hash.Take(16).Select(b=>new string([(char)('a'+(b>>4)),(char)('a'+(b&15))])));require(id==BrowserAssistantBridge.ExtensionId);return Task.CompletedTask;
        });
        await test("Paired tab bridge denies unattached tabs and revokes access after navigation",async()=>{
            using var bridge=new BrowserAssistantBridge("fixture-pairing-token",0);bridge.Start();using var client=new ClientWebSocket();client.Options.Proxy=null;client.Options.SetRequestHeader("Origin",BrowserAssistantBridge.Origin);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(8));await client.ConnectAsync(new Uri($"ws://127.0.0.1:{bridge.Port}/yowidgets-assistant-v1"),timeout.Token);
            await Send(client,new{type="hello",token="fixture-pairing-token"},timeout.Token);await Until(client,"ready",timeout.Token);await Send(client,new{type="tabs",tabs=new[]{new{id="1",title="Fixture page",url="https://fixture.test/one",browser="Chrome"}}},timeout.Token);await Wait(()=>bridge.Tabs.Count==1,timeout.Token);var tab=bridge.Tabs.Single();bool denied=false;try{await bridge.RequestAsync(tab.Id,"read",new{},timeout.Token);}catch(InvalidOperationException){denied=true;}require(denied);
            bridge.Authorize(tab);var pending=bridge.RequestAsync(tab.Id,"read",new{offset=0,limit=1000},timeout.Token);var request=await Until(client,"request",timeout.Token);await Send(client,new{type="result",requestId=request.GetProperty("requestId").GetString(),result=new{text="Owned fixture content",url=tab.Url}},timeout.Token);require((await pending).GetProperty("text").GetString()=="Owned fixture content");
            await Send(client,new{type="tabs",tabs=new[]{new{id="1",title="New page",url="https://fixture.test/two",browser="Chrome"}}},timeout.Token);await Wait(()=>bridge.Tabs[0].Url.EndsWith("two"),timeout.Token);denied=false;try{await bridge.RequestAsync(tab.Id,"read",new{},timeout.Token);}catch(InvalidOperationException){denied=true;}require(denied);bridge.Stop();require(bridge.Tabs.Count==0);
        });
    }
    static async Task Send(ClientWebSocket socket,object value,CancellationToken token)=>await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(value),WebSocketMessageType.Text,true,token);
    static async Task<JsonElement> Until(ClientWebSocket socket,string type,CancellationToken token)
    {var bytes=new byte[32000];while(true){int count=0;ValueWebSocketReceiveResult result;do{result=await socket.ReceiveAsync(bytes.AsMemory(count),token);count+=result.Count;}while(!result.EndOfMessage);using var json=JsonDocument.Parse(bytes.AsMemory(0,count));if(json.RootElement.GetProperty("type").GetString()==type)return json.RootElement.Clone();}}
    static async Task Wait(Func<bool> condition,CancellationToken token) {while(!condition())await Task.Delay(15,token);}
    static HttpResponseMessage Final()=>new(HttpStatusCode.OK){Content=new StringContent("data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Done\"}]},\"finishReason\":\"STOP\"}]}\n\n")};
    sealed class Reply(Func<HttpRequestMessage,Task<HttpResponseMessage>> action):HttpMessageHandler {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>action(request);}
}
