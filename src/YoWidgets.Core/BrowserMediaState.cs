using System.Text;
namespace YoWidgets.Core;

public sealed record BrowserMediaState(string Id, string Provider, string Title, string Artist, double Duration, bool Playing, bool CanShuffle, bool CanRepeat, bool? Shuffle, int? Repeat);
public static class BrowserMediaPolicy
{
    public static string TitleKey(string title) => string.Concat(title.Normalize(NormalizationForm.FormKC).Where(char.IsLetterOrDigit)).ToLowerInvariant();
    public static BrowserMediaState? Match(string source, string title, double duration, bool playing, IEnumerable<BrowserMediaState> states)
    {
        if (!new[] { "chrome", "edge" }.Any(name => source.Contains(name, StringComparison.OrdinalIgnoreCase)) || !double.IsFinite(duration) || duration <= 0 || string.IsNullOrWhiteSpace(title)) return null;
        var key = TitleKey(title);
        var candidates = states.Where(s => s.Provider == "soundcloud" && s.Playing == playing && double.IsFinite(s.Duration) && s.Duration > 0 && Math.Abs(s.Duration - duration) <= 2 && TitleKey(s.Title) == key).Take(2).ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }
}
