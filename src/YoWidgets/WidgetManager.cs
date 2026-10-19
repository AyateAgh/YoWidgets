namespace YoWidgets;

internal sealed class WidgetManager : IDisposable
{
    public static readonly (string Kind, string Title, string Detail, string Icon)[] Catalog = [("clock", "Часы", "Время в вашем ритме", "◷"), ("weather", "Погода", "За окном и на весь день", "☀"), ("calendar", "Календарь", "Сегодня и весь месяц", "▦"), ("music", "Музыка", "Ваш трек, всегда рядом", "♫"), ("photo", "Фото", "Маленькое окно в любимое", "▧"), ("focus", "Фокус", "Меньше шума. Больше дела.", "☾"), ("quick", "Управление", "Звук и яркость под рукой", "☷"), ("notifications", "Уведомления", "Важное в одном месте", "▤"), ("assistant", "Ассистент", "По запросу, без фона", "✧")];
    public AppSettings Settings
    {
        get;
    }
    public SettingsStore Store
    {
        get;
    }
    public WallpaperProvider Wallpaper { get; } = new(); public DataServices Data
    {
        get;
    }
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
    public WidgetManager(string root, bool forceNormal)
    {
        ForceNormal = forceNormal;
        Store = new(Path.Combine(root, "settings.json"));
        Settings = Store.Load();
        Data = new DataServices(this);
        var migrate = Settings.FloatingPlacementVersion < 1;
        foreach (var s in Settings.Widgets.Where(s => s.Enabled))
            Open(s, migrate);
        if (migrate)
            ResetPositions();
        Settings.FloatingPlacementVersion = 1;
        Save();
        Data.UpdateActivity();
    }
    public int ActiveCount => Settings.Widgets.Count(s => s.Enabled);
    public void Save()
    {
        try
        {
            Store.Save(Settings);
        }
        catch (Exception e) { App.Log("settings-save", e); App.CurrentApp.Shell?.Status("Не удалось сохранить настройки: " + e.Message); }
    }
    public void Toggle(string kind)
    {
        var states = Settings.Widgets.Where(s => s.Kind == kind).ToArray();
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
    public void Add(string kind)
    {
        var s = new WidgetState { Kind = kind, Enabled = true, X = 80 + (ActiveCount % 3) * 380, Y = 80 + (ActiveCount / 3) * 220 };
        Settings.Widgets.Add(s);
        Open(s, true);
        Save();
        Changed?.Invoke();
    }
    void Open(WidgetState s, bool center = false)
    {
        if (windows.ContainsKey(s.Id))
            return;
        var w = new WidgetWindow(this, s);
        windows[s.Id] = w;
        w.Show();
        if (center)
            w.CenterOnCurrentMonitor(windows.Count - 1, windows.Count);
        w.Activate();
        if (Snoozed)
            w.Hide();
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
        App.CurrentApp.Shell?.Status("Виджеты перемещены в центр текущего монитора");
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
        Save();
        Changed?.Invoke();
    }
    public void DisableAll()
    {
        Settings.DisableIntegrations();
        foreach (var s in Settings.Widgets.ToArray())
            Disable(s);
        Data.Suspend();
        Snoozed = true;
        StartupRegistration.Set(false);
        Save();
        Changed?.Invoke();
    }
    public void SetSnooze(bool snooze)
    {
        if (Snoozed == snooze)
            return;
        Snoozed = snooze;
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
        Changed?.Invoke();
    }
    public void ApplyAppearance()
    {
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
        Data.Dispose();
        Wallpaper.Dispose();
        Save();
    }
}
