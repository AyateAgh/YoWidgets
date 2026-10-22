namespace YoWidgets.Core;

public sealed record AssistantPlacement(RectD Bounds, bool Up);
public static class AssistantLayout
{
    public const double CompactHeight = 72;
    public const double EditorHeight = 160;
    public const double ResponseHeight = 540;
    public static AssistantPlacement Expand(RectD anchor, double height, RectD work)
    {
        height = Math.Clamp(height, 1, work.Height);
        var below = work.Y + work.Height - anchor.Y;
        var above = anchor.Y + anchor.Height - work.Y;
        var up = below < height && (above >= height || above > below);
        var y = up ? anchor.Y + anchor.Height - height : anchor.Y;
        var clamped = Placement.Clamp(anchor.X, y, anchor.Width, height, work);
        return new(new(clamped.X, clamped.Y, Math.Min(anchor.Width, work.Width), height), up);
    }
}
