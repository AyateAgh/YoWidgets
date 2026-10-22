namespace YoWidgets;

internal sealed class WidgetManager : IDisposable
{
    public static readonly (string Kind, string Title, string Detail, string Icon)[] Catalog = [("clock", "Clock", "Time at your pace", "clock"), ("weather", "Weather", "Outside, and through the day", "sun"), ("calendar", "Calendar", "Today, and the month ahead", "calendar-days"), ("music", "Music", "Your music, within reach", "music"), ("photo", "Photos", "A window into your favorites", "image"), ("focus", "Focus", "Less noise. More focus.", "moon"), ("quick", "Controls", "Sound and brightness, at hand", "sliders-horizontal"), ("notifications", "Notifications", "What matters, in one place", "bell"), ("assistant", "Assistant", "Here when you need it", "sparkles")];
    public AppSettings Settings
    {
        get;
    }
    public SettingsStore Store
    {
        get;
    }
    public DataServices Data
    {
        get;
    }
    internal WidgetCommandHub Commands {get;}
    public bool Snoozed
    {
        get; private set;
    }
    public string DesktopStatus { get; set; } = ""; public bool Editing
    {
        get; set;
    }
    public bool ForceNormal
    {
        get;
    }
    public event Action? Changed;
    readonly Dictionary<string, WidgetWindow> windows = []; bool disposed;
    readonly BackgroundContrast contrast;
    public WidgetManager(string root, bool forceNormal)
    {
        ForceNormal = forceNormal;
        Store = new(Path.Combine(root, "settings.json"));
        Settings = Store.Load();
        var content=File.Exists(Path.Combine(root,"Content","installed.json"))?new ContentStore(Path.Combine(root,"Content")):null;
        if(content!=null)WidgetPacks.LoadInstalled(content);
        WidgetVisuals.Tone(false, true);
        Data = new DataServices(this);
        Commands=new(this);
        contrast = new(() => windows.Values.ToArray());
        var migrate = Settings.FloatingPlacementVersion < 1;
        foreach (var s in Settings.Widgets.Where(s => s.Enabled))
        {
            if(content!=null&&!content.IsReady(s.ContentId)){s.Enabled=false;continue;}
            Open(s, migrate);
        }
        if (migrate)
            ResetPositions();
        Settings.FloatingPlacementVersion = 1;
        Save();
        Data.UpdateActivity();
        contrast.Update(windows.Count > 0);
    }
    public int ActiveCount => Settings.Widgets.Count(s => s.Enabled);
    public void Save() => TrySave();
    public bool TrySave()
    {
        try
        {
            Store.Save(Settings);
            return true;
        }
        catch (Exception e) { App.Log("settings-save", e); App.CurrentApp.Shell?.Status("Could not save settings: " + e.Message); return false; }
    }
    public void Toggle(string kind, string pack)
    {
        pack = WidgetPacks.Get(pack).Id;
        var states = Settings.Widgets.Where(s => s.Kind == kind && s.Pack == pack).ToArray();
        if (states.Any(s => s.Enabled))
        {
            foreach (var s in states)
                Disable(s);
        }
        else
        {
            var s = states.FirstOrDefault();
            if (s == null)
            {
                s = new()
                {
                    Kind = kind,
                    Pack = pack,
                    X = 60 + (ActiveCount % 3) * 390,
                    Y = 60 + (ActiveCount / 3) * 220
                };
                Settings.Widgets.Add(s);
            }
            s.Enabled = true;
            Open(s, true);
        }
        Save();
        Changed?.Invoke();
    }
    public void Add(string kind, string pack, RectD? targetArea = null,string? contentId=null,string defaultSize="M")
    {
        var s = new WidgetState { Kind = kind, Pack = WidgetPacks.Get(pack).Id,ContentId=contentId??pack+"-"+kind,Size=defaultSize, Enabled = true, X = 80 + (ActiveCount % 3) * 380, Y = 80 + (ActiveCount / 3) * 220 };
        Settings.Widgets.Add(s);
        Open(s, true, targetArea);
        Save();
        Changed?.Invoke();
    }
    public void DisablePack(string pack)
    {
        foreach (var state in Settings.Widgets.Where(w => w.Enabled && w.Pack == pack).ToArray()) Disable(state);
    }
    void Open(WidgetState s, bool center = false, RectD? targetArea = null)
    {
        if (windows.ContainsKey(s.Id))
            return;
        var w = new WidgetWindow(this, s);
        windows[s.Id] = w;
        w.Show();
        if (center)
        {
            if(targetArea is {} area)w.CenterInWorkArea(area, windows.Count-1);
            else w.CenterOnCurrentMonitor(windows.Count - 1, windows.Count);
        }
        w.Activate();
        if (Snoozed)
            w.Hide();
        contrast.Update(!Snoozed && windows.Count > 0);
    }
    public void ResetPositions()
    {
        if (Snoozed)
            SetSnooze(false);
        var index = 0;
        var dpi = windows.Count == 0 ? 1 : Math.Max(1, Native.GetDpiForWindow(Native.Handle(windows.Values.First())) / 96d);
        var cellWidth = windows.Count == 0 ? 0 : windows.Values.Max(w => w.Width) * dpi;
        var cellHeight = windows.Count == 0 ? 0 : windows.Values.Max(w => w.Height) * dpi;
        foreach (var w in windows.Values)
        {
            w.CenterOnCurrentMonitor(index++, windows.Count, cellWidth, cellHeight);
            w.Show();
            w.Activate();
        }
        Save();
        App.CurrentApp.Shell?.Status("Widgets centered on this screen");
    }
    public void RevealWidgets()
    {
        if (Snoozed)
            return;
        foreach (var window in windows.Values)
        {
            window.Show();
            window.Activate();
        }
    }
    public void Disable(WidgetState s)
    {
        s.Enabled = false;
        if (windows.Remove(s.Id, out var w))
            w.CloseOwned();
        contrast.Update(!Snoozed && windows.Count > 0);
        Save();
        Changed?.Invoke();
    }
    internal void Enable(WidgetState state){if(state.Enabled)return;state.Enabled=true;Open(state);Save();Changed?.Invoke();}
    public void DisableAll()
    {
        Settings.DisableIntegrations();
        foreach (var s in Settings.Widgets.ToArray())
            Disable(s);
        Data.Suspend();
        Snoozed = true;
        contrast.Update(false);
        StartupRegistration.Set(false);
        Save();
        Changed?.Invoke();
    }
    public void SetSnooze(bool snooze)
    {
        if (Snoozed == snooze)
            return;
        Snoozed = snooze;
        contrast.Update(false);
        if (snooze)
        {
            Data.Suspend();
            foreach (var w in windows.Values.ToArray())
                w.CloseOwned();
            windows.Clear();
        }
        else
        {
            Data.Resume();
            foreach (var state in Settings.Widgets.Where(x => x.Enabled))
                Open(state);
        }
        contrast.Update(!snooze && windows.Count > 0);
        Changed?.Invoke();
    }
    public void ApplyAppearance()
    {
        WidgetVisuals.Tone(false, true);
        Settings.Normalize();
        foreach (var w in windows.Values)
        {
            w.UpdateHost();
            w.RefreshGlass();
        }
        Save();
        Changed?.Invoke();
    }
    public void RecoverDesktop()
    {
        foreach (var w in windows.Values.ToArray())
            w.CloseOwned();
        windows.Clear();
        DesktopStatus = "";
        foreach (var s in Settings.Widgets.Where(s => s.Enabled))
            Open(s);
        Changed?.Invoke();
    }
    public void ShowOpticsTest()
    {
        var s = new WidgetState { Kind = "clock", Enabled = false, X = 250, Y = 180, Size = "L" };
        var w = new WidgetWindow(this, s, true);
        w.Show();
    }
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        foreach (var w in windows.Values.ToArray())
            w.CloseOwned();
        windows.Clear();
        Commands.Dispose();
        Data.Dispose();
        contrast.Dispose();
        Save();
    }
}
