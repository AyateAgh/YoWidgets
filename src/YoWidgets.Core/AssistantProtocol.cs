using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace YoWidgets.Core;

public sealed record AssistantTurn(string Role, string Content, IReadOnlyList<AssistantAttachment>? Attachments = null);

public static class AssistantProtocol
{
    public const int MaxInput = 32000;
    public const int MaxAnswer = 120000;
    public static readonly string[] Providers = ["Gemini", "OpenAI", "Claude", "DeepSeek", "OpenRouter"];
    public static string DefaultModel(string provider) => provider switch { "OpenAI" => "gpt-4.1-mini", "Claude" => "claude-sonnet-4-6", "DeepSeek" => "deepseek-chat", "OpenRouter" => "openai/gpt-4.1-mini", _ => "gemini-flash-lite-latest" };
    static string Origin(string provider) => provider switch { "Gemini" => "https://generativelanguage.googleapis.com/v1beta", "Claude" => "https://api.anthropic.com/v1", "DeepSeek" => "https://api.deepseek.com", "OpenRouter" => "https://openrouter.ai/api/v1", _ => "https://api.openai.com/v1" };
    public static HttpRequestMessage CreateRequest(string provider, string model, string key, IReadOnlyList<AssistantTurn> turns)
    {
        Validate(provider, model);
        turns = turns.Select(t => t with { Attachments = (t.Attachments ?? []).SelectMany(f => MediaParts(f,provider)).ToArray() }).ToArray();
        foreach (var attachment in turns.SelectMany(t => t.Attachments ?? []))
            if (!attachment.IsText && (provider == "DeepSeek" || provider != "Gemini" && attachment.MimeType.StartsWith("audio/") || provider != "Gemini" && attachment.MimeType.StartsWith("video/") || provider == "OpenRouter" && attachment.MimeType == "application/pdf"))
                throw new InvalidOperationException($"{provider} cannot read this attachment directly. Use a text document or choose a service/model that supports this file.");
        var request = Authorized(provider, key, HttpMethod.Post, provider == "Gemini"
            ? $"https://generativelanguage.googleapis.com/v1beta/models/{model}:streamGenerateContent?alt=sse"
            : Origin(provider) + (provider == "Claude" ? "/messages" : "/chat/completions"));
        object payload;
        if (provider == "Gemini") payload = new { contents = turns.Select(t => new { role = t.Role == "assistant" ? "model" : "user", parts = GeminiParts(t) }), generationConfig = new { maxOutputTokens = 8192 } };
        else if (provider == "Claude") payload = new { model, messages = turns.Select(t => new { role = t.Role, content = ClaudeParts(t) }), stream = true, max_tokens = 8192 };
        else
        {
            var body = new Dictionary<string, object> { ["model"] = model, ["messages"] = turns.Select(t => new { role = t.Role, content = ChatParts(t) }).ToArray(), ["stream"] = true };
            body[provider == "OpenAI" ? "max_completion_tokens" : "max_tokens"] = 8192; payload = body;
        }
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return request;
    }
    public static HttpRequestMessage ModelsRequest(string provider, string key, string? pageToken = null)
    {
        Validate(provider, DefaultModel(provider));
        return Authorized(provider, key, HttpMethod.Get, provider == "Gemini"
            ? "https://generativelanguage.googleapis.com/v1beta/models?pageSize=100" + (pageToken == null ? "" : "&pageToken=" + Uri.EscapeDataString(pageToken))
            : Origin(provider) + "/models");
    }
    static HttpRequestMessage Authorized(string provider, string key, HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url) { Version = new Version(2, 0), VersionPolicy = HttpVersionPolicy.RequestVersionOrLower };
        if (provider == "Gemini") request.Headers.Add("x-goog-api-key", key);
        else if (provider == "Claude") { request.Headers.Add("x-api-key", key); request.Headers.Add("anthropic-version", "2023-06-01"); }
        else request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return request;
    }
    static void Validate(string provider, string model)
    {
        if (!Providers.Contains(provider)) throw new ArgumentException("Choose an AI service in Settings.");
        if (!Regex.IsMatch(model, provider == "OpenRouter" ? "^[a-zA-Z0-9][a-zA-Z0-9._:/-]{0,159}$" : "^[a-zA-Z0-9][a-zA-Z0-9._:-]{0,119}$")) throw new ArgumentException("Choose a valid model in Settings.");
    }
    static string FileText(AssistantAttachment file) => "\n\nAttached file: " + file.Name + "\n<attached_content>\n" + file.Text + "\n</attached_content>";
    static IEnumerable<AssistantAttachment> MediaParts(AssistantAttachment file,string provider)
    {
        if(file.MimeType == "image/gif" || file.MimeType.StartsWith("video/") && provider != "Gemini")
        {
            if(file.Frames.Count==0) throw new InvalidOperationException("This media needs visual frames. Remove the attachment and add it again.");
            yield return new(file.Name+" · visual storyboard","text/plain","",$"{file.Name}: {file.Frames.Count} sampled visual frames in chronological order. These are samples, not the complete motion; audio is not included.",0);
            foreach(var frame in file.Frames) { yield return new($"{file.Name} at {frame.Seconds:0.##}s","text/plain","",$"Visual frame at {frame.Seconds:0.##} seconds:",0); yield return new(file.Name,frame.MimeType,frame.Data,null,frame.Data.Length*3L/4); }
        }
        else yield return file;
    }
    static object[] GeminiParts(AssistantTurn turn)
    {
        var parts = new List<object> { new { text = turn.Content } };
        foreach (var file in turn.Attachments ?? []) parts.Add(file.IsText ? new { text = FileText(file) } : (object)new { inline_data = new { mime_type = file.MimeType, data = file.Data } });
        return parts.ToArray();
    }
    static object[] ClaudeParts(AssistantTurn turn)
    {
        var parts = new List<object> { new { type = "text", text = turn.Content } };
        foreach (var file in turn.Attachments ?? []) parts.Add(file.IsText ? new { type = "text", text = FileText(file) } : (object)new { type = file.MimeType.StartsWith("image/") ? "image" : "document", source = new { type = "base64", media_type = file.MimeType, data = file.Data } });
        return parts.ToArray();
    }
    static object ChatParts(AssistantTurn turn)
    {
        if (turn.Attachments == null || turn.Attachments.Count == 0) return turn.Content;
        var parts = new List<object> { new { type = "text", text = turn.Content } };
        foreach (var file in turn.Attachments)
        {
            if (file.IsText) parts.Add(new { type = "text", text = FileText(file) });
            else if (file.MimeType.StartsWith("image/")) parts.Add(new { type = "image_url", image_url = new { url = "data:" + file.MimeType + ";base64," + file.Data } });
            else parts.Add(new { type = "file", file = new { filename = file.Name, file_data = "data:" + file.MimeType + ";base64," + file.Data } });
        }
        return parts.ToArray();
    }
    public static string ParseDelta(string provider, string data)
    {
        if (data == "[DONE]") return "";
        using var doc = JsonDocument.Parse(data);
        var root = doc.RootElement;
        if (root.TryGetProperty("error", out _)) throw new InvalidOperationException("The service interrupted this response. Try again.");
        if (provider == "Claude") return root.TryGetProperty("delta", out var claudeDelta) && claudeDelta.TryGetProperty("type", out var type) && type.GetString() == "text_delta" && claudeDelta.TryGetProperty("text", out var claudeText) ? claudeText.GetString() ?? "" : "";
        if (provider == "Gemini")
        {
            if (root.TryGetProperty("promptFeedback", out var feedback) && feedback.TryGetProperty("blockReason", out _))
                throw new InvalidOperationException("This request was blocked by the service. Rephrase your message.");
            if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0) return "";
            var candidate = candidates[0];
            if (candidate.TryGetProperty("finishReason", out var reason) && reason.GetString() is "SAFETY" or "RECITATION" or "PROHIBITED_CONTENT")
                throw new InvalidOperationException("This response was blocked by the service. Rephrase your message.");
            if (!candidate.TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts)) return "";
            return string.Concat(parts.EnumerateArray().Where(p => !p.TryGetProperty("thought", out var thought) || thought.ValueKind != JsonValueKind.True)
                .Select(p => p.TryGetProperty("text", out var text) ? text.GetString() : ""));
        }
        if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0) return "";
        var choice = choices[0];
        if (choice.TryGetProperty("finish_reason", out var finish) && finish.GetString() == "content_filter")
            throw new InvalidOperationException("This response was blocked by the service. Rephrase your message.");
        if (!choice.TryGetProperty("delta", out var delta)) return "";
        return delta.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() ?? "" : "";
    }
    public static async IAsyncEnumerable<string> ReadEventsAsync(Stream stream, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var data = new StringBuilder();
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Length > 1000000) throw new InvalidOperationException("The service returned an oversized response.");
            if (line.Length == 0)
            {
                if (data.Length > 0) { yield return data.ToString(); data.Clear(); }
            }
            else if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                if (data.Length > 0) data.Append('\n');
                data.Append(line.AsSpan(5).TrimStart());
                if (data.Length > 1000000) throw new InvalidOperationException("The service returned an oversized response.");
            }
        }
        if (data.Length > 0) yield return data.ToString();
    }
    public static string? FinishReason(string provider, string data)
    {
        if (data == "[DONE]") return "stop";
        using var doc = JsonDocument.Parse(data);
        if (provider == "Claude") return doc.RootElement.TryGetProperty("delta", out var delta) && delta.TryGetProperty("stop_reason", out var stop) && stop.ValueKind == JsonValueKind.String ? stop.GetString() : null;
        if (!doc.RootElement.TryGetProperty(provider == "Gemini" ? "candidates" : "choices", out var choices) || choices.GetArrayLength() == 0) return null;
        return choices[0].TryGetProperty(provider == "Gemini" ? "finishReason" : "finish_reason", out var reason) && reason.ValueKind == JsonValueKind.String ? reason.GetString() : null;
    }
    public static string ErrorMessage(int status) => status switch
    {
        400 => "The service rejected this request. Check the model and API key in Settings.",
        401 or 403 => "The API key was rejected or has no access. Check its permissions and selected service in Settings.",
        404 => "This model is unavailable. Use Check connection in Settings to load available models.",
        429 => "The service quota or rate limit was reached. Check your API plan or try again later.",
        >= 500 => "The AI service is temporarily unavailable. Try again shortly.",
        _ => $"The AI service returned HTTP {status}. Check the connection in Settings."
    };
}
