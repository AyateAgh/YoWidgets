namespace YoWidgets.Core;

public sealed record MediaTimeline(double Position, double Start, double End, DateTimeOffset Updated);

// Source timestamps establish the position once; a monotonic clock advances it.
// Re-reading unchanged browser snapshots must not restart time at their old position.
public sealed class MediaClock
{
    MediaTimeline? snapshot;
    double anchor, anchorTime, rate = 1;
    bool playing;
    public double Duration { get; private set; }
    public double Start { get; private set; }
    public double Position(double monotonic) => Math.Clamp(anchor + (playing ? Math.Max(0, monotonic - anchorTime) * rate : 0), 0, Duration);
    public void Apply(MediaTimeline next, bool isPlaying, double? playbackRate, DateTimeOffset now, double monotonic)
    {
        var current = Position(monotonic);
        var nextRate = playbackRate is double r && double.IsFinite(r) && r >= 0 && r <= 16 ? r : 1;
        Duration = Math.Max(0, next.End - next.Start);
        Start = next.Start;
        if (snapshot != next)
        {
            var age = next.Updated > DateTimeOffset.UnixEpoch && next.Updated <= now ? Math.Max(0, (now - next.Updated).TotalSeconds) : 0;
            anchor = Math.Clamp(next.Position - next.Start + (isPlaying ? age * nextRate : 0), 0, Duration);
        }
        else
            anchor = Math.Clamp(current, 0, Duration);
        snapshot = next;
        anchorTime = monotonic;
        rate = nextRate;
        playing = isPlaying;
    }
    public void Seek(double value, double monotonic) { anchor = Math.Clamp(value, 0, Duration); anchorTime = monotonic; }
    public void Reset() { snapshot = null; anchor = Duration = Start = 0; playing = false; }
}
