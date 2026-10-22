using System.Net;
using System.Text;
using YoWidgets;
internal static class GenerationConnectionChecks
{
    internal static async Task Run(Func<string,Func<Task>,Task> test,Action<bool> require)
    {
        await test("Saved chat connections migrate the active key and preserve other providers and generation keys",()=>{
            var settings=new AppSettings{AiProvider="Gemini",AiModel="gemini-flash-lite-latest",AiKeyProtected="protected-gemini",ImageConnection=new(){Mode="separate",Provider="Pollinations",KeyProtected="protected-media"}};settings.Normalize();require(settings.ChatConnections.Single().KeyProtected=="protected-gemini");
            settings.AiProvider="Claude";settings.AiModel="claude-sonnet-4-6";settings.AiKeyProtected="protected-claude";settings.Normalize();require(settings.ChatConnections.Count==2);require(ChatConnections.Select(settings,"Gemini")&&settings.AiKeyProtected=="protected-gemini");require(ChatConnections.Select(settings,"Claude")&&settings.AiKeyProtected=="protected-claude");require(!ChatConnections.Select(settings,"DeepSeek")&&settings.AiProvider=="Claude"&&settings.ImageConnection.KeyProtected=="protected-media");return Task.CompletedTask;
        });
        await test("Generation connections: separate media keys never reuse or replace the chat key",()=>{
            var settings=new AppSettings{AiProvider="Gemini",AiModel="gemini-flash-lite-latest",AiKeyProtected=AiService.Protect("chat-only"),ImageConnection=new(){Mode="separate",Provider="Pollinations",KeyProtected=AiService.Protect("media-only")}};
            var media=GenerationConnections.Resolve(settings,"image");require(media.Provider=="Pollinations"&&AiService.UnlockGenerationKey(media.KeyProtected)=="media-only"&&AiService.UnlockGenerationKey(settings.AiKeyProtected)=="chat-only");
            settings.AiProvider="Claude";require(GenerationConnections.Resolve(settings,"image").Provider=="Pollinations");return Task.CompletedTask;
        });
        await test("Generation connections: migration preserves legacy models and disabled categories make no network call",()=>{
            var settings=new AppSettings{AiProvider="Gemini",AiKeyProtected=AiService.Protect("chat"),AiImageModel="gemini-3.1-flash-image"};settings.Normalize();require(GenerationConnections.Resolve(settings,"image").Model==settings.AiImageModel);
            foreach(var kind in new[]{"audio","model3d"})try{GenerationConnections.Resolve(settings,kind);throw new Exception("enabled unexpectedly");}catch(InvalidOperationException e){require(e.Message.Contains("Enable"));}return Task.CompletedTask;
        });
        await test("Pollinations check: authenticated key endpoint is used; public catalogs do not prove authentication",async()=>{
            using var http=new HttpClient(new Reply((r,t)=>{require(r.RequestUri!.AbsolutePath=="/account/key"&&r.Headers.Authorization?.Parameter=="private-key"&&!r.RequestUri.Query.Contains("private-key"));return Task.FromResult(Json(HttpStatusCode.Unauthorized,new{error=new{code="UNAUTHORIZED",message="Invalid API key"}}));}));
            try{await PollinationsCatalog.Account(http,"private-key",CancellationToken.None);throw new Exception("public list authenticated");}catch(InvalidOperationException e){require(e.Message.Contains("HTTP 401")&&!e.Message.Contains("private-key"));}
        });
        await test("Pollinations check: model permissions, Quest restriction, budget and unavailable balance remain distinct",async()=>{
            using var http=new HttpClient(new Reply((r,t)=>Task.FromResult(r.RequestUri!.AbsolutePath switch{
                "/account/key"=>Json(HttpStatusCode.OK,new{valid=true,type="secret",pollenBudget=.25,questPollenOnly=true,rateLimitEnabled=true,permissions=new{models=new[]{"image"}}}),
                "/account/balance"=>Json(HttpStatusCode.Forbidden,new{error="scope"}),_=>Json(HttpStatusCode.OK,new[]{new{name="example/paid-image",title="Example",input_modalities=new[]{"text"},output_modalities=new[]{"image"},paid_only=true}})})));
            var result=await PollinationsCatalog.Check(http,new("image","Pollinations","example/paid-image","protected","alloy"),"private-key",CancellationToken.None);
            require(!result.ModelAvailable&&result.Message.Contains("Quest-only")&&result.Message.Contains("Balance unavailable")&&result.Message.Contains("0.25")&&!result.Message.Contains("0 Pollen"));
        });
        await test("Pollinations generation: its media key reaches the fixed image API and never the Gemini chat API",async()=>{
            var png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jN1sAAAAASUVORK5CYII=");var calls=0;using var http=new HttpClient(new Reply(async(r,t)=>{
                calls++;require(r.RequestUri!.Host=="gen.pollinations.ai");if(r.Method==HttpMethod.Get){require(r.Headers.Authorization==null);return Json(HttpStatusCode.OK,new[]{new{name="tongyi-mai/z-image-turbo",input_modalities=new[]{"text"},output_modalities=new[]{"image"}}});}
                require(r.Headers.Authorization?.Parameter=="media-key"&&r.RequestUri.AbsolutePath=="/v1/images/generations");var body=await r.Content!.ReadAsStringAsync(t);require(body.Contains("b64_json")&&!body.Contains("chat-only"));return Json(HttpStatusCode.OK,new{data=new[]{new{b64_json=Convert.ToBase64String(png),media_type="image/png"}}});
            }));var result=await new GeneratedMediaService(http,"Pollinations","media-key","tongyi-mai/z-image-turbo","").Generate(new("image","Blue circle","1:1"),null,null,CancellationToken.None);require(calls==2&&result.Provider=="Pollinations"&&File.Exists(result.Path));
        });
        await test("Pollinations edits: inline reference uses the native image_url object schema and preserves the source",async()=>{
            var png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jN1sAAAAASUVORK5CYII=");var source=AttachmentActions.Snapshot("reference.png",png);
            using var http=new HttpClient(new Reply(async(r,t)=>{
                if(r.Method==HttpMethod.Get)return Json(HttpStatusCode.OK,new[]{new{name="fixture/edit",input_modalities=new[]{"text","image"},output_modalities=new[]{"image"}}});
                require(r.RequestUri!.AbsolutePath=="/v1/images/edits");using var body=System.Text.Json.JsonDocument.Parse(await r.Content!.ReadAsStringAsync(t));var reference=body.RootElement.GetProperty("image")[0];require(reference.ValueKind==System.Text.Json.JsonValueKind.Object&&reference.GetProperty("image_url").GetString()=="data:image/png;base64,"+Convert.ToBase64String(png));
                return Json(HttpStatusCode.OK,new{data=new[]{new{b64_json=Convert.ToBase64String(png),media_type="image/png"}}});
            }));var edited=await new GeneratedMediaService(http,"Pollinations","media-key","fixture/edit","").Generate(new("image","Make the circle red","1:1"),null,source,CancellationToken.None);require(File.Exists(edited.Path)&&source.Bytes().SequenceEqual(png));
        });
        await test("Pollinations generation: audio and GLB are actual files; image-only 3D models reject missing reference before billing",async()=>{
            var wav=new byte[48];Encoding.ASCII.GetBytes("RIFF").CopyTo(wav,0);Encoding.ASCII.GetBytes("WAVE").CopyTo(wav,8);var glb=new byte[24];Encoding.ASCII.GetBytes("glTF").CopyTo(glb,0);
            foreach(var kind in new[]{"audio","model3d"}){var category=kind=="model3d"?"3d":"audio";using var http=new HttpClient(new Reply((r,t)=>Task.FromResult(r.Method==HttpMethod.Get?Json(HttpStatusCode.OK,new[]{new{name="fixture/model",input_modalities=new[]{"text"},output_modalities=new[]{category}}}):kind=="audio"?new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(wav)}:Json(HttpStatusCode.OK,new{data=new[]{new{b64_json=Convert.ToBase64String(glb),media_type="model/gltf-binary"}}}))));var file=await new GeneratedMediaService(http,"Pollinations","media-key","fixture/model","").Generate(new(kind,"fixture","1:1"),null,null,CancellationToken.None);require(Path.GetExtension(file.Path)==(kind=="audio"?".wav":".glb"));}
            var billed=false;using var denied=new HttpClient(new Reply((r,t)=>{if(r.Method!=HttpMethod.Get)billed=true;return Task.FromResult(Json(HttpStatusCode.OK,new[]{new{name="microsoft/trellis-2",input_modalities=new[]{"image"},output_modalities=new[]{"3d"}}}));}));
            try{await new GeneratedMediaService(denied,"Pollinations","media-key","microsoft/trellis-2","").Generate(new("model3d","cube","1:1"),null,null,CancellationToken.None);throw new Exception("missing reference allowed");}catch(InvalidOperationException e){require(!billed&&e.Message.Contains("requires an image"));}
        });
        await test("Generation connections: native Gemini chat delegates to Pollinations without disclosing either raw key",async()=>{
            var chat=0;var png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jN1sAAAAASUVORK5CYII=");
            using var http=new HttpClient(new Reply(async(r,t)=>{
                if(r.RequestUri!.Host=="generativelanguage.googleapis.com"){
                    require(r.Headers.GetValues("x-goog-api-key").Single()=="chat-secret");var body=await r.Content!.ReadAsStringAsync(t);require(!body.Contains("media-secret")&&!body.Contains("chat-secret"));
                    var text=chat++==0?AgentChecks.Event("Gemini","generate_media",new{kind="image",prompt="Blue circle",reference="",aspect_ratio="1:1"}):"data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Image created.\"}]},\"finishReason\":\"STOP\"}]}\n\n";return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(text)};
                }
                require(r.RequestUri.Host=="gen.pollinations.ai");if(r.Method==HttpMethod.Get)return Json(HttpStatusCode.OK,new[]{new{name="tongyi-mai/z-image-turbo",input_modalities=new[]{"text"},output_modalities=new[]{"image"}}});require(r.Headers.Authorization?.Parameter=="media-secret");return Json(HttpStatusCode.OK,new{data=new[]{new{b64_json=Convert.ToBase64String(png),media_type="image/png"}}});
            }));var ai=new AiService(http);ai.SelectPluginInstruction("media");var settings=new AppSettings{AiProvider="Gemini",AiModel="gemini-flash-lite-latest",AiKeyProtected=AiService.Protect("chat-secret"),ImageConnection=new(){Mode="separate",Provider="Pollinations",KeyProtected=AiService.Protect("media-secret")}};
            await ai.SendAsync("Draw a blue circle",settings);require(ai.Error==""&&ai.Messages.Last().Complete&&ai.Messages.Last().Media.Single().Result?.Provider=="Pollinations");
        });
    }
    static HttpResponseMessage Json(HttpStatusCode status,object data)=>new(status){Content=new StringContent(System.Text.Json.JsonSerializer.Serialize(data),Encoding.UTF8,"application/json")};
    sealed class Reply(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> action):HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>action(request,token);}
}
