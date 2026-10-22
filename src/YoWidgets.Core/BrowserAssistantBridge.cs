using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace YoWidgets.Core;

public sealed record BrowserTab(string Id,string Title,string Url,string Browser);
public sealed class BrowserAssistantBridge(string pairingToken,int port=37892) : IDisposable
{
    public const string ExtensionId="jalemmnjlhnoaiknohnlnmlcpfghoceb";
    public const string Origin="chrome-extension://"+ExtensionId;
    readonly ConcurrentDictionary<string,Connection> connections=new();
    readonly ConcurrentDictionary<string,BrowserTab> tabs=new();
    readonly ConcurrentDictionary<string,string> authorized=new();
    readonly SemaphoreSlim slots=new(4);
    CancellationTokenSource? lifetime;
    TcpListener? listener;
    bool disposed;
    public int Port {get;private set;}
    public event Action? Changed;
    public IReadOnlyList<BrowserTab> Tabs=>tabs.Values.OrderBy(t=>t.Browser).ThenBy(t=>t.Title,StringComparer.CurrentCultureIgnoreCase).Take(100).ToArray();
    public void Start()
    {
        if(disposed||lifetime!=null)return;
        try {listener=new TcpListener(IPAddress.Loopback,port);listener.Start(4);Port=((IPEndPoint)listener.LocalEndpoint).Port;lifetime=new();_ = Accept(lifetime.Token);}catch(SocketException){listener?.Stop();listener=null;}
    }
    async Task Accept(CancellationToken token)
    {
        try {while(!token.IsCancellationRequested){var client=await listener!.AcceptTcpClientAsync(token).ConfigureAwait(false);if(!slots.Wait(0)){client.Dispose();continue;}_ = Handle(client,token);}}catch(Exception e) when(e is SocketException or OperationCanceledException or ObjectDisposedException){}
    }
    async Task Handle(TcpClient client,CancellationToken token)
    {
        var id=Guid.NewGuid().ToString("N");Connection? connection=null;
        using var session=CancellationTokenSource.CreateLinkedTokenSource(token);
        try {
            using var stream=client.GetStream();using(var handshake=CancellationTokenSource.CreateLinkedTokenSource(token)) {
                handshake.CancelAfter(TimeSpan.FromSeconds(5));var header=new List<byte>();var one=new byte[1];
                while(header.Count<8192){if(await stream.ReadAsync(one,handshake.Token).ConfigureAwait(false)!=1)return;header.Add(one[0]);if(header.Count>=4&&header[^4]==13&&header[^3]==10&&header[^2]==13&&header[^1]==10)break;}
                var lines=Encoding.ASCII.GetString(header.ToArray()).Split("\r\n");var headers=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                foreach(var line in lines.Skip(1).Where(l=>l.Contains(':'))){var split=line.IndexOf(':');if(!headers.TryAdd(line[..split].Trim(),line[(split+1)..].Trim()))return;}
                if(lines[0]!="GET /yowidgets-assistant-v1 HTTP/1.1"||!headers.TryGetValue("Origin",out var origin)||origin!=Origin||!headers.TryGetValue("Host",out var host)||host!="127.0.0.1:"+Port||!headers.TryGetValue("Upgrade",out var upgrade)||!upgrade.Equals("websocket",StringComparison.OrdinalIgnoreCase)||!headers.TryGetValue("Connection",out var mode)||!mode.Split(',').Any(v=>v.Trim().Equals("Upgrade",StringComparison.OrdinalIgnoreCase))||!headers.TryGetValue("Sec-WebSocket-Version",out var version)||version!="13"||!headers.TryGetValue("Sec-WebSocket-Key",out var key)||Convert.FromBase64String(key).Length!=16)return;
                var accept=Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key+"258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: "+accept+"\r\n\r\n"),handshake.Token).ConfigureAwait(false);
            }
            using var socket=WebSocket.CreateFromStream(stream,true,null,TimeSpan.FromSeconds(20));connection=new(socket);var buffer=new byte[2_500_000];bool paired=false;
            using var hello=CancellationTokenSource.CreateLinkedTokenSource(session.Token);hello.CancelAfter(TimeSpan.FromSeconds(5));
            while(!session.IsCancellationRequested) {
                int count=0;ValueWebSocketReceiveResult part;
                do {part=await socket.ReceiveAsync(buffer.AsMemory(count),paired?session.Token:hello.Token).ConfigureAwait(false);if(part.MessageType!=WebSocketMessageType.Text)return;count+=part.Count;if(count>=buffer.Length)return;}while(!part.EndOfMessage);
                using var doc=JsonDocument.Parse(buffer.AsMemory(0,count));var payload=doc.RootElement;var type=payload.GetProperty("type").GetString();
                if(!paired) {
                    if(type!="hello"||!payload.TryGetProperty("token",out var value)||value.ValueKind!=JsonValueKind.String||!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(value.GetString()??""),Encoding.UTF8.GetBytes(pairingToken)))return;
                    paired=true;connections[id]=connection;await connection.Send(new {type="ready"},session.Token).ConfigureAwait(false);_ = Probe(connection,session.Token);continue;
                }
                if(type=="tabs"&&payload.TryGetProperty("tabs",out var items)&&items.ValueKind==JsonValueKind.Array) {
                    var kept=new HashSet<string>();foreach(var item in items.EnumerateArray().Take(100)) {
                        var tabId=item.GetProperty("id").GetString()??"";var title=item.GetProperty("title").GetString()??"";var url=item.GetProperty("url").GetString()??"";var browser=item.GetProperty("browser").GetString()??"Browser";
                        if(tabId.Length is <1 or >20||!tabId.All(char.IsAsciiDigit)||title.Length>500||!ValidUrl(url)||browser.Length>80)continue;
                        var owned=id+"/"+tabId;if(tabs.TryGetValue(owned,out var old)&&old.Url!=url)authorized.TryRemove(owned,out _);tabs[owned]=new(owned,title.Length==0?url:title,url,browser);kept.Add(owned);
                    }
                    foreach(var old in tabs.Keys.Where(k=>k.StartsWith(id+"/",StringComparison.Ordinal)&&!kept.Contains(k))){tabs.TryRemove(old,out _);authorized.TryRemove(old,out _);}Changed?.Invoke();
                }else if(type=="result"&&payload.TryGetProperty("requestId",out var request)&&connection.Pending.TryGetValue(request.GetString()??"",out var pending))pending.TrySetResult(payload.Clone());
            }
        }catch(Exception e) when(e is IOException or SocketException or WebSocketException or JsonException or OperationCanceledException or InvalidOperationException or ArgumentException or FormatException or KeyNotFoundException or ObjectDisposedException){}
        finally {session.Cancel();connections.TryRemove(id,out _);foreach(var key in tabs.Keys.Where(k=>k.StartsWith(id+"/",StringComparison.Ordinal))){tabs.TryRemove(key,out _);authorized.TryRemove(key,out _);}if(connection!=null)foreach(var pending in connection.Pending.Values)pending.TrySetException(new InvalidOperationException("The browser companion disconnected."));client.Dispose();slots.Release();Changed?.Invoke();}
    }
    public static bool ValidUrl(string url)=>url.Length<=4000&&Uri.TryCreate(url,UriKind.Absolute,out var uri)&&uri.Scheme is "http" or "https"&&string.IsNullOrEmpty(uri.UserInfo);
    public BrowserTab Authorize(BrowserTab tab)
    {if(!tabs.TryGetValue(tab.Id,out var current)||current.Url!=tab.Url)throw new InvalidOperationException("This tab changed. Attach it again.");authorized[tab.Id]=tab.Url;return current;}
    public async Task<JsonElement> RequestAsync(string id,string action,object payload,CancellationToken token)
    {
        if(!tabs.TryGetValue(id,out var tab)||!authorized.TryGetValue(id,out var url)||url!=tab.Url)throw new InvalidOperationException("Attach this browser tab with the plus menu before inspecting or controlling it.");
        var split=id.IndexOf('/');if(split<0||!connections.TryGetValue(id[..split],out var connection))throw new InvalidOperationException("The browser companion is not connected.");
        var requestId=Guid.NewGuid().ToString("N");var completion=new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);connection.Pending[requestId]=completion;
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(30));
        try {await connection.Send(new {type="request",requestId,tabId=id[(split+1)..],expectedUrl=url,action,payload},deadline.Token).ConfigureAwait(false);var result=await completion.Task.WaitAsync(deadline.Token).ConfigureAwait(false);if(result.TryGetProperty("error",out var error))throw new InvalidOperationException(error.GetString()??"Browser action failed.");return result.GetProperty("result").Clone();}
        catch(OperationCanceledException) when(!token.IsCancellationRequested){throw new InvalidOperationException("The browser action did not finish. Read the tab again before retrying.");}
        finally {connection.Pending.TryRemove(requestId,out _);}
    }
    static async Task Probe(Connection connection,CancellationToken token)
    {try {while(!token.IsCancellationRequested){await connection.Send(new {type="probe"},token).ConfigureAwait(false);await Task.Delay(TimeSpan.FromSeconds(25),token).ConfigureAwait(false);}}catch(Exception e) when(e is WebSocketException or OperationCanceledException or IOException or ObjectDisposedException){}}
    public void Stop() {lifetime?.Cancel();listener?.Stop();foreach(var c in connections.Values)c.Socket.Abort();lifetime?.Dispose();lifetime=null;listener=null;tabs.Clear();authorized.Clear();Changed?.Invoke();}
    public void Dispose(){if(disposed)return;disposed=true;Stop();}
    sealed class Connection(WebSocket socket)
    {
        readonly SemaphoreSlim sending=new(1);
        public WebSocket Socket {get;}=socket;
        public ConcurrentDictionary<string,TaskCompletionSource<JsonElement>> Pending {get;}=new();
        public async Task Send(object value,CancellationToken token) {await sending.WaitAsync(token).ConfigureAwait(false);try {await Socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(value),WebSocketMessageType.Text,true,token).ConfigureAwait(false);}finally {sending.Release();}}
    }
}
