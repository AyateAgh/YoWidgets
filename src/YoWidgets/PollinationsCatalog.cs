using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
namespace YoWidgets;

internal sealed record PollinationsModel(string Id,string Title,string Category,string[] Aliases,string[] Inputs,string[] Outputs,bool PaidOnly,string Price,string[] Voices,string Limits);
internal sealed record PollinationsAccount(bool Valid,string Type,string? Expires,string[]? AllowedModels,decimal? Budget,decimal? Balance,bool QuestOnly,bool RateLimited,string BalanceNote)
{
    internal bool Allows(string category,PollinationsModel? model)=>AllowedModels==null||AllowedModels.Contains(category)||AllowedModels.Contains(model?.Id??"")||model?.Aliases.Any(AllowedModels.Contains)==true;
    internal string Summary=>"Key accepted · "+Type+(Balance.HasValue?" · "+Balance.Value.ToString("0.####",CultureInfo.InvariantCulture)+" Pollen":"")+(Budget.HasValue?" · key budget "+Budget.Value.ToString("0.####",CultureInfo.InvariantCulture):"")+(QuestOnly?" · Quest Pollen only":"")+(RateLimited?" · rate limits enabled":"")+(Expires!=null?" · expires "+Expires:"")+(BalanceNote.Length>0?"\n"+BalanceNote:"");
}
internal static class PollinationsCatalog
{
    internal const string Origin="https://gen.pollinations.ai";
    internal static async Task<IReadOnlyList<PollinationsModel>> Models(HttpClient http,string kind,CancellationToken token)
    {
        var category=GenerationConnections.Category(kind);var endpoint=category=="model3d"?"3d":category;
        using var request=new HttpRequestMessage(HttpMethod.Get,Origin+"/"+endpoint+"/models?source=official");using var doc=await Read(http,request,token);
        var root=doc.RootElement;if(root.ValueKind!=JsonValueKind.Array)throw new InvalidOperationException("Pollinations returned an invalid model catalog.");
        var results=new List<PollinationsModel>();foreach(var model in root.EnumerateArray()) {
            var outputs=Strings(model,"output_modalities");if(!outputs.Contains(endpoint))continue;
            var id=Text(model,"name");if(id.Length==0)continue;
            var prices=new List<string>();if(model.TryGetProperty("pricing",out var pricing)&&pricing.ValueKind==JsonValueKind.Object)foreach(var item in pricing.EnumerateObject())if(item.Name!="currency")prices.Add((Flag(model,"flat_rate")&&item.Name=="completionImageTokens"?"per output":item.Name)+": "+item.Value.ToString());
            var limits=new List<string>();if(Number(model,"per_user_rpm") is decimal rpm)limits.Add(rpm.ToString(CultureInfo.InvariantCulture)+" requests/minute per user");if(Number(model,"max_duration") is decimal duration)limits.Add("up to "+duration.ToString(CultureInfo.InvariantCulture)+" seconds per video");if(Number(model,"max_reference_images") is decimal refs)limits.Add("up to "+refs.ToString(CultureInfo.InvariantCulture)+" image references");
            results.Add(new(id,Text(model,"title",id),endpoint,Strings(model,"aliases"),Strings(model,"input_modalities"),outputs,Flag(model,"paid_only"),prices.Count>0?string.Join(" · ",prices)+" Pollen":"Price not supplied by the catalog",Strings(model,"voices"),string.Join(" · ",limits)));
        }return results;
    }
    internal static async Task<PollinationsAccount> Account(HttpClient http,string secret,CancellationToken token)
    {
        using var query=Authorized("/account/key",secret);using var doc=await Read(http,query,token,secret);var root=doc.RootElement;
        if(!Flag(root,"valid"))throw new InvalidOperationException("Pollinations rejected this key: it is inactive, invalid or expired.");
        string[]? allowed=null;if(root.TryGetProperty("permissions",out var permissions)&&permissions.TryGetProperty("models",out var models)&&models.ValueKind==JsonValueKind.Array)allowed=models.EnumerateArray().Where(m=>m.ValueKind==JsonValueKind.String).Select(m=>m.GetString()!).ToArray();
        decimal? balance=null;var note="";
        using var balanceQuery=Authorized("/account/balance",secret);using var reply=await http.SendAsync(balanceQuery,HttpCompletionOption.ResponseHeadersRead,token);
        if(reply.IsSuccessStatusCode){using var data=await Parse(reply,token);balance=Number(data.RootElement,"balance");}
        else if((int)reply.StatusCode==403)note="Balance unavailable: this key has no account:usage permission. This does not make the key invalid.";
        else note="Balance could not be checked (HTTP "+(int)reply.StatusCode+").";
        return new(true,Text(root,"type","unknown"),root.TryGetProperty("expiresAt",out var expiry)&&expiry.ValueKind==JsonValueKind.String?expiry.GetString():null,allowed,Number(root,"pollenBudget"),balance,Flag(root,"questPollenOnly"),Flag(root,"rateLimitEnabled"),note);
    }
    internal static async Task<ConnectionCheck> Check(HttpClient http,ResolvedGeneration connection,string secret,CancellationToken token)
    {
        var account=await Account(http,secret,token);var models=await Models(http,connection.Kind,token);var selected=models.FirstOrDefault(m=>m.Id==connection.Model||m.Aliases.Contains(connection.Model));
        var category=GenerationConnections.Category(connection.Kind)=="model3d"?"3d":GenerationConnections.Category(connection.Kind);
        var allowed=account.Allows(category,selected);var text=account.Summary+"\n"+(selected==null?"Selected model is not in this category's current catalog.":!allowed?"This key does not allow the selected model/category.":selected.PaidOnly&&account.QuestOnly?"Selected model requires paid Pollen; this key is Quest-only.":"Model listed · "+selected.Title+" · "+(selected.PaidOnly?"Paid Pollen required":"Quest Pollen eligible (still consumes Pollen)")+". Generation has not been tested.");
        return new(text,models.Select(m=>m.Id).ToArray(),selected!=null&&allowed&&!(selected.PaidOnly&&account.QuestOnly));
    }
    internal static HttpRequestMessage Authorized(string path,string secret){var request=new HttpRequestMessage(HttpMethod.Get,Origin+path);request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",secret);return request;}
    static async Task<JsonDocument> Read(HttpClient http,HttpRequestMessage request,CancellationToken token,string? secret=null)
    {using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);if(!response.IsSuccessStatusCode){if(secret!=null)throw await new GeneratedMediaService(http,"Pollinations",secret,"","").ApiError(response,null,token);throw new InvalidOperationException("Pollinations catalog · HTTP "+(int)response.StatusCode);}return await Parse(response,token);}
    static async Task<JsonDocument> Parse(HttpResponseMessage response,CancellationToken token)
    {const int limit=2*1024*1024;if(response.Content.Headers.ContentLength>limit)throw new InvalidOperationException("Pollinations catalog is too large.");await using var stream=await response.Content.ReadAsStreamAsync(token);using var data=new MemoryStream();var buffer=new byte[8192];int count;while((count=await stream.ReadAsync(buffer,token))>0){if(data.Length+count>limit)throw new InvalidOperationException("Pollinations response is too large.");data.Write(buffer,0,count);}return JsonDocument.Parse(data.ToArray());}
    static string Text(JsonElement item,string name,string fallback="")=>item.ValueKind==JsonValueKind.Object&&item.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()??fallback:fallback;
    static bool Flag(JsonElement item,string name)=>item.ValueKind==JsonValueKind.Object&&item.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.True;
    static decimal? Number(JsonElement item,string name)=>item.ValueKind==JsonValueKind.Object&&item.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.Number&&value.TryGetDecimal(out var number)?number:null;
    static string[] Strings(JsonElement item,string name)=>item.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.Array?value.EnumerateArray().Where(v=>v.ValueKind==JsonValueKind.String).Select(v=>v.GetString()!).ToArray():[];
}
