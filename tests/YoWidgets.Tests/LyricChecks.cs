using System.Net;
using System.Text;
using YoWidgets.Core;

static class LyricChecks
{
    internal static void Run(Action<string, Action> test)
    {
        void Check(bool value) { if (!value) throw new Exception("Lyrics regression"); }
        test("native music apps expose lyrics without a browser identity", () => {
            Check(MediaCapabilities.IsMusic(true, false, "SpotifyAB.SpotifyMusic", "Track", "Artist"));
            Check(MediaCapabilities.IsMusic(false, false, "iTunes.exe", "Track", "Artist"));
            Check(MediaCapabilities.IsMusic(true, false, "SoundCloud", "Track", "Artist"));
            Check(!MediaCapabilities.IsMusic(true, false, "Chrome", "Video | YouTube"));
            Check(!MediaCapabilities.IsMusic(false, true, "player", "Video"));
        });
        test("lyrics offset and enhanced timestamps do not leak into display text", () => {
            var lines = Lyrics.Parse("[offset:500]\n[00:02.00]<00:02.00>Our <00:02.50>own line");
            Check(lines.Count == 1 && lines[0].Seconds == 1.5 && lines[0].Text == "Our own line");
        });
        test("lyric selection follows seeks, exact boundaries and intro", () => {
            var lines = Lyrics.Parse("[00:05.00]First\n[00:10.00]Second\n[00:10.00]Translation");
            Check(Lyrics.CurrentIndex(lines, 0) == -1 && Lyrics.CurrentIndex(lines, 5) == 0 && Lyrics.CurrentIndex(lines, 10) == 2 && Lyrics.CurrentIndex(lines, 6) == 0);
        });
        test("browser uploader is replaced by artist embedded in track title", () => {
            var track = LyricTrack.FromMetadata("LONOWN, Asenssia - addiction (Slowed)", "serenity", 233.5, true);
            Check(track.Title == "addiction (Slowed)" && track.Artist == "LONOWN, Asenssia" && track.BaseTitle == "addiction" && track.PrimaryArtist == "LONOWN");
            Check(LyricTrack.FromMetadata("Part One - Part Two", "Real Artist", 180, false).Title == "Part One - Part Two");
        });
        test("mashup prefix does not hide the named original artist", () => {
            var track = LyricTrack.FromMetadata("Just Staring / SOPHIE - VYZEE", "nn", 197.834, true);
            Check(track.Title == "VYZEE" && track.PrimaryArtist == "SOPHIE");
        });
        test("composite upload uses the named artist index without inventing karaoke", () => {
            int calls = 0;
            using var http = new HttpClient(new Handler(uri => {
                calls++;
                Check(uri.AbsolutePath.EndsWith("/search") && uri.Query.Contains("artist_name=SOPHIE"));
                return new(HttpStatusCode.OK) { Content = new StringContent("""[{"trackName":"VYZEE","artistName":"SOPHIE","duration":197.834,"syncedLyrics":"[00:04.00]Our words"}]""") };
            }));
            using var client = new LyricClient(http, TimeSpan.Zero);
            var result = client.FindAsync(LyricTrack.FromMetadata("Just Staring / SOPHIE - VYZEE", "nn", 197.834, true), default).GetAwaiter().GetResult();
            Check(result.Available && !result.Synced && calls == 1);
        });
        test("unknown record duration does not discard other valid lyrics", () => {
            var match = LyricLookup.Match(new("Test song", "Test Artist", 234), """[{"trackName":"Test song","artistName":"Test Artist","duration":null,"plainLyrics":"Our words"},{"trackName":"Test song","artistName":"Test Artist","duration":234,"syncedLyrics":"[00:04.00]Our words"}]""");
            Check(match.Synced && match.Lines[0].Seconds == 4);
        });
        const string records = """[{"id":1,"trackName":"Test song","artistName":"Test Artist","duration":200,"plainLyrics":"Our words","syncedLyrics":"[00:01.00]Our words"},{"id":2,"trackName":"Test song","artistName":"Test Artist","duration":234,"plainLyrics":"Our words","syncedLyrics":"[00:04.00]Our words"},{"id":3,"trackName":"Test song","artistName":"Other Artist","duration":234,"syncedLyrics":"[00:00.00]Wrong"}]""";
        test("lyrics search prefers matching duration and never a different artist", () => {
            var match = LyricLookup.Match(new("Test song (Slowed)", "Test Artist", 233.5), records);
            Check(match.Synced && match.Lines[0].Seconds == 4);
            Check(!LyricLookup.Match(new("Unknown song", "Test Artist", 234), records).Available);
            Check(!LyricLookup.Match(new("Test song", "Unknown Artist", 234), records).Available);
        });
        test("different speed keeps readable lyrics without fake synchronization", () => {
            var match = LyricLookup.Match(new("Test song (Slowed)", "Test Artist", 260), records);
            Check(match.Available && !match.Synced && match.Plain == "Our words" && match.Lines.Count == 0);
        });
        test("instrumental and empty records do not advertise a lyrics button", () => {
            Check(!LyricLookup.Match(new("Test", "Artist", 200), """[{"trackName":"Test","artistName":"Artist","duration":200,"instrumental":true}]""").Available);
        });
        test("failed exact lookup falls back to search then caches without repeated traffic", () => {
            var requests = new List<string>();
            using var http = new HttpClient(new Handler(uri => {
                requests.Add(uri.AbsoluteUri);
                return uri.AbsolutePath.EndsWith("/get") ? new(HttpStatusCode.NotFound) : new(HttpStatusCode.OK) { Content = new StringContent(records, Encoding.UTF8, "application/json") };
            }));
            using var client = new LyricClient(http, TimeSpan.Zero);
            var track = new LyricTrack("Test song (Slowed)", "Test Artist", 233.5);
            Check(client.FindAsync(track, default).GetAwaiter().GetResult().Synced);
            Check(client.FindAsync(track, default).GetAwaiter().GetResult().Synced && requests.Count == 2);
            Check(requests[1].Contains("track_name=Test%20song"));
        });
        test("rate limits prevent fallback requests until retry-after expires", () => {
            var calls = 0;
            using var http = new HttpClient(new Handler(_ => { calls++; var r = new HttpResponseMessage((HttpStatusCode)429); r.Headers.RetryAfter = new(TimeSpan.FromMinutes(2)); return r; }));
            using var client = new LyricClient(http, TimeSpan.Zero);
            for (int i = 0; i < 2; i++) { try { client.FindAsync(new("Test", "Artist", 200), default).GetAwaiter().GetResult(); throw new Exception("Expected retry delay"); } catch (LyricRetryException) { } }
            Check(calls == 1);
        });
        test("confirmed instrumental does not search for unrelated words", () => {
            int calls = 0;
            using var http = new HttpClient(new Handler(_ => { calls++; return new(HttpStatusCode.OK) { Content = new StringContent("""{"trackName":"Instrumental","artistName":"Artist","duration":200,"instrumental":true}""") }; }));
            using var client = new LyricClient(http, TimeSpan.Zero);
            Check(!client.FindAsync(new("Instrumental", "Artist", 200), default).GetAwaiter().GetResult().Available && calls == 1);
        });
        test("unavailable exact fetch still allows the read-only search index", () => {
            int calls = 0;
            using var http = new HttpClient(new Handler(uri => { calls++; return uri.AbsolutePath.EndsWith("/get") ? new(HttpStatusCode.ServiceUnavailable) : new(HttpStatusCode.OK) { Content = new StringContent(records) }; }));
            using var client = new LyricClient(http, TimeSpan.Zero);
            Check(client.FindAsync(new("Test song", "Test Artist", 234), default).GetAwaiter().GetResult().Synced && calls == 2);
        });
        test("cancelled lookup never starts a network request", () => {
            var calls = 0;
            using var http = new HttpClient(new Handler(_ => { calls++; return new(HttpStatusCode.OK); }));
            using var client = new LyricClient(http, TimeSpan.Zero);
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            try { client.FindAsync(new("Test", "Artist", 200), cancellation.Token).GetAwaiter().GetResult(); throw new Exception("Expected cancellation"); } catch (OperationCanceledException) { }
            Check(calls == 0);
        });
    }
    sealed class Handler(Func<Uri, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(response(request.RequestUri!));
    }
}
