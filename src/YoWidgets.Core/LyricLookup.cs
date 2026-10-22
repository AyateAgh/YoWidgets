using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace YoWidgets.Core;

public sealed record LyricTrack(string Title, string Artist, double Duration)
{
    public string BaseTitle => Regex.Replace(Title, @"\s*[\[(][^\])]*(?:slowed|sped\s*up|reverb|nightcore|official\s*(?:audio|video)|lyrics?|visualizer)[^\])]*[\])]", "", RegexOptions.IgnoreCase).Trim();
    public string PrimaryArtist => Regex.Split(Regex.Split(Artist, @"\s+/\s+").Last(), @"\s*(?:,|&|\sfeat\.?\s|\sft\.?\s)\s*", RegexOptions.IgnoreCase)[0].Trim();
    public bool Composite => Artist.Contains(" / ", StringComparison.Ordinal);
    public string Key => Title + "|" + Artist + "|" + Math.Round(Duration);
    public static LyricTrack FromMetadata(string title, string artist, double duration, bool browser)
    {
        title = Regex.Replace(title, @"\s*(?:\||[-–—])\s*(?:SoundCloud|Spotify|YouTube|Apple Music)\s*$", "", RegexOptions.IgnoreCase).Trim();
        if (browser)
        {
            var parts = Regex.Split(title, @"\s+[-–—]\s+", RegexOptions.None, TimeSpan.FromMilliseconds(100));
            if (parts.Length >= 2 && parts[0].Length is > 0 and < 120) { artist = parts[0].Trim(); title = string.Join(" - ", parts.Skip(1)).Trim(); }
        }
        return new(title.Trim(), artist.Trim(), double.IsFinite(duration) && duration > 0 ? duration : 0);
    }
}
public sealed record LyricResult(string Plain, IReadOnlyList<LyricLine> Lines, bool Instrumental = false)
{
    public static readonly LyricResult Empty = new("", Array.Empty<LyricLine>());
    public bool Available => Plain.Length > 0 || Lines.Count > 0;
    public bool Synced => Lines.Count > 0;
}
public static class LyricLookup
{
    static string Canonical(string value) => string.Concat(value.Normalize().Where(char.IsLetterOrDigit)).ToLowerInvariant();
    public static LyricResult Match(LyricTrack track, string json)
    {
        using var document = JsonDocument.Parse(json);
        var entries = document.RootElement.ValueKind == JsonValueKind.Array ? document.RootElement.EnumerateArray().Take(100).ToArray() : [document.RootElement];
        static string Text(JsonElement item, string name) => item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
        var matches = new List<(LyricResult Result, double Rank)>();
        foreach (var entry in entries)
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;
            var title = Text(entry, "trackName");
            var artist = Text(entry, "artistName");
            var candidate = new LyricTrack(title, artist, 0);
            if (Canonical(candidate.BaseTitle) != Canonical(track.BaseTitle) || string.IsNullOrWhiteSpace(track.PrimaryArtist)) continue;
            var artists = Regex.Split(artist, @"\s*(?:,|&|\sfeat\.?\s|\sft\.?\s)\s*", RegexOptions.IgnoreCase);
            if (!artists.Any(a => Canonical(a) == Canonical(track.PrimaryArtist)) && Canonical(artist) != Canonical(track.Artist)) continue;
            double duration = entry.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number && d.TryGetDouble(out var seconds) ? seconds : 0;
            var gap = Math.Abs(duration - track.Duration);
            var synced = !track.Composite && track.Duration > 0 && duration > 0 && gap <= 2;
            if (entry.TryGetProperty("instrumental", out var instrumental) && instrumental.ValueKind == JsonValueKind.True)
            { if (synced) matches.Add((new("", Array.Empty<LyricLine>(), true), 2000 - gap)); continue; }
            var lines = Lyrics.Parse(Text(entry, "syncedLyrics"));
            var plain = Text(entry, "plainLyrics").Trim();
            if (plain.Length == 0 && lines.Count > 0) plain = string.Join("\n", lines.Select(l => l.Text));
            var result = new LyricResult(plain, synced ? lines.AsReadOnly() : Array.Empty<LyricLine>());
            if (result.Available) matches.Add((result, (synced ? 2000 : 0) + (result.Synced ? 10 : 0) - Math.Min(1000, gap)));
        }
        return matches.OrderByDescending(m => m.Rank).Select(m => m.Result).FirstOrDefault() ?? LyricResult.Empty;
    }
}
public sealed class LyricRetryException(DateTimeOffset retryAt) : Exception("Lyrics provider is temporarily unavailable")
{
    public DateTimeOffset RetryAt { get; } = retryAt;
}
public sealed class LyricClient(HttpClient http, TimeSpan? requestGap = null) : IDisposable
{
    readonly SemaphoreSlim serial = new(1);
    readonly Dictionary<string, (LyricResult Result, DateTimeOffset Expires)> cache = [];
    volatile bool disposed;
    DateTimeOffset nextRequest;
    readonly TimeSpan gap = requestGap ?? TimeSpan.FromMilliseconds(250);
    public async Task<LyricResult> FindAsync(LyricTrack track, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(disposed, this);
        await serial.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (cache.TryGetValue(track.Key, out var hit) && hit.Expires > DateTimeOffset.UtcNow) return hit.Result;
            var wait = nextRequest - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.FromSeconds(1)) throw new LyricRetryException(nextRequest);
            if (string.IsNullOrWhiteSpace(track.BaseTitle) || string.IsNullOrWhiteSpace(track.Artist)) return LyricResult.Empty;
            var queries = new List<string> { "get?track_name=" + E(track.Title) + "&artist_name=" + E(track.Artist) + (track.Duration is >= 1 and <= 3600 ? "&duration=" + track.Duration.ToString("0.###", CultureInfo.InvariantCulture) : "") };
            queries.Add("search?track_name=" + E(track.BaseTitle) + "&artist_name=" + E(track.Artist));
            if (track.PrimaryArtist != track.Artist) queries.Add("search?track_name=" + E(track.BaseTitle) + "&artist_name=" + E(track.PrimaryArtist));
            if (track.Composite) queries = ["search?track_name=" + E(track.BaseTitle) + "&artist_name=" + E(track.PrimaryArtist)];
            var result = LyricResult.Empty;
            foreach (var query in queries)
            {
                wait = nextRequest - DateTimeOffset.UtcNow;
                if (wait > TimeSpan.Zero) await Task.Delay(wait, ct).ConfigureAwait(false);
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://lrclib.net/api/" + query);
                request.Headers.UserAgent.ParseAdd("YoWidgets/0.5.2 (Windows-desktop-lyrics-client)");
                using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
                nextRequest = DateTimeOffset.UtcNow + gap;
                if (response.StatusCode == (HttpStatusCode)429 || (!response.IsSuccessStatusCode && response.Headers.RetryAfter != null))
                {
                    nextRequest = response.Headers.RetryAfter?.Date ?? DateTimeOffset.UtcNow + (response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(2));
                    if (nextRequest <= DateTimeOffset.UtcNow) nextRequest = DateTimeOffset.UtcNow.AddSeconds(30);
                    throw new LyricRetryException(nextRequest);
                }
                if (response.StatusCode == HttpStatusCode.NotFound) continue;
                // /get may depend on an external fetcher while /search still serves its index.
                // Fall back once rather than retrying the failed fetch endpoint.
                if ((int)response.StatusCode >= 500 && query.StartsWith("get?", StringComparison.Ordinal)) continue;
                response.EnsureSuccessStatusCode();
                var match = LyricLookup.Match(track, await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
                if (match.Instrumental) { result = match; break; }
                if (match.Synced) { result = match; break; }
                if (match.Available) result = match;
            }
            ct.ThrowIfCancellationRequested();
            if (cache.Count >= 12) cache.Remove(cache.Keys.First());
            cache[track.Key] = (result, DateTimeOffset.UtcNow.AddMinutes(result.Available ? 120 : 30));
            return result;
        }
        finally { serial.Release(); }
    }
    static string E(string text) => Uri.EscapeDataString(text);
    // A cancelled HTTP continuation may still own the gate when its UI owner closes.
    // SemaphoreSlim has no allocated wait handle here; let GC reclaim it after that continuation.
    public void Dispose() { disposed = true; }
}
