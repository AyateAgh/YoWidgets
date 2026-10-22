using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace YoWidgets;

internal sealed record GeneratedMedia(string Id,string Path,string MimeType,string Provider,string Model,string Prompt,string? InteractionId,string? ParentId)
{
    internal string? ConnectionId {get;init;}
    internal string Label=>MimeType.StartsWith("audio/")?"AI-generated audio":MimeType is "model/gltf-binary" or "application/ply"?"3D model":MimeType.StartsWith("video/")?"Video":MimeType=="image/gif"?"Animation":"Image";
    internal AssistantAttachment Attachment()=>AttachmentActions.Snapshot(System.IO.Path.GetFileName(Path),File.ReadAllBytes(Path));
}
internal sealed class GeneratedMediaJob(string kind,string prompt,string aspect)
{
    internal string Kind {get;}=kind;
    internal string Prompt {get;}=prompt;
    internal string Aspect {get;}=aspect;
    internal string Stage {get;private set;}="Preparing request…";
    internal int? Percent {get;private set;}
    internal GeneratedMedia? Result {get;set;}
    internal bool Finished {get;set;}
    internal string Error {get;set;}="";
    internal event Action? Changed;
    internal void Update(string stage,int? percent=null){if(Stage==stage&&Percent==percent)return;Stage=stage;Percent=percent;Changed?.Invoke();}
}

// Fixed provider endpoints. Generated output is an immutable owned artifact, never an arbitrary file write.
internal sealed class GeneratedMediaService(HttpClient http,string provider,string key,string imageModel,string videoModel,string voice="alloy")
{
    const int Limit=128*1024*1024;
    internal static bool Available(string provider)=>provider is "Gemini" or "OpenAI" or "OpenRouter" or "Pollinations";
    internal static string DefaultImage(string provider)=>provider switch {"Gemini"=>"gemini-nano-banana-2.1","OpenAI"=>"gpt-image-2.5-flare","OpenRouter"=>"google/gemini-3.1-flash-image",_=>""};
    internal static string DefaultVideo(string provider)=>provider=="Gemini"?"gemini-omni-1.1-flash":"";
    internal async Task<GeneratedMedia> Generate(GeneratedMediaJob job,GeneratedMedia? reference,AssistantAttachment? input,CancellationToken token)
    {
        if(job.Kind is not ("image" or "video" or "animation" or "model3d" or "audio"))throw new InvalidOperationException("Choose image, video, animation, model3d or audio.");
        if(job.Prompt.Length is <1 or >16000)throw new InvalidOperationException("Media prompts must contain 1–16000 characters.");
        if(!new[]{"1:1","3:4","4:3","9:16","16:9"}.Contains(job.Aspect))throw new InvalidOperationException("Use 1:1, 3:4, 4:3, 9:16 or 16:9.");
        var video=job.Kind is "video" or "animation";var model=(video?videoModel:imageModel).Trim();if(model.Length==0)model=GenerationConnections.DefaultModel(job.Kind,provider);
        if(!Available(provider)||video&&provider is not ("Gemini" or "Pollinations")||job.Kind is "audio" or "model3d"&&provider!="Pollinations")throw new InvalidOperationException("Media generation is not configured for this service/category. Choose a compatible generation connection in Settings.");
        if(!Regex.IsMatch(model,@"^[A-Za-z0-9][A-Za-z0-9._:/-]{0,159}$"))throw new InvalidOperationException("Enter a valid media model ID in Settings.");
        byte[]? source=null;string? sourceMime=null;
        if(reference!=null){var sourcePath=reference.Path;sourceMime=reference.MimeType;if(sourceMime=="image/gif"&&video&&File.Exists(Path.ChangeExtension(sourcePath,".mp4"))){sourcePath=Path.ChangeExtension(sourcePath,".mp4");sourceMime="video/mp4";}source=await File.ReadAllBytesAsync(sourcePath,token);}
        else if(input!=null){source=input.Bytes();sourceMime=input.MimeType;}
        if(source!=null&&(source.Length>24*1024*1024||sourceMime is not ("image/png" or "image/jpeg" or "image/webp" or "video/mp4")))throw new InvalidOperationException("This editor accepts PNG/JPEG/WebP images or MP4 video up to 24 MB. Download the original to edit larger files externally.");
        if(!video&&sourceMime=="video/mp4")throw new InvalidOperationException("Select video generation to edit a video.");
        job.Update("Generating "+(video?"video":job.Kind=="model3d"?"3D model":job.Kind)+" · waiting for the service…");
        string? interaction=null;byte[] bytes;string mime;
        if(provider=="Pollinations") {
            var catalog=await PollinationsCatalog.Models(http,job.Kind,token);var selected=catalog.FirstOrDefault(m=>m.Id==model||m.Aliases.Contains(model))??throw new InvalidOperationException("This Pollinations model is not in the selected category. Load models with Check key in Settings.");
            if(source!=null&&!selected.Inputs.Contains(sourceMime!.StartsWith("video/")?"video":"image"))throw new InvalidOperationException("The selected Pollinations model cannot use this reference. Choose an editing-capable model in Settings; no automatic paid-model switch was made.");
            if(source==null&&!selected.Inputs.Contains("text"))throw new InvalidOperationException("This model requires an image reference and does not use text prompts. Attach an image or choose a text-capable model.");
            var body=new JsonObject{["model"]=model,["prompt"]=job.Prompt,["response_format"]="b64_json",["n"]=1};
            body["size"]=job.Aspect switch{"3:4"=>"768x1024","4:3"=>"1024x768","9:16"=>"576x1024","16:9"=>"1024x576",_=>"1024x1024"};
            if(video)body["duration"]=3;
            if(source!=null){var data="data:"+sourceMime+";base64,"+Convert.ToBase64String(source);body["image"]=job.Kind=="image"?new JsonArray(new JsonObject{["image_url"]=data}):new JsonArray(data);}
            var endpoint=job.Kind=="audio"?"/v1/audio/speech":source!=null&&job.Kind=="image"?"/v1/images/edits":"/v1/images/generations";
            if(job.Kind=="audio")body=new JsonObject{["model"]=model,["input"]=job.Prompt,["voice"]=voice};
            // These fixed native endpoints accept inline references. No public input upload is created by this host.
            using var request=Request(HttpMethod.Post,PollinationsCatalog.Origin+endpoint,body);
            using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);if(!response.IsSuccessStatusCode)throw await ApiError(response,model,token);
            var contentType=response.Content.Headers.ContentType?.MediaType??"";
            if(contentType.Contains("json",StringComparison.OrdinalIgnoreCase)) {
                using var doc=JsonDocument.Parse(await ReadBounded(response,Limit*2,token));
                if(!doc.RootElement.TryGetProperty("data",out var items)||items.ValueKind!=JsonValueKind.Array||items.GetArrayLength()==0)throw new InvalidOperationException("Pollinations returned no generated media.");
                var output=items[0];var data=String(output,"b64_json");if(data.Length==0)throw new InvalidOperationException("Pollinations did not return inline media. Choose a model supporting the native generation endpoint.");bytes=Decode(data);mime=String(output,"media_type");if(mime.Length==0)mime=Detect(bytes);
            }else {bytes=await ReadBounded(response,Limit,token,(done,total)=>job.Update("Downloading generated "+job.Kind+"…",total>0?(int)Math.Min(100,done*100/total):null));mime=Detect(bytes);}
        }else if(provider=="Gemini") {
            var contents=new JsonArray(new JsonObject{["type"]="text",["text"]=job.Prompt});
            var body=new JsonObject{["model"]=model,["input"]=contents,["response_format"]=new JsonObject{["type"]=video?"video":"image",["aspect_ratio"]=job.Aspect}};
            if(video)body["response_format"]!["delivery"]="uri";
            var continuity=reference is {InteractionId:not null,Provider:"Gemini"}&&reference.Model==model&&reference.ConnectionId==ConnectionId();
            if(continuity)body["previous_interaction_id"]=reference!.InteractionId;
            else if(source!=null)contents.Add(new JsonObject{["type"]=sourceMime!.StartsWith("video/")?"video":"image",["mime_type"]=sourceMime,["data"]=Convert.ToBase64String(source)});
            var classic=!video&&!model.Contains("nano-banana",StringComparison.Ordinal)&&model.Contains("image",StringComparison.Ordinal);
            string url="https://generativelanguage.googleapis.com/v1beta/interactions";
            if(classic) {
                var parts=new JsonArray(new JsonObject{["text"]=job.Prompt});if(source!=null)parts.Add(new JsonObject{["inlineData"]=new JsonObject{["mimeType"]=sourceMime,["data"]=Convert.ToBase64String(source)}});
                body=new JsonObject{["contents"]=new JsonArray(new JsonObject{["role"]="user",["parts"]=parts}),["generationConfig"]=new JsonObject{["responseModalities"]=new JsonArray("TEXT","IMAGE"),["imageConfig"]=new JsonObject{["aspectRatio"]=job.Aspect}}};
                url="https://generativelanguage.googleapis.com/v1beta/models/"+Uri.EscapeDataString(model)+":generateContent";
            }
            using var request=Request(HttpMethod.Post,url,body);using var doc=await Json(request,token,model);var root=doc.RootElement;
            interaction=root.TryGetProperty("id",out var id)?id.GetString():null;
            var output=FindOutput(root,video,classic);mime=String(output,"mime_type",String(output,"mimeType",video?"video/mp4":"image/png"));
            var data=String(output,"data");
            if(data.Length>0)bytes=Decode(data);
            else {var uri=String(output,"uri");if(uri.Length==0)throw new InvalidOperationException("The model returned no media. It may lack generation access or have declined this request.");bytes=await DownloadGoogle(uri,job,token);}
        }else {
            if(sourceMime=="video/mp4")throw new InvalidOperationException("This service cannot edit video through its image API.");
            var openai=provider=="OpenAI";
            var body=new JsonObject{["model"]=model,["prompt"]=job.Prompt};
            if(!openai){body["aspect_ratio"]=job.Aspect;if(source!=null)body["input_references"]=new JsonArray(new JsonObject{["type"]="image_url",["image_url"]=new JsonObject{["url"]="data:"+sourceMime+";base64,"+Convert.ToBase64String(source)}});}
            else {body["n"]=1;body["size"]=job.Aspect=="1:1"?"1024x1024":job.Aspect is "3:4" or "9:16"?"1024x1536":"1536x1024";}
            using var request=Request(HttpMethod.Post,openai?"https://api.openai.com/v1/images/"+(source!=null?"edits":"generations"):"https://openrouter.ai/api/v1/images",body);
            if(openai&&source!=null) {
                var form=new MultipartFormDataContent();form.Add(new StringContent(model),"model");form.Add(new StringContent(job.Prompt),"prompt");form.Add(new StringContent(body["size"]!.GetValue<string>()),"size");
                var image=new ByteArrayContent(source);image.Headers.ContentType=new MediaTypeHeaderValue(sourceMime!);form.Add(image,"image[]","reference"+(sourceMime=="image/jpeg"?".jpg":sourceMime=="image/webp"?".webp":".png"));request.Content!.Dispose();request.Content=form;
            }
            using var doc=await Json(request,token,model);var first=doc.RootElement.GetProperty("data").EnumerateArray().FirstOrDefault();var data=String(first,"b64_json");if(data.Length==0)throw new InvalidOperationException("The service returned no inline image. Choose a model that supports image generation.");
            bytes=Decode(data);mime=String(first,"media_type",String(first,"output_format")=="jpeg"?"image/jpeg":"image/png");
        }
        token.ThrowIfCancellationRequested();job.Update("Validating generated media…");
        mime=Validate(bytes,mime);
        if(job.Kind=="image"&&!mime.StartsWith("image/")||video&&mime!="video/mp4"||job.Kind=="audio"&&!mime.StartsWith("audio/")||job.Kind=="model3d"&&mime is not ("model/gltf-binary" or "application/ply"))throw new InvalidOperationException("The service returned a different media category than requested.");
        var extension=mime switch {"image/png"=>".png","image/jpeg"=>".jpg","image/webp"=>".webp","image/gif"=>".gif","video/mp4"=>".mp4","audio/mpeg"=>".mp3","audio/wav"=>".wav","audio/flac"=>".flac","model/gltf-binary"=>".glb","application/ply"=>".ply",_=>throw new InvalidOperationException("Unsupported generated media format.")};
        var directory=Path.Combine(App.OwnedDirectory("Assistant"),"Media");Directory.CreateDirectory(directory);var name=Guid.NewGuid().ToString("N");var path=Path.Combine(directory,name+extension);var temporary=path+".partial";
        try {await File.WriteAllBytesAsync(temporary,bytes,token);token.ThrowIfCancellationRequested();File.Move(temporary,path);}finally {if(File.Exists(temporary))File.Delete(temporary);}
        if(job.Kind=="animation") {
            try{job.Update("Encoding animated GIF…");path=await GeneratedAnimation.FromVideo(path,token);mime="image/gif";}
            catch {File.Delete(path);throw;}
        }
        if(token.IsCancellationRequested){File.Delete(path);token.ThrowIfCancellationRequested();}
        return new GeneratedMedia("media:"+name,path,mime,provider,model,job.Prompt,job.Kind=="animation"?null:interaction,reference?.Id){ConnectionId=ConnectionId()};
    }
    string ConnectionId()=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    HttpRequestMessage Request(HttpMethod method,string url,JsonObject? body=null)
    {var request=new HttpRequestMessage(method,url);if(provider=="Gemini")request.Headers.Add("x-goog-api-key",key);else request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);if(body!=null)request.Content=new StringContent(body.ToJsonString(),Encoding.UTF8,"application/json");return request;}
    async Task<JsonDocument> Json(HttpRequestMessage request,CancellationToken token,string? model=null)
    {
        using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);if(!response.IsSuccessStatusCode)throw await ApiError(response,model,token);
        return JsonDocument.Parse(await ReadBounded(response,Limit*2,token));
    }
    internal async Task<InvalidOperationException> ApiError(HttpResponseMessage response,string? model,CancellationToken token)
    {
        // Error bodies can echo credentials or invalid input. Read a bounded prefix,
        // extract only plain error fields, redact secrets, and never log the raw body.
        var buffer=new byte[16384];var length=0;await using var stream=await response.Content.ReadAsStreamAsync(token);
        while(length<buffer.Length){var read=await stream.ReadAsync(buffer.AsMemory(length),token);if(read==0)break;length+=read;}
        var message="";var code="";var zeroFreeQuota=false;
        try {
            using var doc=JsonDocument.Parse(buffer.AsMemory(0,length));var error=doc.RootElement.ValueKind==JsonValueKind.Object&&doc.RootElement.TryGetProperty("error",out var nested)?nested:doc.RootElement;
            message=error.ValueKind==JsonValueKind.String?error.GetString()??"":String(error,"message");code=String(error,"status",String(error,"code"));
            if(error.ValueKind==JsonValueKind.Object&&error.TryGetProperty("details",out var details)&&details.ValueKind==JsonValueKind.Array)foreach(var detail in details.EnumerateArray())if(detail.ValueKind==JsonValueKind.Object&&detail.TryGetProperty("violations",out var violations)&&violations.ValueKind==JsonValueKind.Array)foreach(var violation in violations.EnumerateArray())if(String(violation,"quotaValue")=="0"&&String(violation,"quotaMetric").Contains("free_tier",StringComparison.OrdinalIgnoreCase))zeroFreeQuota=true;
        }catch(JsonException){}
        string Safe(string value,int limit){if(key.Length>0)value=value.Replace(key,"[redacted]",StringComparison.Ordinal).Replace(Uri.EscapeDataString(key),"[redacted]",StringComparison.Ordinal);value=Regex.Replace(value,@"\s+"," ").Trim();value=new string(value.Where(c=>!char.IsControl(c)).ToArray());return value.Length>limit?value[..limit]+"…":value;}
        message=Safe(message,900);code=Safe(code,80);
        zeroFreeQuota|=Regex.IsMatch(message,@"(?i)\b0\s+(?:requests|(?:(?:input|output)\s+)?tokens)\s+per\s+(?:day|minute)\s+on\s+Free\s+Tier\b");
        var status=(int)response.StatusCode;var label="Media API · "+provider+(model!=null?" · "+model:"")+" · HTTP "+status+(code.Length>0?" · "+code:"");
        if(message.Length==0)message=AssistantProtocol.ErrorMessage(status);
        if(status==429&&zeroFreeQuota)message="This media model has no generation quota on this key's Free Tier (limit 0). Enable API billing for its Google Cloud project, or use a key with image/video generation quota. Waiting or simplifying the prompt will not enable this quota. Service: "+message;
        return new InvalidOperationException(label+"\n"+message);
    }
    async Task<byte[]> DownloadGoogle(string value,GeneratedMediaJob job,CancellationToken token)
    {
        var uri=GoogleFile(value);var file=Regex.Match(uri.AbsolutePath,@"/v1beta/files/([A-Za-z0-9_-]+)");if(!file.Success)throw new InvalidOperationException("The service returned an unsupported media download URL.");
        var baseUrl="https://generativelanguage.googleapis.com/v1beta/files/"+file.Groups[1].Value;
        while(true) {
            using var query=Request(HttpMethod.Get,baseUrl);using var doc=await Json(query,token);var state=String(doc.RootElement,"state");
            if(state=="FAILED")throw new InvalidOperationException("The service could not finish processing the video.");if(state=="ACTIVE")break;
            if(state is not ("PROCESSING" or "PENDING"))throw new InvalidOperationException("The service returned an unknown video processing state.");job.Update("Processing video at the service…");await Task.Delay(5000,token);
        }
        var target=new Uri(baseUrl+":download?alt=media");
        for(var redirect=0;redirect<4;redirect++) {
            using var request=target.Host=="generativelanguage.googleapis.com"?Request(HttpMethod.Get,target.AbsoluteUri):new HttpRequestMessage(HttpMethod.Get,target);
            using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);
            if((int)response.StatusCode is 301 or 302 or 303 or 307 or 308) {
                var location=response.Headers.Location;if(location==null)throw new InvalidOperationException("Missing media redirect.");var next=location.IsAbsoluteUri?location:new Uri(target,location);
                if(next.Scheme!="https"||!next.IsDefaultPort||next.UserInfo.Length>0||!(next.Host=="storage.googleapis.com"||next.Host.EndsWith(".googleusercontent.com",StringComparison.Ordinal)))throw new InvalidOperationException("The service returned an untrusted media redirect.");
                // Signed Google storage URLs are downloaded anonymously; the API key is never forwarded.
                target=next;continue;
            }
            if(!response.IsSuccessStatusCode)throw new InvalidOperationException("Could not download generated media (HTTP "+(int)response.StatusCode+").");
            return await ReadBounded(response,Limit,token,(done,total)=>job.Update("Downloading video…",total>0?(int)Math.Min(100,done*100/total):null));
        }throw new InvalidOperationException("Too many media download redirects.");
    }
    internal static Uri GoogleFile(string value)
    {
        if(value.StartsWith("files/",StringComparison.Ordinal))value="https://generativelanguage.googleapis.com/v1beta/"+value;
        if(!Uri.TryCreate(value,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Host!="generativelanguage.googleapis.com"||!uri.IsDefaultPort||uri.UserInfo.Length>0||!Regex.IsMatch(uri.AbsolutePath,@"^/v1beta/files/[A-Za-z0-9_-]+(?::download)?$"))throw new InvalidOperationException("The service returned an untrusted media URL.");return uri;
    }
    static async Task<byte[]> ReadBounded(HttpResponseMessage response,int limit,CancellationToken token,Action<long,long>? progress=null)
    {
        var size=response.Content.Headers.ContentLength??0;if(size>limit)throw new InvalidOperationException("Generated media exceeds the 128 MB output limit.");
        await using var stream=await response.Content.ReadAsStreamAsync(token);using var result=new MemoryStream();var buffer=new byte[65536];int count;
        while((count=await stream.ReadAsync(buffer,token))>0){if(result.Length+count>limit)throw new InvalidOperationException("Generated media exceeds the output limit.");result.Write(buffer,0,count);progress?.Invoke(result.Length,size);}return result.ToArray();
    }
    static byte[] Decode(string data){if(data.Length>Limit*4L/3+4)throw new InvalidOperationException("Generated media exceeds the output limit.");try{return Convert.FromBase64String(data);}catch(FormatException){throw new InvalidOperationException("The service returned invalid media data.");}}
    static JsonElement FindOutput(JsonElement root,bool video,bool classic)
    {
        if(root.TryGetProperty(video?"output_video":"output_image",out var output)&&output.ValueKind==JsonValueKind.Object)return output;
        if(classic&&root.TryGetProperty("candidates",out var candidates))foreach(var candidate in candidates.EnumerateArray())if(candidate.TryGetProperty("content",out var content)&&content.TryGetProperty("parts",out var parts))foreach(var part in parts.EnumerateArray())if(!(part.TryGetProperty("thought",out var thought)&&thought.ValueKind==JsonValueKind.True)&&part.TryGetProperty("inlineData",out output))return output;
        if(root.TryGetProperty("steps",out var steps))foreach(var step in steps.EnumerateArray())if(String(step,"type")=="model_output"&&step.TryGetProperty("content",out var contents))foreach(var item in contents.EnumerateArray())if(String(item,"type")== (video?"video":"image"))return item;
        throw new InvalidOperationException("The model returned no generated media. Check model access or revise the request.");
    }
    static string String(JsonElement element,string name,string fallback="")=>element.ValueKind==JsonValueKind.Object&&element.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()??fallback:fallback;
    internal static string Validate(byte[] bytes,string mime)
    {
        if(bytes.Length<12||bytes.Length>Limit)throw new InvalidOperationException("The generated file is empty or too large.");
        var actual=Detect(bytes);
        if(actual.Length==0||actual!=mime)throw new InvalidOperationException("The returned file does not match its media format.");return actual;
    }
    internal static string Detect(byte[] bytes)
    {
        if(bytes.Length<12)return "";var span=bytes.AsSpan();
        return span.StartsWith(new byte[]{137,80,78,71,13,10,26,10})?"image/png":span[0]==255&&span[1]==216&&span[2]==255?"image/jpeg":span[..6].SequenceEqual("GIF89a"u8)||span[..6].SequenceEqual("GIF87a"u8)?"image/gif":span[..4].SequenceEqual("RIFF"u8)&&span.Slice(8,4).SequenceEqual("WEBP"u8)?"image/webp":span.Slice(4,4).SequenceEqual("ftyp"u8)?"video/mp4":span[..4].SequenceEqual("RIFF"u8)&&span.Slice(8,4).SequenceEqual("WAVE"u8)?"audio/wav":span[..3].SequenceEqual("ID3"u8)||span[0]==255&&(span[1]&0xE0)==0xE0?"audio/mpeg":span[..4].SequenceEqual("fLaC"u8)?"audio/flac":span[..4].SequenceEqual("glTF"u8)?"model/gltf-binary":span[..4].SequenceEqual("ply\n"u8)||span[..4].SequenceEqual("ply\r"u8)?"application/ply":"";
    }
}
