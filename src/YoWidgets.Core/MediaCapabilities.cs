namespace YoWidgets.Core;

public static class MediaCapabilities
{
    static bool NativeMusic(string? source) => new[] { "spotify", "soundcloud", "itunes", "applemusic", "apple music" }.Any(name => (source ?? "").Contains(name, StringComparison.OrdinalIgnoreCase));
    public static bool IsMusic(bool music, bool video, params string?[] metadata) => !video && !VideoSite(metadata) && (music || NativeMusic(metadata.FirstOrDefault()));
    public static (bool Shuffle, bool Repeat) QueueControls(bool active, bool video, bool shuffle, bool repeat, params string?[] metadata)
    {
        return !active || video || VideoSite(metadata) ? (false, false) : (shuffle, repeat);
    }
    static bool VideoSite(string?[] metadata)
    {
        var source = metadata.FirstOrDefault() ?? "";
        bool Site(string? value)
        {
            var text = (value ?? "").Trim();
            return text.Equals("YouTube", StringComparison.OrdinalIgnoreCase) || text.Equals("TikTok", StringComparison.OrdinalIgnoreCase) ||
                text.EndsWith("| YouTube", StringComparison.OrdinalIgnoreCase) || text.EndsWith("- YouTube", StringComparison.OrdinalIgnoreCase) ||
                text.EndsWith("| TikTok", StringComparison.OrdinalIgnoreCase) || text.EndsWith("- TikTok", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("youtube.com/", StringComparison.OrdinalIgnoreCase) || text.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase) || text.Contains("tiktok.com/", StringComparison.OrdinalIgnoreCase);
        }
        return source.Contains("youtube", StringComparison.OrdinalIgnoreCase) || source.Contains("tiktok", StringComparison.OrdinalIgnoreCase) || (!NativeMusic(source) && metadata.Skip(1).Any(Site));
    }
}
