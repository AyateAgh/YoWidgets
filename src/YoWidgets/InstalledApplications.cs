using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
namespace YoWidgets;

internal sealed record ApplicationLaunch(string Id,string Name,string Executable,string[] Arguments);
internal sealed class InstalledApplications
{
    readonly Dictionary<string,ApplicationLaunch> catalog=[];
    internal InstalledApplications(IEnumerable<ApplicationLaunch>? fixtures=null){if(fixtures!=null)foreach(var app in fixtures)if(AllowedExecutable(app.Executable)&&File.Exists(app.Executable))catalog[app.Id]=app;}
    internal static bool AllowedExecutable(string path)=>Path.IsPathFullyQualified(path)&&Path.GetExtension(path).Equals(".exe",StringComparison.OrdinalIgnoreCase)&&!Blocked.Contains(Path.GetFileNameWithoutExtension(path));
    static readonly HashSet<string> Blocked=new(StringComparer.OrdinalIgnoreCase){"cmd","powershell","pwsh","powershell_ise","wscript","cscript","mshta","rundll32","regsvr32","reg","schtasks","sc","conhost","wt","windowsterminal","openconsole","credentialuibroker","logonui","lockapp","keepass","keepassxc","1password","bitwarden","yowidgets"};
    internal IReadOnlyList<ApplicationLaunch> Find(string query)
    {
        if(catalog.Count==0)Refresh();return catalog.Values.Where(a=>query.Length==0||a.Name.Contains(query,StringComparison.CurrentCultureIgnoreCase)).OrderBy(a=>a.Name).Take(100).ToArray();
    }
    internal ApplicationLaunch Get(string id)=>catalog.TryGetValue(id,out var app)&&AllowedExecutable(app.Executable)&&File.Exists(app.Executable)?app:throw new InvalidOperationException("This installed app is unavailable. Call computer_list_apps again; use its exact app_id, never a guessed path.");
    void Add(string name,string path,params string[] arguments)
    {
        path=Environment.ExpandEnvironmentVariables(path.Trim('"'));if(!AllowedExecutable(path)||!File.Exists(path))return;
        var id=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path.ToUpperInvariant()+string.Join('\n',arguments))))[..20];catalog.TryAdd(id,new(id,name,path,arguments));
    }
    internal void Refresh()
    {
        catalog.Clear();var local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);var roaming=Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        foreach(var path in new[]{Path.Combine(roaming,"Telegram Desktop/Telegram.exe"),Path.Combine(local,"Telegram Desktop/Telegram.exe"),Path.Combine(local,"Programs/Telegram Desktop/Telegram.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Telegram Desktop/Telegram.exe")})Add("Telegram",path);
        foreach(var channel in new[]{"Discord","DiscordCanary","DiscordPTB"}) {
            var folder=Path.Combine(local,channel);if(!Directory.Exists(folder))continue;
            // The updater's fixed argument is required by Discord's normal install.
            Add(channel,Path.Combine(folder,"Update.exe"),"--processStart",channel+".exe");
            if(!catalog.Values.Any(a=>a.Name==channel))foreach(var version in Directory.EnumerateDirectories(folder,"app-*").OrderByDescending(p=>Directory.GetLastWriteTimeUtc(p)).Take(1))Add(channel,Path.Combine(version,channel+".exe"));
        }
        foreach(var hive in new[]{Registry.CurrentUser,Registry.LocalMachine})try {
            using var paths=hive.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths");if(paths==null)continue;
            foreach(var name in paths.GetSubKeyNames().Take(500)){using var entry=paths.OpenSubKey(name);if(entry?.GetValue(null) is string path)Add(Path.GetFileNameWithoutExtension(name),path);}
        }catch(System.Security.SecurityException){}
        // Resolve Start-menu shortcuts as metadata only. Never execute shortcuts,
        // arbitrary arguments, shell commands, scripts or model-supplied paths.
        object? shell=null;
        try {
            var type=Type.GetTypeFromProgID("WScript.Shell");if(type==null)return;shell=Activator.CreateInstance(type);if(shell==null)return;
            foreach(var folder in new[]{Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)}) {
                if(!Directory.Exists(folder))continue;
                foreach(var path in Directory.EnumerateFiles(folder,"*.lnk",SearchOption.AllDirectories).Take(800)) {
                    object? link=null;try {dynamic host=shell;link=host.CreateShortcut(path);dynamic shortcut=link;string target=shortcut.TargetPath;string args=shortcut.Arguments;if(string.IsNullOrWhiteSpace(args))Add(Path.GetFileNameWithoutExtension(path),target);}catch(COMException){}finally {if(link!=null&&Marshal.IsComObject(link))Marshal.FinalReleaseComObject(link);}
                }
            }
        }catch(Exception e) when(e is COMException or IOException or UnauthorizedAccessException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException){}
        finally {if(shell!=null&&Marshal.IsComObject(shell))Marshal.FinalReleaseComObject(shell);}
    }
    internal static Process? Start(ApplicationLaunch app)
    {
        if(!AllowedExecutable(app.Executable)||!File.Exists(app.Executable))throw new InvalidOperationException("The installed app executable is no longer available.");
        var start=new ProcessStartInfo(app.Executable){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(app.Executable)!};foreach(var arg in app.Arguments)start.ArgumentList.Add(arg);return Process.Start(start);
    }
}
