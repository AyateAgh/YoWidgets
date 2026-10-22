namespace YoWidgets;

internal sealed class DataServices : IDisposable
{
    public BrowserAssistantBridge Browser {get;}
    public MediaService Media { get; } = new(); public WeatherService Weather { get; } = new(); public NotificationService Notifications { get; } = new(); public AiService Ai { get; } = new(); public SystemControls Controls { get; } = new();
    public LyricResult TrackLyrics { get; private set; } = LyricResult.Empty;
    public bool LyricsAvailable => manager.Settings.LyricsEnabled && Media.LyricsEligible && TrackLyrics.Available;
    public string LyricsStatus { get; private set; } = "Enable lyrics in Settings";
    readonly HttpClient lyricsHttp = new() { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
    readonly LyricClient lyricsClient;
    CancellationTokenSource? lyricWork;
    int lyricGeneration;
    DateTimeOffset nextLyricsAttempt;
    public DateTimeOffset? FocusEnd
    {
        get; private set;
    }
    public event Action? Changed; readonly WidgetManager manager; readonly DispatcherTimer timer; CancellationTokenSource work = new(); DateTimeOffset lastWeather = DateTimeOffset.MinValue; string lyricsKey = ""; bool suspended; bool disposed;
    public DataServices(WidgetManager m)
    {
        manager = m;
        Browser=new(BrowserCompanionSetup.PairingToken());
        Ai.Browser=Browser;
        lyricsClient = new(lyricsHttp);
        timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        timer.Tick += Tick;
        Media.Changed += OnMedia;
        Weather.Changed += Notify;
        Notifications.Changed += Notify;
        Ai.Changed += Notify;
        timer.Start();
        m.Changed += UpdateActivity;
    }
    void Notify()
    {
        if (disposed || suspended)
            return;
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() => Changed?.Invoke());
    }
    internal void NotifyWidgets()=>Notify();
    public void UpdateActivity()
    {
        if (suspended || disposed)
            return;
        var kinds = manager.Settings.Widgets.Where(w => w.Enabled).Select(w => w.Kind).ToArray();
        if (!kinds.Contains("assistant")) Ai.Cancel();
        if (kinds.Contains("music") && !Media.Running)
            _ = Media.StartAsync();
        if (!kinds.Contains("music"))
            Media.Suspend();
        RefreshLyrics();
        ScheduleTick();
        if (kinds.Contains("weather") && DateTimeOffset.Now - lastWeather > TimeSpan.FromMinutes(20))
        {
            lastWeather = DateTimeOffset.Now;
            _ = Weather.RefreshAsync(manager.Settings.City, work.Token);
        }
        Notify();
    }
    void Tick(object? sender, EventArgs e)
    {
        if (suspended)
            return;
        UpdateActivity();
        Media.Tick();
        if (FocusEnd != null && DateTimeOffset.Now >= FocusEnd)
        {
            FocusEnd = null;
            App.CurrentApp.Shell?.Status("Focus complete. Time for a break.");
        }
        if (manager.Settings.Widgets.Any(x => x.Enabled && x.Kind == "weather") && DateTimeOffset.Now - lastWeather > TimeSpan.FromMinutes(20))
        {
            lastWeather = DateTimeOffset.Now;
            _ = Weather.RefreshAsync(manager.Settings.City, work.Token);
        }
        Notify();
    }
    void OnMedia()
    {
        ScheduleTick();
        Notify();
        RefreshLyrics();
    }
    void ScheduleTick()
    {
        if (suspended || disposed) return;
        var clock = manager.Settings.Widgets.Any(w => w.Enabled && w.Kind == "clock");
        var seconds = ActivityPolicy.Seconds(clock && manager.Settings.Seconds, Media.Playing, FocusEnd != null, false);
        timer.Interval = TimeSpan.FromSeconds(ActivityPolicy.NextDelay(seconds, clock && !manager.Settings.Seconds, DateTime.Now));
    }
    public void StartFocus(int minutes)
    {
        FocusEnd = DateTimeOffset.Now.AddMinutes(minutes);
        UpdateActivity();
    }
    public void StopFocus()
    {
        FocusEnd = null;
        UpdateActivity();
    }
    public async Task RefreshWeatherAsync()
    {
        lastWeather = DateTimeOffset.Now;
        await Weather.RefreshAsync(manager.Settings.City, work.Token);
    }
    public void RefreshLyrics()
    {
        if (suspended || disposed) return;
        if (!manager.Settings.LyricsEnabled || !Media.LyricsEligible || !manager.Settings.Widgets.Any(w => w.Enabled && w.Kind == "music"))
        {
            if (lyricsKey.Length > 0 || TrackLyrics.Available) { ClearLyrics(); Notify(); }
            LyricsStatus = manager.Settings.LyricsEnabled ? "No lyrics for this source" : "Enable lyrics in Settings";
            return;
        }
        var track = Media.LyricTrack;
        if (track.Key == lyricsKey && DateTimeOffset.UtcNow < nextLyricsAttempt) return;
        lyricWork?.Cancel(); lyricWork?.Dispose();
        lyricWork = CancellationTokenSource.CreateLinkedTokenSource(work.Token);
        lyricWork.CancelAfter(TimeSpan.FromSeconds(20));
        var stamp = ++lyricGeneration;
        lyricsKey = track.Key;
        TrackLyrics = LyricResult.Empty;
        LyricsStatus = "Finding lyrics…";
        nextLyricsAttempt = DateTimeOffset.MaxValue;
        Notify();
        _ = LoadLyricsAsync(track, stamp, lyricWork.Token);
    }
    void ClearLyrics()
    {
        ++lyricGeneration;
        lyricWork?.Cancel();
        lyricsKey = "";
        TrackLyrics = LyricResult.Empty;
        nextLyricsAttempt = DateTimeOffset.MinValue;
    }
    async Task LoadLyricsAsync(LyricTrack track, int stamp, CancellationToken ct)
    {
        bool Current() => !suspended && !disposed && !ct.IsCancellationRequested && stamp == lyricGeneration && manager.Settings.LyricsEnabled && lyricsKey == track.Key;
        try
        {
            var result = await lyricsClient.FindAsync(track, ct);
            if (!Current()) return;
            TrackLyrics = result;
            LyricsStatus = result.Synced ? "Synced lyrics ready" : result.Available ? "Lyrics ready · timing unavailable" : "No lyrics found for this track";
            nextLyricsAttempt = DateTimeOffset.UtcNow.AddMinutes(result.Available ? 120 : 30);
            App.Trace("lyrics", $"available={result.Available}; synced={result.Synced}; lines={result.Lines.Count}");
        }
        catch (LyricRetryException e) { if (!Current()) return; nextLyricsAttempt = e.RetryAt; LyricsStatus = "Lyrics service is busy. Retrying later."; }
        catch (OperationCanceledException)
        {
            if (suspended || disposed || stamp != lyricGeneration) return;
            nextLyricsAttempt = DateTimeOffset.UtcNow.AddMinutes(2);
            LyricsStatus = "Lyrics request timed out. Retrying later.";
        }
        catch (Exception e) { if (!Current()) return; nextLyricsAttempt = DateTimeOffset.UtcNow.AddMinutes(2); LyricsStatus = "Lyrics unavailable. Retrying later."; App.Log("lyrics", e); }
        Notify();
    }
    public void Suspend()
    {
        if (suspended)
            return;
        suspended = true;
        ClearLyrics();
        timer.Stop();
        work.Cancel();
        Media.Suspend();
        Notifications.Suspend();
        Ai.Cancel();
        Browser.Stop();
    }
    public void Resume()
    {
        if (!suspended)
            return;
        work.Dispose();
        work = new();
        suspended = false;
        lastWeather = DateTimeOffset.MinValue;
        timer.Start();
        UpdateActivity();
        if (manager.Settings.NotificationsEnabled && manager.Settings.Widgets.Any(w => w.Enabled && w.Kind == "notifications"))
        {
            Notifications.Enable();
            _ = Notifications.RefreshAsync(false);
        }
        Notify();
    }
    public void Dispose()
    {
        if (disposed)
            return;
        Suspend();
        disposed = true;
        timer.Tick -= Tick;
        manager.Changed -= UpdateActivity;
        Media.Changed -= OnMedia;
        Weather.Changed -= Notify;
        Notifications.Changed -= Notify;
        Ai.Changed -= Notify;
        Media.Dispose();
        Browser.Dispose();
        Ai.CloseBrowser();
        Controls.Dispose();
        lyricWork?.Dispose();
        lyricsClient.Dispose();
        lyricsHttp.Dispose();
        work.Dispose();
    }
}
