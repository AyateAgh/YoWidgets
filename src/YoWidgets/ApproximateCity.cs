using System.Text.Json;
namespace YoWidgets;
internal static class ApproximateCity
{
    internal static string Parse(JsonElement root)
    {
        if(root.ValueKind!=JsonValueKind.Object||root.TryGetProperty("error",out var error)&&error.ValueKind==JsonValueKind.True||!root.TryGetProperty("city",out var city)||city.ValueKind!=JsonValueKind.String)throw new InvalidOperationException("Your city could not be determined. Enter it manually.");
        var value=city.GetString()?.Trim()??"";if(value.Length is <1 or >120||value.Any(char.IsControl))throw new InvalidOperationException("Your city could not be determined. Enter it manually.");return value;
    }
    internal static async Task<string> Locate(CancellationToken token)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(8));
        using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(8)};http.DefaultRequestHeaders.UserAgent.ParseAdd("YoWidgets/0.6.1");
        try{using var response=await http.GetAsync("https://ipapi.co/json/",HttpCompletionOption.ResponseHeadersRead,deadline.Token);response.EnsureSuccessStatusCode();if(response.Content.Headers.ContentLength>32768)throw new IOException();using var stream=await response.Content.ReadAsStreamAsync(deadline.Token);using var memory=new MemoryStream();var buffer=new byte[4096];int count;while((count=await stream.ReadAsync(buffer,deadline.Token))>0){if(memory.Length+count>32768)throw new IOException();memory.Write(buffer,0,count);}using var document=JsonDocument.Parse(memory.ToArray());return Parse(document.RootElement);}
        catch(Exception e)when(e is HttpRequestException or IOException or JsonException or OperationCanceledException){throw new InvalidOperationException("Location is unavailable. Enter your city manually.");}
    }
}
