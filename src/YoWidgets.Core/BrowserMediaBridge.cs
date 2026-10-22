using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace YoWidgets.Core;

// Loopback only. The browser controls site access; the listener accepts only our
// fixed extension Origin, never arbitrary web Origins or remote executable code.
public sealed class BrowserMediaBridge(int port = 37891) : IDisposable
{
    public const string ExtensionId = "fllmnphklooimbfkdomfjlgjnphkkncm";
    public const string Origin = "chrome-extension://" + ExtensionId;
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    readonly ConcurrentDictionary<string, Connection> connections = new();
    readonly ConcurrentDictionary<string, (BrowserMediaState State, DateTimeOffset Seen)> states = new();
    readonly SemaphoreSlim slots = new(4);
    CancellationTokenSource? lifetime;
    TcpListener? listener;
    bool disposed;
    public event Action? Changed;
    public int Port { get; private set; }
    public string Status { get; private set; } = "Not connected";
    public IReadOnlyList<BrowserMediaState> States => states.Values.Where(s => DateTimeOffset.UtcNow - s.Seen < TimeSpan.FromSeconds(45)).Select(s => s.State).ToArray();
    public void Start()
    {
        if (disposed || lifetime != null) return;
        try
        {
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start(4);
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            lifetime = new();
            Status = "Not connected · optional SoundCloud extension";
            _ = Accept(listener, lifetime.Token);
        }
        catch (SocketException) { listener?.Stop(); listener = null; Status = "Local bridge port unavailable"; }
    }
    async Task Accept(TcpListener server, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await server.AcceptTcpClientAsync(ct).ConfigureAwait(false);
                if (!slots.Wait(0)) { client.Dispose(); continue; }
                _ = Handle(client, ct);
            }
        }
        catch (Exception e) when (e is OperationCanceledException or SocketException or ObjectDisposedException) { }
    }
    async Task Handle(TcpClient client, CancellationToken ct)
    {
        string id = Guid.NewGuid().ToString("N");
        Connection? connection = null;
        using var session = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            using var stream = client.GetStream();
            using (var handshake = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                handshake.CancelAfter(TimeSpan.FromSeconds(5));
                var header = new List<byte>();
                var one = new byte[1];
                while (header.Count < 8192)
                {
                    if (await stream.ReadAsync(one, handshake.Token).ConfigureAwait(false) != 1) return;
                    header.Add(one[0]);
                    if (header.Count >= 4 && header[^4] == 13 && header[^3] == 10 && header[^2] == 13 && header[^1] == 10) break;
                }
                var lines = Encoding.ASCII.GetString(header.ToArray()).Split("\r\n");
                if (header.Count < 4 || header[^4] != 13 || header[^3] != 10 || header[^2] != 13 || header[^1] != 10) return;
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in lines.Skip(1).Where(l => l.Contains(':'))) { var split = line.IndexOf(':'); if (!headers.TryAdd(line[..split].Trim(), line[(split + 1)..].Trim())) return; }
                if (lines[0] != "GET /yowidgets-media-v1 HTTP/1.1" || !headers.TryGetValue("Origin", out var origin) || origin != Origin ||
                    !headers.TryGetValue("Host", out var host) || host != "127.0.0.1:" + Port ||
                    !headers.TryGetValue("Upgrade", out var upgrade) || !upgrade.Equals("websocket", StringComparison.OrdinalIgnoreCase) ||
                    !headers.TryGetValue("Connection", out var connectionHeader) || !connectionHeader.Split(',').Any(v => v.Trim().Equals("Upgrade", StringComparison.OrdinalIgnoreCase)) ||
                    !headers.TryGetValue("Sec-WebSocket-Version", out var version) || version != "13" ||
                    !headers.TryGetValue("Sec-WebSocket-Key", out var key) || Convert.FromBase64String(key).Length != 16) return;
                var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
                await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n"), handshake.Token).ConfigureAwait(false);
            }
            using var socket = WebSocket.CreateFromStream(stream, true, null, TimeSpan.FromSeconds(20));
            connection = new(socket);
            connections[id] = connection;
            Status = "SoundCloud bridge connected";
            Changed?.Invoke();
            _ = Probe(connection, session.Token);
            var buffer = new byte[32768];
            while (!session.IsCancellationRequested)
            {
                int count = 0;
                ValueWebSocketReceiveResult part;
                do
                {
                    part = await socket.ReceiveAsync(buffer.AsMemory(count), session.Token).ConfigureAwait(false);
                    if (part.MessageType != WebSocketMessageType.Text) return;
                    count += part.Count;
                    if (count >= buffer.Length) return;
                } while (!part.EndOfMessage);
                using var document = JsonDocument.Parse(buffer.AsMemory(0, count));
                var payload = document.RootElement;
                if (!payload.TryGetProperty("type", out var type)) continue;
                if (type.GetString() == "state" && payload.TryGetProperty("states", out var list) && list.ValueKind == JsonValueKind.Array)
                {
                    var retained = new HashSet<string>(); bool changed = false;
                    foreach (var value in list.EnumerateArray().Take(16))
                    {
                        var state = value.Deserialize<BrowserMediaState>(Json);
                        if (state == null || state.Provider != "soundcloud" || string.IsNullOrWhiteSpace(state.Id) || state.Id.Length > 80 || state.Title == null || state.Title.Length > 1024 || state.Artist == null || state.Artist.Length > 256 || !double.IsFinite(state.Duration) || state.Duration <= 0 || state.Duration > 86400 || state.Repeat is < 0 or > 2) continue;
                        var owned = id + "/" + state.Id;
                        state = state with { Id = owned };
                        changed |= !states.TryGetValue(owned, out var old) || old.State != state;
                        states[owned] = (state, DateTimeOffset.UtcNow);
                        retained.Add(owned);
                    }
                    foreach (var entry in states.Keys.Where(k => k.StartsWith(id + "/", StringComparison.Ordinal) && !retained.Contains(k))) changed |= states.TryRemove(entry, out _);
                    if (changed) Changed?.Invoke();
                }
                else if (type.GetString() == "ack" && payload.TryGetProperty("requestId", out var request) && request.ValueKind == JsonValueKind.String && request.GetString() is { } requestId && connection.Pending.TryGetValue(requestId, out var pending))
                    pending.TrySetResult(payload.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True);
            }
        }
        catch (Exception e) when (e is IOException or WebSocketException or OperationCanceledException or ObjectDisposedException or JsonException or FormatException or InvalidOperationException or ArgumentException) { }
        finally
        {
            session.Cancel();
            client.Dispose();
            connections.TryRemove(id, out _);
            foreach (var key in states.Keys.Where(k => k.StartsWith(id + "/", StringComparison.Ordinal))) states.TryRemove(key, out _);
            if (connection != null) foreach (var pending in connection.Pending.Values) pending.TrySetResult(false);
            Status = connections.IsEmpty ? "Not connected" : "SoundCloud bridge connected";
            Changed?.Invoke();
            slots.Release();
        }
    }
    static async Task Probe(Connection connection, CancellationToken ct)
    {
        try { while (!ct.IsCancellationRequested) { await connection.Send(new { type = "probe" }, ct).ConfigureAwait(false); await Task.Delay(TimeSpan.FromSeconds(20), ct).ConfigureAwait(false); } }
        catch (Exception e) when (e is IOException or WebSocketException or OperationCanceledException or ObjectDisposedException or InvalidOperationException) { }
    }
    public async Task<bool> CommandAsync(BrowserMediaState target, string action, CancellationToken ct = default)
    {
        if (action is not ("shuffle" or "repeat")) return false;
        if (!states.TryGetValue(target.Id, out var known) || DateTimeOffset.UtcNow - known.Seen > TimeSpan.FromSeconds(45) || known.State != target) return false;
        var slash = target.Id.IndexOf('/');
        if (slash < 0 || !connections.TryGetValue(target.Id[..slash], out var connection)) return false;
        var requestId = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Pending[requestId] = completion;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            await connection.Send(new { type = "command", requestId, targetId = target.Id[(slash + 1)..], action, expectedTitle = target.Title, expectedDuration = target.Duration, expectedPlaying = target.Playing }, timeout.Token).ConfigureAwait(false);
            return await completion.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or WebSocketException or OperationCanceledException or ObjectDisposedException or InvalidOperationException) { return false; }
        finally { connection.Pending.TryRemove(requestId, out _); }
    }
    public void Stop()
    {
        lifetime?.Cancel(); listener?.Stop();
        foreach (var connection in connections.Values) connection.Socket.Abort();
        lifetime?.Dispose(); lifetime = null; listener = null;
        states.Clear();
        Status = "Not connected";
    }
    public void Dispose() { if (disposed) return; disposed = true; Stop(); }
    sealed class Connection(WebSocket socket)
    {
        readonly SemaphoreSlim sending = new(1);
        public WebSocket Socket { get; } = socket;
        public ConcurrentDictionary<string, TaskCompletionSource<bool>> Pending { get; } = new();
        public async Task Send(object payload, CancellationToken ct)
        {
            await sending.WaitAsync(ct).ConfigureAwait(false);
            try { await Socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(payload, Json).AsMemory(), WebSocketMessageType.Text, true, ct).ConfigureAwait(false); }
            finally { sending.Release(); }
        }
    }
}
