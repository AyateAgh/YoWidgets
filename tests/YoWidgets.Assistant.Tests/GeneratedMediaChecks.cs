using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using YoWidgets;
using YoWidgets.Core;

internal static class GeneratedMediaChecks
{
    internal static async Task Run(Func<string,Func<Task>,Task> test,Action<bool> require)
    {
        var png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jN1sAAAAASUVORK5CYII=");
        await test("Media regression: Gemini output format stays provider-selected and JPEG is saved as JPEG",async()=>{
            using var bitmap=new System.Drawing.Bitmap(2,2);using var memory=new MemoryStream();bitmap.Save(memory,System.Drawing.Imaging.ImageFormat.Jpeg);var jpeg=memory.ToArray();
            using var http=new HttpClient(new MediaHandler(async(r,t)=>{var body=JsonNode.Parse(await r.Content!.ReadAsStringAsync(t))!;require(body["response_format"]!["mime_type"]==null);return Json(new{output_image=new{mime_type="image/jpeg",data=Convert.ToBase64String(jpeg)}});}));
            var media=await new GeneratedMediaService(http,"Gemini","secret","","").Generate(new("image","test","1:1"),null,null,CancellationToken.None);
            require(media.MimeType=="image/jpeg"&&Path.GetExtension(media.Path)==".jpg"&&File.ReadAllBytes(media.Path).SequenceEqual(jpeg));
        });
        await test("Media regression: a zero free-tier quota is distinct from a temporary rate limit",async()=>{
            using var http=new HttpClient(new MediaHandler((r,t)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests){Content=new StringContent("{\"error\":{\"message\":\"Rate limit exceeded for model gemini-nano-banana-2.1 (limit: 0 requests per day on Free Tier). Please upgrade your tier at https://ai.dev/rate-limit.\",\"code\":\"too_many_requests\"}}",Encoding.UTF8,"application/json")})));
            try{await new GeneratedMediaService(http,"Gemini","secret","","").Generate(new("image","test","1:1"),null,null,CancellationToken.None);throw new Exception("accepted");}catch(InvalidOperationException e){require(e.Message.Contains("HTTP 429")&&e.Message.Contains("0 requests")&&e.Message.Contains("Free Tier")&&e.Message.Contains("gemini-nano-banana-2.1")&&!e.Message.Contains("try again later",StringComparison.OrdinalIgnoreCase));}
        });
        await test("Media regression: rejected request shows sanitized provider reason without a guessed billing diagnosis",async()=>{
            using var http=new HttpClient(new MediaHandler((r,t)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest){Content=new StringContent("{\"error\":{\"message\":\"Unsupported response_format.mime_type image/png. secret-key\",\"code\":\"invalid_request\"}}",Encoding.UTF8,"application/json")})));
            try{await new GeneratedMediaService(http,"Gemini","secret-key","","").Generate(new("image","test","1:1"),null,null,CancellationToken.None);throw new Exception("accepted");}catch(InvalidOperationException e){require(e.Message.Contains("HTTP 400")&&e.Message.Contains("response_format.mime_type")&&e.Message.Contains("invalid_request")&&!e.Message.Contains("secret-key")&&!e.Message.Contains("billing",StringComparison.OrdinalIgnoreCase));}
        });
        await test("Media regression: a zero token quota reports missing access without inventing a daily request limit",async()=>{
            using var http=new HttpClient(new MediaHandler((r,t)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests){Content=new StringContent("{\"error\":{\"message\":\"Rate limit exceeded (limit: 0 input tokens per minute on Free Tier).\",\"code\":\"too_many_requests\"}}",Encoding.UTF8,"application/json")})));
            try{await new GeneratedMediaService(http,"Gemini","secret","","").Generate(new("image","test","1:1"),null,null,CancellationToken.None);throw new Exception("accepted");}catch(InvalidOperationException e){require(e.Message.Contains("Enable API billing")&&e.Message.Contains("0 input tokens per minute")&&!e.Message.Contains("0 requests/day"));}
        });
        await test("Media: Gemini returns actual bytes and ignores thought content",async()=>{
            using var http=new HttpClient(new MediaHandler(async(req,t)=>{
                require(req.RequestUri!.AbsoluteUri=="https://generativelanguage.googleapis.com/v1beta/interactions");
                var body=JsonNode.Parse(await req.Content!.ReadAsStringAsync(t))!;
                require(body["response_format"]!["type"]!.GetValue<string>()=="image");
                return Json(new {id="test-interaction",steps=new object[]{new{type="thought",content=new[]{new{type="text",text="private"}}},new{type="model_output",content=new[]{new{type="image",mime_type="image/png",data=Convert.ToBase64String(png)}}}}});
            }));
            var job=new GeneratedMediaJob("image","test","1:1");
            var service=new GeneratedMediaService(http,"Gemini","secret","gemini-nano-banana-2.1","");
            var file=await service.Generate(job,null,null,CancellationToken.None);
            require(File.ReadAllBytes(file.Path).SequenceEqual(png)&&file.InteractionId=="test-interaction"&&job.Percent==null);
            require(!file.Prompt.Contains("private"));
        });
        await test("Media: edits use exact reference bytes across models",async()=>{
            var path=Path.Combine(App.OwnedDirectory("Assistant"),"reference.png");File.WriteAllBytes(path,png);
            var reference=new GeneratedMedia("media:test",path,"image/png","Gemini","other-model","original","old-id",null);
            using var http=new HttpClient(new MediaHandler(async(req,t)=>{
                var body=JsonNode.Parse(await req.Content!.ReadAsStringAsync(t))!;
                require(body["previous_interaction_id"]==null&&body["input"]![1]!["data"]!.GetValue<string>()==Convert.ToBase64String(png));
                return Json(new{output_image=new{mime_type="image/png",data=Convert.ToBase64String(png)}});
            }));
            var result=await new GeneratedMediaService(http,"Gemini","secret","gemini-nano-banana-2.1","").Generate(new("image","edit","1:1"),reference,null,CancellationToken.None);
            require(result.ParentId==reference.Id&&File.Exists(reference.Path)&&result.Path!=reference.Path);
        });
        await test("Media: unsupported video fails before transport",async()=>{
            using var http=new HttpClient(new MediaHandler((r,t)=>throw new Exception("unexpected transport")));
            try{await new GeneratedMediaService(http,"Claude","secret","","").Generate(new("video","test","16:9"),null,null,CancellationToken.None);throw new Exception("accepted");}catch(InvalidOperationException e){require(e.Message.Contains("not configured"));}
        });
        await test("Media: foreign download URLs never receive the API key",async()=>{
            var calls=0;using var http=new HttpClient(new MediaHandler((r,t)=>{calls++;return Task.FromResult(Json(new{output_video=new{mime_type="video/mp4",uri="https://example.com/private"}}));}));
            try{await new GeneratedMediaService(http,"Gemini","secret","","").Generate(new("video","test","16:9"),null,null,CancellationToken.None);throw new Exception("accepted");}catch(InvalidOperationException){require(calls==1);}
        });
        await test("Media: cancellation never publishes a result",async()=>{
            using var stop=new CancellationTokenSource();using var http=new HttpClient(new MediaHandler((r,t)=>{stop.Cancel();return Task.FromResult(Json(new{output_image=new{mime_type="image/png",data=Convert.ToBase64String(png)}}));}));
            try{await new GeneratedMediaService(http,"Gemini","secret","","").Generate(new("image","test","1:1"),null,null,stop.Token);throw new Exception("accepted");}catch(OperationCanceledException){}
        });
        await test("Media: video download progress counts real bytes and storage receives no API key",async()=>{
            var mp4=new byte[200000];Encoding.ASCII.GetBytes("ftyp").CopyTo(mp4,4);var count=0;var percentages=new List<int?>();var job=new GeneratedMediaJob("video","test","16:9");job.Changed+=()=>percentages.Add(job.Percent);
            using var http=new HttpClient(new MediaHandler((r,t)=>{
                count++;if(count==1)return Task.FromResult(Json(new{output_video=new{mime_type="video/mp4",uri="files/fixture"}}));
                if(count==2)return Task.FromResult(Json(new{state="ACTIVE"}));
                if(count==3){require(r.Headers.Contains("x-goog-api-key"));return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Found){Headers={Location=new Uri("https://storage.googleapis.com/fixture/clip.mp4")}});}
                require(r.RequestUri!.Host=="storage.googleapis.com"&&!r.Headers.Contains("x-goog-api-key")&&r.Headers.Authorization==null);return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(mp4)});
            }));
            var media=await new GeneratedMediaService(http,"Gemini","secret","","").Generate(job,null,null,CancellationToken.None);
            require(count==4&&media.MimeType=="video/mp4"&&percentages.Any(p=>p is >0 and <100)&&percentages.Contains(100)&&job.Percent==null);
        });
        await test("Media: OpenAI edits send multipart references to the real image edits endpoint",async()=>{
            using var http=new HttpClient(new MediaHandler(async(r,t)=>{require(r.RequestUri!.AbsolutePath=="/v1/images/edits"&&r.Content is MultipartFormDataContent&&r.Headers.Authorization?.Scheme=="Bearer");var body=await r.Content!.ReadAsStringAsync(t);require(body.Contains("image[]")&&body.Contains("reference.png"));return Json(new{data=new[]{new{b64_json=Convert.ToBase64String(png)}}});}));
            var image=await new GeneratedMediaService(http,"OpenAI","secret","gpt-image-1.5","").Generate(new("image","edit","1:1"),null,new("input.png","image/png",Convert.ToBase64String(png),null,png.Length),CancellationToken.None);require(image.Provider=="OpenAI");
        });
        await test("Media: OpenRouter generation sends selected model and exact reference data",async()=>{
            using var http=new HttpClient(new MediaHandler(async(r,t)=>{var body=JsonNode.Parse(await r.Content!.ReadAsStringAsync(t))!;require(r.RequestUri!.AbsoluteUri=="https://openrouter.ai/api/v1/images"&&body["input_references"]![0]!["image_url"]!["url"]!.GetValue<string>()=="data:image/png;base64,"+Convert.ToBase64String(png));return Json(new{data=new[]{new{b64_json=Convert.ToBase64String(png),media_type="image/png"}}});}));
            var image=await new GeneratedMediaService(http,"OpenRouter","secret","google/gemini-3.1-flash-image","").Generate(new("image","edit","1:1"),null,new("input.png","image/png",Convert.ToBase64String(png),null,png.Length),CancellationToken.None);require(image.Provider=="OpenRouter");
        });
        await test("Media: native chat tool loop displays images and preserves references across chat model changes",async()=>{
            var rounds=0;var outputs=0;var reference="";
            using var http=new HttpClient(new MediaHandler(async(r,t)=>{
                var body=await r.Content!.ReadAsStringAsync(t);
                if(r.RequestUri!.AbsolutePath.EndsWith("/interactions")){outputs++;return Json(new{id="interaction-"+outputs,output_image=new{mime_type="image/png",data=Convert.ToBase64String(png)}});}
                if(rounds>=2)require(body.Contains(reference));
                var first=rounds++%2==0;
                var data=first?new{candidates=new[]{new{content=new{parts=new object[]{new{functionCall=new{name="generate_media",args=new{kind="image",prompt=outputs==0?"Blue circle":"Green circle",reference,aspect_ratio="1:1"}}}}},finishReason="STOP"}}}:(object)new{candidates=new[]{new{content=new{parts=new[]{new{text="Created the actual image."}}},finishReason="STOP"}}};
                return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("data: "+System.Text.Json.JsonSerializer.Serialize(data)+"\n\n")};
            }));
            var ai=new AiService(http);ai.SelectPluginInstruction("media");var settings=new AppSettings{AiProvider="Gemini",AiModel="gemini-flash-latest",AiKeyProtected=AiService.Protect("fixture")};
            await ai.SendAsync("Draw a still image of a blue circle",settings);var first=ai.Messages.Last().Media.Single().Result!;require(first!=null&&ai.Messages.Last().Complete&&ai.Error=="");reference=first!.Id;
            settings.AiModel="gemini-2.5-flash";ai.ConfigurationChanged(settings);await ai.SendAsync("Change its circle to green",settings);var second=ai.Messages.Last().Media.Single().Result;
            require(second?.ParentId==first.Id&&outputs==2&&ai.Messages.Count==4&&File.Exists(first.Path)&&ai.Error=="");
        });
        await test("Media: specialized image model routes an explicit Russian video request to the video backend",async()=>{
            var bytes=new byte[24];Encoding.ASCII.GetBytes("ftyp").CopyTo(bytes,4);using var http=new HttpClient(new MediaHandler(async(r,t)=>{var body=JsonNode.Parse(await r.Content!.ReadAsStringAsync(t))!;require(body["model"]!.GetValue<string>()=="gemini-omni-1.1-flash"&&body["response_format"]!["aspect_ratio"]!.GetValue<string>()=="16:9");return Json(new{output_video=new{mime_type="video/mp4",data=Convert.ToBase64String(bytes)}});}));
            var ai=new AiService(http);await ai.SendAsync("Сгенерируй видео с синим кругом",new AppSettings{AiProvider="Gemini",AiModel="gemini-nano-banana-2.1",AiKeyProtected=AiService.Protect("fixture")});require(ai.Error==""&&ai.Messages.Last().Media.Single().Kind=="video"&&ai.Messages.Last().Complete);
        });
        await test("Media: selecting a video model does not generate unsolicited motion",async()=>{
            using var http=new HttpClient(new MediaHandler((r,t)=>throw new Exception("unsolicited generation")));var ai=new AiService(http);await ai.SendAsync("Hello",new AppSettings{AiProvider="Gemini",AiModel="gemini-omni-1.1-flash",AiKeyProtected=AiService.Protect("fixture")});require(ai.Messages.Last().Media.Count==0&&ai.Error.Contains("Explicitly request"));
        });
    }
    static HttpResponseMessage Json(object data)=>new(HttpStatusCode.OK){Content=new StringContent(System.Text.Json.JsonSerializer.Serialize(data),Encoding.UTF8,"application/json")};
    sealed class MediaHandler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>send(request,token);}
}
