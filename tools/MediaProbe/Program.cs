using Windows.Media.Control;
using System.Text.Json;
using YoWidgets.Core;
if (args.Length == 4 && args[0] == "--lookup")
{
    var track = LyricTrack.FromMetadata(args[1], args[2], double.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture), true);
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
    using var client = new LyricClient(http);
    var result = await client.FindAsync(track, default);
    Console.WriteLine(JsonSerializer.Serialize(new { track.Title, track.Artist, result.Available, result.Synced, Lines = result.Lines.Count, PlainCharacters = result.Plain.Length }));
    return;
}
var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
foreach (var session in manager.GetSessions())
{
    var metadata = await session.TryGetMediaPropertiesAsync();
    var info = session.GetPlaybackInfo();
    var timeline = session.GetTimelineProperties();
    Console.WriteLine(JsonSerializer.Serialize(new {
        Source = session.SourceAppUserModelId, metadata.Title, metadata.Artist, metadata.Subtitle,
        metadata.AlbumTitle, Type = info.PlaybackType?.ToString(), State = info.PlaybackStatus.ToString(),
        Shuffle = info.Controls.IsShuffleEnabled, Repeat = info.Controls.IsRepeatEnabled,
        info.IsShuffleActive, RepeatMode = info.AutoRepeatMode?.ToString(),
        Next = info.Controls.IsNextEnabled, Previous = info.Controls.IsPreviousEnabled,
        Position = timeline.Position.TotalSeconds, Duration = timeline.EndTime.TotalSeconds - timeline.StartTime.TotalSeconds
    }));
    if (args.Contains("--lyrics") && MediaCapabilities.IsMusic(info.PlaybackType == Windows.Media.MediaPlaybackType.Music, info.PlaybackType == Windows.Media.MediaPlaybackType.Video, session.SourceAppUserModelId, metadata.Title, metadata.Artist, metadata.Subtitle))
    {
        var browser = new[] { "chrome", "edge", "firefox" }.Any(name => session.SourceAppUserModelId.Contains(name, StringComparison.OrdinalIgnoreCase));
        var track = LyricTrack.FromMetadata(metadata.Title, metadata.Artist, timeline.EndTime.TotalSeconds - timeline.StartTime.TotalSeconds, browser);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
        using var lyrics = new LyricClient(http);
        var result = await lyrics.FindAsync(track, default);
        Console.WriteLine(JsonSerializer.Serialize(new { LookupTitle = track.Title, LookupArtist = track.Artist, result.Available, result.Synced, Lines = result.Lines.Count, PlainCharacters = result.Plain.Length }));
    }
}
