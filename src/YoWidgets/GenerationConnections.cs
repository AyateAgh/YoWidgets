using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace YoWidgets;

internal sealed record ResolvedGeneration(string Kind,string Provider,string Model,string KeyProtected,string Voice);
internal sealed record ConnectionCheck(string Message,IReadOnlyList<string> Models,bool ModelAvailable);
internal static class GenerationConnections
{
    internal static readonly (string Id,string Title,string Icon)[] Categories=[("image","Images","image"),("video","Video","play"),("model3d","3D models","shapes"),("audio","Audio","microphone")];
    internal static string Category(string kind)=>kind=="animation"?"video":kind;
    internal static string[] Providers(string kind)=>Category(kind) switch {"image"=>["Pollinations","Gemini","OpenAI","OpenRouter"],"video"=>["Pollinations","Gemini"],"model3d" or "audio"=>["Pollinations"],_=>[]};
    internal static GenerationConnection Get(AppSettings settings,string kind)=>Category(kind) switch {"image"=>settings.ImageConnection,"video"=>settings.VideoConnection,"model3d"=>settings.Model3DConnection,"audio"=>settings.AudioConnection,_=>throw new ArgumentException("Unknown generation category.")};
    internal static void Set(AppSettings settings,string kind,GenerationConnection value){switch(Category(kind)){case "image":settings.ImageConnection=value;break;case "video":settings.VideoConnection=value;break;case "model3d":settings.Model3DConnection=value;break;case "audio":settings.AudioConnection=value;break;default:throw new ArgumentException("Unknown generation category.");}}
    internal static string DefaultModel(string kind,string provider)=>provider=="Pollinations"?Category(kind) switch{"image"=>"tongyi-mai/z-image-turbo","video"=>"x-ai/grok-imagine-video-1.5-lite","model3d"=>"hyper3d/rodin-2.5","audio"=>"openai/tts-1",_=>""}:Category(kind) switch {"image"=>GeneratedMediaService.DefaultImage(provider),"video"=>GeneratedMediaService.DefaultVideo(provider),_=>""};
    internal static string DefaultVoice(string provider)=>"alloy";
    internal static ResolvedGeneration Resolve(AppSettings settings,string kind,GenerationConnection? configuration=null)
    {
        var category=Category(kind);var selected=configuration??Get(settings,category);if(selected.Mode=="off")throw new InvalidOperationException("Enable "+Categories.First(c=>c.Id==category).Title+" in Settings → Generation connections.");
        var provider=selected.Mode=="separate"?selected.Provider:settings.AiProvider;var key=selected.Mode=="separate"?selected.KeyProtected:settings.AiKeyProtected;
        if(!Providers(category).Contains(provider))throw new InvalidOperationException("Choose a supported "+category+" service and its own key in Settings → Generation connections.");
        if(string.IsNullOrWhiteSpace(key))throw new InvalidOperationException("Add an API key for "+category+" generation in Settings.");
        var model=selected.Model.Trim();
        if(model.Length==0&&selected.Mode=="chat")model=category switch{"image"=>settings.AiImageModel,"video"=>settings.AiVideoModel,_=>""};
        if(model.Length==0)model=DefaultModel(category,provider);
        if(!Regex.IsMatch(model,@"^[A-Za-z0-9][A-Za-z0-9._:/-]{0,159}$"))throw new InvalidOperationException("Choose a valid generation model ID in Settings.");
        return new(kind,provider,model,key,selected.Voice.Length>0?selected.Voice:DefaultVoice(provider));
    }
    internal static string Describe(AppSettings settings)=>string.Join("; ",Categories.Select(c=>{try{var v=Resolve(settings,c.Id);return c.Id+": "+v.Provider+" / "+v.Model;}catch(InvalidOperationException){return c.Id+": not configured";}}));
    internal static async Task<ConnectionCheck> Check(HttpClient http,ResolvedGeneration connection,string secret,CancellationToken token)
    {
        if(connection.Provider=="Pollinations")return await PollinationsCatalog.Check(http,connection,secret,token);
        var provider=connection.Provider;var url=provider switch{"Gemini"=>"https://generativelanguage.googleapis.com/v1beta/models?pageSize=100","OpenAI"=>"https://api.openai.com/v1/models","OpenRouter"=>"https://openrouter.ai/api/v1/key",_=>throw new InvalidOperationException("Unsupported generation service.")};
        using var request=new HttpRequestMessage(HttpMethod.Get,url);
        if(provider=="Gemini")request.Headers.Add("x-goog-api-key",secret);else request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",secret);
        using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);
        if(!response.IsSuccessStatusCode)throw new InvalidOperationException(provider+" · HTTP "+(int)response.StatusCode+" · "+AssistantProtocol.ErrorMessage((int)response.StatusCode));
        if(provider=="OpenRouter")return new("Key accepted. Generation access/quota is not yet tested.",[],false);
        if(response.Content.Headers.ContentLength>2*1024*1024)throw new InvalidOperationException("Model list is too large.");
        await using var stream=await response.Content.ReadAsStreamAsync(token);using var memory=new MemoryStream();var buffer=new byte[8192];int read;while((read=await stream.ReadAsync(buffer,token))>0){if(memory.Length+read>2*1024*1024)throw new InvalidOperationException("Model list is too large.");memory.Write(buffer,0,read);}
        using var doc=JsonDocument.Parse(memory.ToArray());var names=provider=="Gemini"?doc.RootElement.GetProperty("models").EnumerateArray().Select(m=>(m.GetProperty("name").GetString()??"").Replace("models/","")):doc.RootElement.GetProperty("data").EnumerateArray().Select(m=>m.GetProperty("id").GetString()??"");
        var models=names.Distinct().Order().ToArray();var available=models.Contains(connection.Model);
        return new(available?"Key accepted · model listed. Generation quota is not yet tested.":"Key accepted · selected model was not found on this model-list page. Check the model ID before generating.",models,available);
    }
}
