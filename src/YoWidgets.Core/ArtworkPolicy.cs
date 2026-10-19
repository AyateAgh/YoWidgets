namespace YoWidgets.Core;

public static class ArtworkPolicy
{
    public static string? WebsiteIcon(params string?[] metadata)
    {
        var text = string.Join(" ", metadata).ToLowerInvariant();
        if (text.Contains("youtube") || text.Contains("youtu.be"))
            return "https://www.youtube.com/favicon.ico";
        if (text.Contains("tiktok"))
            return "https://www.tiktok.com/favicon.ico";
        if (text.Contains("spotify"))
            return "https://open.spotify.com/favicon.ico";
        if (text.Contains("soundcloud"))
            return "https://soundcloud.com/favicon.ico";
        if (text.Contains("twitch"))
            return "https://www.twitch.tv/favicon.ico";
        if (text.Contains("music.apple.com") || text.Contains("apple music"))
            return "https://music.apple.com/favicon.ico";
        if (text.Contains("music.yandex") || text.Contains("яндекс музыка"))
            return "https://music.yandex.ru/favicon.ico";
        return null;
    }
}
