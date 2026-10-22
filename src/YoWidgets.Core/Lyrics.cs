using System.Globalization;
using System.Text.RegularExpressions;
namespace YoWidgets.Core;

public static partial class Lyrics
{
    public static int CurrentIndex(IReadOnlyList<LyricLine> lines, double seconds)
    {
        if (!double.IsFinite(seconds)) return -1;
        int low = 0, high = lines.Count - 1, found = -1;
        while (low <= high) { int mid = (low + high) / 2; if (lines[mid].Seconds <= seconds) { found = mid; low = mid + 1; } else high = mid - 1; }
        return found;
    }
    [GeneratedRegex(@"\[(\d+):(\d+(?:\.\d+)?)\]")] private static partial Regex Timestamp();
    [GeneratedRegex(@"\[offset:([+-]?\d+)\]", RegexOptions.IgnoreCase)] private static partial Regex Offset();
    [GeneratedRegex(@"<\d+:\d+(?:\.\d+)?>")] private static partial Regex WordTimestamp();
    public static List<LyricLine> Parse(string text)
    {
        var result = new List<LyricLine>();
        var offsetMatch = Offset().Match(text);
        double offset = offsetMatch.Success && double.TryParse(offsetMatch.Groups[1].Value, CultureInfo.InvariantCulture, out var milliseconds) ? milliseconds / 1000 : 0;
        foreach (var line in text.Split('\n'))
        {
            var matches = Timestamp().Matches(line);
            var content = WordTimestamp().Replace(Timestamp().Replace(line, ""), "").Trim();
            foreach (Match m in matches)
                if (double.TryParse(m.Groups[1].Value, CultureInfo.InvariantCulture, out var min) && double.TryParse(m.Groups[2].Value, CultureInfo.InvariantCulture, out var sec))
                    result.Add(new(Math.Max(0, min * 60 + sec - offset), content));
        }
        return result.OrderBy(x => x.Seconds).ToList();
    }
}
public sealed class WorkGate
{
    private long version; private bool suspended;
    public long Version => Interlocked.Read(ref version); public bool Suspended => Volatile.Read(ref suspended);
    public bool CanApply(long value) => !Suspended && value == Version;
    public void Suspend()
    {
        Volatile.Write(ref suspended, true);
        Interlocked.Increment(ref version);
    }
    public void Resume()
    {
        Interlocked.Increment(ref version);
        Volatile.Write(ref suspended, false);
    }
}
