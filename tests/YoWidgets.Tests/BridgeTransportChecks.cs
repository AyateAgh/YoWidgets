using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using YoWidgets.Core;

static class BridgeTransportChecks
{
    internal static void Run(Action<string, Action> test)
    {
        test("bridge rejects arbitrary website origins", () => RejectOrigin().GetAwaiter().GetResult());
        test("bridge routes to the exact source, awaits confirmation and cleans disconnects", () => Roundtrip().GetAwaiter().GetResult());
    }
    static void Check(bool value) { if (!value) throw new Exception("WebSocket bridge regression"); }
    static async Task Send(ClientWebSocket socket, object value, CancellationToken ct) =>
        await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(value).AsMemory(), WebSocketMessageType.Text, true, ct);
    static async Task<JsonElement> Receive(ClientWebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[32768]; int count = 0; ValueWebSocketReceiveResult result;
        do { result = await socket.ReceiveAsync(buffer.AsMemory(count), ct); count += result.Count; } while (!result.EndOfMessage);
        return JsonDocument.Parse(buffer.AsMemory(0, count)).RootElement.Clone();
    }
    static async Task Until(Func<bool> condition, CancellationToken ct)
    { while (!condition()) await Task.Delay(10, ct); }
    static async Task RejectOrigin()
    {
        using var ct = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var bridge = new BrowserMediaBridge(0); bridge.Start();
        using var socket = new ClientWebSocket(); socket.Options.SetRequestHeader("Origin", "https://soundcloud.com");
        try { await socket.ConnectAsync(new($"ws://127.0.0.1:{bridge.Port}/yowidgets-media-v1"), ct.Token); throw new Exception("Untrusted website connected"); }
        catch (WebSocketException) { Check(bridge.States.Count == 0); }
    }
    static async Task Roundtrip()
    {
        using var ct = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        using var bridge = new BrowserMediaBridge(0); bridge.Start();
        using var socket = new ClientWebSocket(); socket.Options.SetRequestHeader("Origin", BrowserMediaBridge.Origin);
        await socket.ConnectAsync(new($"ws://127.0.0.1:{bridge.Port}/yowidgets-media-v1"), ct.Token);
        Check((await Receive(socket, ct.Token)).GetProperty("type").GetString() == "probe");
        var state = new BrowserMediaState("17", "soundcloud", "Test song", "Test artist", 197, true, true, true, false, 0);
        await Send(socket, new { type = "state", states = new[] { state } }, ct.Token);
        await Until(() => bridge.States.Count == 1, ct.Token);
        var owned = bridge.States.Single(); Check(owned.Id.EndsWith("/17"));
        Check(!await bridge.CommandAsync(owned, "unsupported", ct.Token));
        var commandTask = bridge.CommandAsync(owned, "shuffle", ct.Token);
        var command = await Receive(socket, ct.Token);
        Check(command.GetProperty("targetId").GetString() == "17");
        Check(command.GetProperty("action").GetString() == "shuffle");
        Check(command.GetProperty("expectedTitle").GetString() == state.Title);
        Check(command.GetProperty("expectedPlaying").GetBoolean() == state.Playing);
        Check(!commandTask.IsCompleted && bridge.States.Single().Shuffle == false);
        await Send(socket, new { type = "state", states = new[] { state with { Shuffle = true } } }, ct.Token);
        await Send(socket, new { type = "ack", requestId = command.GetProperty("requestId").GetString(), ok = true }, ct.Token);
        Check(await commandTask); Check(bridge.States.Single().Shuffle == true);
        Check(!await bridge.CommandAsync(owned, "repeat", ct.Token)); // A stale record cannot target a changed source.
        var refused = bridge.CommandAsync(bridge.States.Single(), "repeat", ct.Token);
        command = await Receive(socket, ct.Token);
        await Send(socket, new { type = "ack", requestId = command.GetProperty("requestId").GetString(), ok = false }, ct.Token);
        Check(!await refused); Check(bridge.States.Single().Repeat == 0);
        socket.Abort(); await Until(() => bridge.States.Count == 0, ct.Token);
        bridge.Stop(); Check(!await bridge.CommandAsync(owned, "shuffle", ct.Token));
    }
}
