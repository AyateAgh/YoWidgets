namespace YoWidgets.Core;

public static class ContrastPolicy
{
    public static double Luminance(byte red, byte green, byte blue)
    {
        double Linear(byte channel) { var value = channel / 255d; return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4); }
        return .2126 * Linear(red) + .7152 * Linear(green) + .0722 * Linear(blue);
    }
    public static double MergeMargins(IEnumerable<double> samples) => samples.Where(double.IsFinite).DefaultIfEmpty(0).Min();
    // Hysteresis prevents text oscillating over a moving, mid-tone background.
    public static bool DarkInk(double luminance, bool? previous) => double.IsFinite(luminance) && luminance > (previous == true ? .16 : previous == false ? .28 : .22);
}
