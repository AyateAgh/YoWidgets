namespace YoWidgets.Core;

public static class RollbackPolicy
{
    public static bool CanRestore(string originalDevice, string currentDevice, double current, double last) => !string.IsNullOrWhiteSpace(originalDevice) && string.Equals(originalDevice, currentDevice, StringComparison.Ordinal) && double.IsFinite(current) && double.IsFinite(last) && Math.Abs(current - last) <= 1;
}
