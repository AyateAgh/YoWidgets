namespace YoWidgets.Core;

public record SystemChange(string Kind, string Device, double Original, double Last, string Stage = "applied", double? Pending = null);

public static class ChangeJournal
{
    public static SystemChange Advance(SystemChange? previous, string kind, string device, double original, double value, string stage)
    {
        if (stage == "prepared")
        {
            // A pending attempt must never replace the last confirmed baseline/value.
            if (previous?.Stage == "applied")
                return previous with
                {
                    Pending = value
                };
            return new(kind, device, original, value, "prepared", value);
        }
        if (stage != "applied")
            throw new ArgumentOutOfRangeException(nameof(stage));
        return new(kind, device, previous?.Original ?? original, value);
    }
}
