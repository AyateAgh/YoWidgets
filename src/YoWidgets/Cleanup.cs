namespace YoWidgets;

internal static class Cleanup
{
    public static string InstallRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "YoWidgets");
    public static bool IsInstalled => string.Equals(Path.GetDirectoryName(Environment.ProcessPath), InstallRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(InstallRoot, "installation.json")) && File.Exists(Path.Combine(InstallRoot, "uninstall.ps1"));
    public static void LaunchUninstall()
    {
        if (!IsInstalled)
            return;
        var script = Path.Combine(InstallRoot, "uninstall.ps1");
        var start = new System.Diagnostics.ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in new[] { "-NoProfile", "-File", script, "-ParentId", Environment.ProcessId.ToString(), "-ShowResult" })
            start.ArgumentList.Add(a);
        System.Diagnostics.Process.Start(start);
        App.CurrentApp.ExitApp();
    }
}
