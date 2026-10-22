using YoWidgets.Core;
static class AssistantLayoutChecks
{
    public static void Run(Action<string, Action> test)
    {
        test("assistant grows down when there is space and preserves composer edge", () => {
            var p = AssistantLayout.Expand(new(100,100,440,112),580,new(0,0,1920,1040));
            Require(!p.Up && p.Bounds.Y == 100 && p.Bounds.Height == 580);
        });
        test("assistant grows upward near the taskbar", () => {
            var p = AssistantLayout.Expand(new(100,850,440,112),580,new(0,0,1920,1040));
            Require(p.Up && p.Bounds.Y + p.Bounds.Height == 962 && p.Bounds.Y >= 0);
        });
        test("assistant clamps to small and negative-coordinate displays", () => {
            var p = AssistantLayout.Expand(new(-1100,150,440,112),900,new(-1280,0,1280,720));
            Require(p.Bounds.Height == 720 && p.Bounds.Y == 0 && p.Bounds.X >= -1280);
        });
        test("attachment-only prompts and Claude text deltas use native contracts", () => {
            using var request = AssistantProtocol.CreateRequest("Claude", "claude-sonnet-4-6", "secret", [new("user", "Read this")]);
            Require(request.RequestUri!.Host == "api.anthropic.com" && request.Headers.Contains("x-api-key") && request.Headers.Contains("anthropic-version"));
            Require(AssistantProtocol.ParseDelta("Claude", """{"type":"content_block_delta","delta":{"type":"text_delta","text":"hello"}}""") == "hello");
            Require(AssistantProtocol.FinishReason("Claude", """{"type":"message_delta","delta":{"stop_reason":"end_turn"}}""") == "end_turn");
        });
        test("DeepSeek credentials use its own destination and token contract", () => {
            using var request = AssistantProtocol.CreateRequest("DeepSeek", "deepseek-chat", "secret", [new("user", "hello")]);
            Require(request.RequestUri!.Host == "api.deepseek.com");
            var json = request.Content!.ReadAsStringAsync().Result;
            Require(json.Contains("max_tokens") && !json.Contains("max_completion_tokens"));
        });
    }
    static void Require(bool condition) { if (!condition) throw new Exception("Assistant layout/provider contract failed"); }
}
