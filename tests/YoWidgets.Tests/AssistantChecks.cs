using System.Text.Json;
using YoWidgets.Core;

static class AssistantChecks
{
    public static void Run(Action<string, Action> test)
    {
        test("legacy Gemini configuration migrates away from OpenAI", () => {
            var s = new AppSettings { AiModel = "Gemini" }; s.Normalize();
            Require(s.AiProvider == "Gemini" && s.AiModel.StartsWith("gemini-"));
        });
        test("Gemini credentials go only to Google in a header", () => {
            using var r = AssistantProtocol.CreateRequest("Gemini", "gemini-flash-latest", "test-secret", [new("user", "Привет")]);
            Require(r.RequestUri!.Host == "generativelanguage.googleapis.com");
            Require(!r.RequestUri.ToString().Contains("test-secret") && r.Headers.GetValues("x-goog-api-key").Single() == "test-secret");
            using var j = JsonDocument.Parse(r.Content!.ReadAsStringAsync().Result);
            Require(j.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString() == "Привет");
        });
        test("conversation preserves alternating Gemini model roles", () => {
            using var r = AssistantProtocol.CreateRequest("Gemini", "gemini-flash-latest", "key", [new("user", "one"), new("assistant", "two"), new("user", "three")]);
            using var j = JsonDocument.Parse(r.Content!.ReadAsStringAsync().Result);
            Require(j.RootElement.GetProperty("contents")[1].GetProperty("role").GetString() == "model");
        });
        test("stream parser excludes Gemini thinking and joins visible parts", () => {
            var delta = AssistantProtocol.ParseDelta("Gemini", """{"candidates":[{"content":{"parts":[{"text":"private","thought":true},{"text":"Hello "},{"text":"world"}]}}]}""");
            Require(delta == "Hello world");
        });
        test("OpenAI stream and completion marker parse safely", () => {
            Require(AssistantProtocol.ParseDelta("OpenAI", """{"choices":[{"delta":{"content":"code"}}]}""") == "code");
            Require(AssistantProtocol.ParseDelta("OpenAI", "[DONE]") == "");
        });
        test("blocked Gemini response produces an actionable failure", () => {
            try { AssistantProtocol.ParseDelta("Gemini", """{"promptFeedback":{"blockReason":"SAFETY"}}"""); throw new Exception("block ignored"); }
            catch (InvalidOperationException e) { Require(e.Message.Contains("blocked")); }
        });
        test("invalid provider or model cannot redirect a saved API key", () => {
            foreach (var pair in new[] { ("Other", "model"), ("Gemini", "../../elsewhere?key=x") }) {
                try { using var r = AssistantProtocol.CreateRequest(pair.Item1, pair.Item2, "key", []); throw new Exception("accepted unsafe target"); }
                catch (ArgumentException) { }
            }
        });
        test("API errors are useful without reflecting provider secrets", () => {
            Require(AssistantProtocol.ErrorMessage(429).Contains("quota"));
            Require(AssistantProtocol.ErrorMessage(403).Contains("key"));
        });
        test("null legacy model normalizes and HTTP2 is preferred for Google streams", () => {
            var s = JsonSerializer.Deserialize<AppSettings>("{\"AiModel\":null}")!; s.Normalize();
            Require(s.AiModel == AssistantProtocol.DefaultModel("Gemini"));
            using var request = AssistantProtocol.CreateRequest("Gemini", s.AiModel, "key", []);
            Require(request.Version.Major == 2);
        });
    }
    static void Require(bool condition) { if (!condition) throw new Exception("Assistant contract failed"); }
}
