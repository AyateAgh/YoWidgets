using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
namespace YoWidgets;

internal sealed class App : System.Windows.Application
{
    // Explicit diagnostic launch only: lets native accessibility tools select each widget.
    internal static bool InspectWidgetWindows => Environment.GetCommandLineArgs().Contains("--inspect-widget-windows");
    public static string DataRoot { get; private set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YoWidgets");
    internal static string OwnedDirectory(string name)
    {
        if(name is not ("Assistant" or "assistant-cache" or "browser-profile" or "integrations" or "Cache"))throw new ArgumentException("Unknown application directory.");
        var root=Path.GetFullPath(DataRoot);var path=Path.Combine(root,name);var marker=Path.Combine(path,".yowidgets-owned.json");
        if(Directory.Exists(path)&&((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0||!File.Exists(marker)&&Directory.EnumerateFileSystemEntries(path).Any()))throw new IOException("The application cache location contains unrecognized files: "+path);
        Directory.CreateDirectory(path);if(File.Exists(marker)) {try {using var record=System.Text.Json.JsonDocument.Parse(File.ReadAllText(marker));if(record.RootElement.GetProperty("Product").GetString()!="YoWidgets.Native.v1"||!string.Equals(record.RootElement.GetProperty("Path").GetString(),path,StringComparison.OrdinalIgnoreCase))throw new IOException("Application data ownership does not match.");}catch(Exception e) when(e is System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException){throw new IOException("Application data ownership is damaged.",e);}}
        else File.WriteAllText(marker,System.Text.Json.JsonSerializer.Serialize(new {Product="YoWidgets.Native.v1",Path=path}));return path;
    }
    public static App CurrentApp => (App)Current; public WidgetManager Manager { get; private set; } = null!; internal ShellController? Shell
    {
        get; private set;
    }
    System.Windows.Forms.NotifyIcon? tray; Mutex? mutex;
    WidgetIpcServer? widgetIpc;
    internal DesktopPlatform Platform {get;private set;}=null!;
    ManagementServer? management;
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--data-dir"))
        {
            var i = Array.IndexOf(args, "--data-dir");
            if (i + 1 < args.Length)
                DataRoot = Path.GetFullPath(args[i + 1]);
        }
        Directory.CreateDirectory(DataRoot);
        if(args.Contains("--shell-host"))
        {
            var host=new App{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            host.Startup+=(_,_)=>{Ui.Theme();new ShellWindow().Show();};
            host.DispatcherUnhandledException+=(_,e)=>{Log("shell",e.Exception);e.Handled=true;};
            host.Run();return;
        }
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) => { Log("ui", e.Exception); e.Handled = true; app.Shell?.Status("An interface error was logged. You can restart YoWidgets."); };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log("fatal", e.ExceptionObject as Exception ?? new Exception("Unknown failure"));
        app.Startup += (_, _) => app.Start(args);
        app.Run();
    }
    void Start(string[] args)
    {
        mutex = new Mutex(true, "Local\\YoWidgets-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(DataRoot.ToUpperInvariant())))[..20], out var first);
        if (!first)
        {
            if(args.Contains("--widgets-only")){Shutdown();return;}
            _=OpenExisting(args);
            return;
        }
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.DefaultThreadCurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.DefaultThreadCurrentCulture;
        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
        Ui.Theme();
        BundleCache.Register();
        var legacy=File.Exists(Path.Combine(DataRoot,"settings.json"))&&!File.Exists(Path.Combine(DataRoot,"Content","installed.json"));
        Manager = new WidgetManager(DataRoot, args.Contains("--no-desktop"));
        var feedIndex=Array.IndexOf(args,"--catalog-url");
        Platform=new DesktopPlatform(Manager,DataRoot,legacy,feedIndex>=0&&feedIndex+1<args.Length?args[feedIndex+1]:null);
        Shell=new ShellController(Platform);management=new ManagementServer(Platform,Shell,DataRoot,Dispatcher);
        widgetIpc=new(Manager.Commands,Dispatcher,DataRoot);
        tray = new System.Windows.Forms.NotifyIcon { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application, Text = "YoWidgets", Visible = true };
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Font = new System.Drawing.Font("Segoe UI Variable Text", 10, System.Drawing.FontStyle.Regular);
        menu.Items.Add("Open YoWidgets", null, (_, _) => Dispatcher.Invoke(ShowShell));
        menu.Items.Add("Snooze / resume", null, (_, _) => Dispatcher.Invoke(() => Manager.SetSnooze(!Manager.Snoozed)));
        menu.Items.Add("Center widgets on this screen", null, (_, _) => Dispatcher.Invoke(Manager.ResetPositions));
        menu.Items.Add("Disable all", null, (_, _) => Dispatcher.Invoke(Manager.DisableAll));
        menu.Items.Add("Quit", null, (_, _) => Dispatcher.Invoke(ExitApp));
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowShell);
        if (!args.Contains("--widgets-only"))
            ShowShell();
        if(args.Contains("--settings"))Shell?.Select("settings");
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => { Manager.RevealWidgets(); ReleaseUnusedImages(); });
        if (args.Contains("--optics-test"))
            Manager.ShowOpticsTest();
        Trace("startup", $"Version 0.6.2; WPF tier {RenderCapability.Tier >> 16}; OS {Environment.OSVersion.Version}");
    }
    public void ShowShell()
    {
        Shell?.Show();
    }
    async Task OpenExisting(string[] args)
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(6));
        try{await ManagementClient.Request(DataRoot,"app.open",new{settings=args.Contains("--settings")},timeout.Token);}
        catch(Exception e)when(e is IOException or TimeoutException or OperationCanceledException){Log("open-existing",e);}
        finally{Shutdown();}
    }
    public void ReleaseUnusedImages()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
        {
            Trace("memory", $"managed={GC.GetTotalMemory(false)}; wallpaperBitmaps=0; workingSet={Environment.WorkingSet}");
        });
    }
    public void ExitApp()
    {
        Shell?.Dispose();management?.Dispose();Platform?.Dispose();InterfaceAudio.Stop();
        widgetIpc?.Dispose();
        Manager.Dispose();
        tray?.Dispose();
        mutex?.Dispose();
        Shutdown();
    }
    public static void Log(string area, Exception e)
    {
        try
        {
            Directory.CreateDirectory(DataRoot);
            var file = Path.Combine(DataRoot, "app.log");
            if (File.Exists(file) && new FileInfo(file).Length > 512_000)
                File.Move(file, file + ".old", true);
            File.AppendAllText(file, $"{DateTimeOffset.Now:o} {area}: {e.GetType().Name}: {e.Message}\n");
        }
        catch { }
    }
    public static void Trace(string area, string message)
    {
        try
        {
            File.AppendAllText(Path.Combine(DataRoot, "app.log"), $"{DateTimeOffset.Now:o} {area}: {message}\n");
        }
        catch { }
    }
}
