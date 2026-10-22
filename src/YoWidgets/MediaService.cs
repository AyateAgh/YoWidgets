using Windows.Media.Control;
using Windows.Media;
using Windows.Storage.Streams;
namespace YoWidgets;

internal sealed class MediaService : IDisposable
{
    readonly MediaArtwork artwork = new();
    readonly BrowserMediaBridge browserBridge = new();
    BrowserMediaState? browserTarget;
    public string BrowserBridgeStatus => browserBridge.Status;
    public string DisplayArtist => !string.IsNullOrWhiteSpace(browserTarget?.Artist) ? browserTarget.Artist : Artist;
    public MediaService() { browserBridge.Changed += BrowserChanged; }
    void BrowserChanged() => DispatchRefresh();
    CancellationTokenSource artworkWork = new();
    string artworkKey = "";
    readonly YoWidgets.Core.MediaClock clock = new();
    static double Monotonic => System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency;
    double lastProbe;
    bool active;
    bool attached;
    bool lyricsMusic;
    string subtitle = "";
    public string ArtworkOrigin { get; private set; } = "none";
    GlobalSystemMediaTransportControlsSessionManager? manager; GlobalSystemMediaTransportControlsSession? session; int generation; int lifecycle; bool starting; bool restartRequested; bool disposed; bool suspended; string? selectedId;
    public string SelectedSource => selectedId ?? "Automatic";
    public IReadOnlyList<string> Sources => manager?.GetSessions().Select(s => s.SourceAppUserModelId).Distinct().ToArray() ?? Array.Empty<string>(); public void SelectSource(string? id)
    {
        selectedId = id;
        Attach(id == null ? manager?.GetCurrentSession() : manager?.GetSessions().FirstOrDefault(s => s.SourceAppUserModelId == id));
    }
    public event Action? Changed; public string Title { get; private set; } = "Not playing"; public string Artist { get; private set; } = "Open a compatible music app"; public string Error { get; private set; } = ""; public BitmapSource? Artwork
    {
        get; private set;
    }
    public bool Playing
    {
        get; private set;
    }
    public bool Available => session != null && active && !suspended && !disposed; public bool Running => !suspended && manager != null; public bool CanSeek
    {
        get; private set;
    }
    public bool CanNext
    {
        get; private set;
    }
    public bool CanPrevious
    {
        get; private set;
    }
    public bool CanPlay
    {
        get; private set;
    }
    public bool CanShuffle
    {
        get; private set;
    }
    public bool CanRepeat
    {
        get; private set;
    }
    public bool Shuffle
    {
        get; private set;
    }
    public int Repeat
    {
        get; private set;
    }
    public double Duration
    {
        get; private set;
    }
    public double Position => clock.Position(Monotonic);
    public bool LyricsEligible => Available && lyricsMusic && !string.IsNullOrWhiteSpace(Title);
    public LyricTrack LyricTrack => LyricTrack.FromMetadata(Title, DisplayArtist, Duration,
        session?.SourceAppUserModelId is string id && new[] { "chrome", "edge", "firefox", "soundcloud" }.Any(name => id.Contains(name, StringComparison.OrdinalIgnoreCase)));
    public async Task StartAsync()
    {
        if (disposed)
            return;
        suspended = false;
        browserBridge.Start();
        if (starting)
        {
            restartRequested = true;
            return;
        }
        starting = true;
        var stamp = ++lifecycle;
        try
        {
            var candidate = manager ?? await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (disposed || suspended || stamp != lifecycle)
                return;
            manager = candidate;
            manager.CurrentSessionChanged -= SessionChanged;
            manager.CurrentSessionChanged += SessionChanged;
            manager.SessionsChanged -= SessionsChanged;
            manager.SessionsChanged += SessionsChanged;
            SelectSource(selectedId);
        }
        catch (Exception e) { if (!suspended && !disposed && stamp == lifecycle) { Error = "Windows media sessions unavailable"; App.Log("media-init", e); Changed?.Invoke(); } }
        finally { starting = false; if (restartRequested && !suspended && !disposed) { restartRequested = false; _ = StartAsync(); } }
    }
    void SessionChanged(GlobalSystemMediaTransportControlsSessionManager s, CurrentSessionChangedEventArgs e)
    {
        if (suspended || disposed)
            return;
        var stamp = lifecycle;
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() => { if (!suspended && !disposed && stamp == lifecycle) SelectSource(selectedId); });
    }
    void SessionsChanged(GlobalSystemMediaTransportControlsSessionManager s, SessionsChangedEventArgs e)
    {
        if (!suspended && !disposed)
            System.Windows.Application.Current.Dispatcher.BeginInvoke(() => { if (!suspended && !disposed) SelectSource(selectedId); });
    }
    void Detach()
    {
        attached = false;
        if (session == null)
            return;
        session.MediaPropertiesChanged -= PropertiesChanged;
        session.PlaybackInfoChanged -= PlaybackChanged;
        session.TimelinePropertiesChanged -= TimelineChanged;
    }
    void Attach(GlobalSystemMediaTransportControlsSession? next)
    {
        if (attached && ReferenceEquals(next, session) && next != null) { ReadTransport(); Changed?.Invoke(); return; }
        Detach();
        session = next;
        clock.Reset();
        active = false;
        browserTarget = null;
        lyricsMusic = false;
        subtitle = "";
        artworkWork.Cancel();
        artworkWork.Dispose();
        artworkWork = new();
        artworkKey = "";
        generation++;
        if (session == null)
        {
            Title = "Not playing";
            Artist = "Open a compatible music app";
            Artwork = null;
            ArtworkOrigin = "none";
            Playing = false;
            Duration = 0;
            CanPlay = CanNext = CanPrevious = CanSeek = CanShuffle = CanRepeat = false;
            Changed?.Invoke();
            return;
        }
        session.MediaPropertiesChanged += PropertiesChanged;
        session.PlaybackInfoChanged += PlaybackChanged;
        session.TimelinePropertiesChanged += TimelineChanged;
        attached = true;
        _ = RefreshAsync();
    }
    void PropertiesChanged(GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs e) => DispatchRefresh(true); void PlaybackChanged(GlobalSystemMediaTransportControlsSession s, PlaybackInfoChangedEventArgs e) => DispatchRefresh(); void TimelineChanged(GlobalSystemMediaTransportControlsSession s, TimelinePropertiesChangedEventArgs e) => DispatchRefresh();
    void DispatchRefresh(bool artworkChanged = false)
    {
        if (!suspended)
            System.Windows.Application.Current.Dispatcher.BeginInvoke(() => { if (suspended || disposed) return; ReadTransport(); Changed?.Invoke(); if (artworkChanged) { artworkKey = ""; _ = RefreshAsync(); } });
    }
    void ReadTransport()
    {
        var current = session;
        if (current == null || suspended || disposed) return;
        try
        {
            var info = current.GetPlaybackInfo();
            active = info.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed;
            Playing = info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            var c = info.Controls;
            CanPlay = active && (c.IsPlayPauseToggleEnabled || (Playing ? c.IsPauseEnabled : c.IsPlayEnabled));
            CanNext = active && c.IsNextEnabled;
            CanPrevious = active && c.IsPreviousEnabled;
            CanSeek = active && c.IsPlaybackPositionEnabled;
            var queueActive = info.PlaybackStatus is GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing or GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused;
            lyricsMusic = MediaCapabilities.IsMusic(info.PlaybackType == MediaPlaybackType.Music, info.PlaybackType == MediaPlaybackType.Video, current.SourceAppUserModelId, Title, Artist, subtitle);
            var t = current.GetTimelineProperties();
            clock.Apply(new(t.Position.TotalSeconds, t.StartTime.TotalSeconds, t.EndTime.TotalSeconds, t.LastUpdatedTime), Playing, info.PlaybackRate, DateTimeOffset.UtcNow, Monotonic);
            Duration = clock.Duration;
            browserTarget = BrowserMediaPolicy.Match(current.SourceAppUserModelId, Title, Duration, Playing, browserBridge.States);
            (CanShuffle, CanRepeat) = MediaCapabilities.QueueControls(queueActive, info.PlaybackType == MediaPlaybackType.Video,
                c.IsShuffleEnabled || browserTarget?.CanShuffle == true, c.IsRepeatEnabled || browserTarget?.CanRepeat == true,
                current.SourceAppUserModelId, Title, Artist, subtitle);
            Shuffle = c.IsShuffleEnabled ? info.IsShuffleActive ?? false : browserTarget?.Shuffle ?? false;
            Repeat = c.IsRepeatEnabled ? (int)(info.AutoRepeatMode ?? MediaPlaybackAutoRepeatMode.None) : browserTarget?.Repeat ?? 0;
            lastProbe = Monotonic;
        }
        catch (Exception e) { App.Log("media-transport", e); }
    }
    async Task RefreshAsync()
    {
        var current = session;
        var version = ++generation;
        if (current == null || suspended)
            return;
        try
        {
            ReadTransport();
            var props = await current.TryGetMediaPropertiesAsync();
            if (suspended || version != generation)
                return;
            Title = string.IsNullOrWhiteSpace(props.Title) ? "Untitled" : props.Title;
            Artist = string.IsNullOrWhiteSpace(props.Artist) ? current.SourceAppUserModelId : props.Artist;
            subtitle = props.Subtitle;
            ReadTransport();
            var key = current.SourceAppUserModelId + "|" + Title + "|" + Artist + "|" + props.AlbumTitle + "|" + (props.Thumbnail != null);
            if (key != artworkKey)
            {
                artworkKey = key;
                artworkWork.Cancel();
                artworkWork.Dispose();
                artworkWork = new();
                Artwork = null;
                ArtworkOrigin = "none";
                _ = LoadArtworkAsync(props.Thumbnail, current.SourceAppUserModelId, Title, Artist, props.Subtitle, key, lifecycle, artworkWork.Token);
            }
            Error = "";
            Changed?.Invoke();
        }
        catch (Exception e) { Error = "Could not refresh player details"; App.Log("media-refresh", e); Changed?.Invoke(); }
    }
    async Task LoadArtworkAsync(IRandomAccessStreamReference? thumbnail, string source, string title, string artist, string subtitle, string key, int stamp, CancellationToken ct)
    {
        try
        {
            var result = await artwork.ResolveAsync(thumbnail, source, title, artist, subtitle, ct);
            if (ct.IsCancellationRequested || suspended || disposed || lifecycle != stamp || artworkKey != key)
                return;
            Artwork = result.Image;
            ArtworkOrigin = result.Origin;
            App.Trace("media-artwork", result.Origin);
            Changed?.Invoke();
        }
        catch (OperationCanceledException) { }
    }
    public void Tick()
    {
        if (!suspended && !disposed && session != null && Monotonic - lastProbe >= 2) { ReadTransport(); Changed?.Invoke(); }
    } // Bounded transport-only correction; thumbnails/metadata are never polled per frame.
    public async Task Command(string command, double value = 0)
    {
        var current = session;
        var stamp = lifecycle;
        if (current == null || suspended || disposed || !Available)
            return;
        ReadTransport();
        if ((command == "shuffle" && !CanShuffle) || (command == "repeat" && !CanRepeat)) return;
        try
        {
            var playback = current.GetPlaybackInfo();
            bool ok = command switch
            {
                "pause" => !Playing || await current.TryPauseAsync(),
                "resume" => Playing || await current.TryPlayAsync(),
                "play" when playback.Controls.IsPlayPauseToggleEnabled => await current.TryTogglePlayPauseAsync(),
                "play" when Playing => await current.TryPauseAsync(),
                "play" => await current.TryPlayAsync(),
                "next" => await current.TrySkipNextAsync(),
                "previous" => await current.TrySkipPreviousAsync(),
                "seek" => await current.TryChangePlaybackPositionAsync((long)((Math.Clamp(value, 0, Duration) + clock.Start) * TimeSpan.TicksPerSecond)),
                "shuffle" when playback.Controls.IsShuffleEnabled => await current.TryChangeShuffleActiveAsync(!Shuffle),
                "repeat" when playback.Controls.IsRepeatEnabled => await current.TryChangeAutoRepeatModeAsync((MediaPlaybackAutoRepeatMode)((Repeat + 1) % 3)),
                "shuffle" or "repeat" when browserTarget is { } target => await browserBridge.CommandAsync(target, command),
                _ => false
            };
            if (suspended || disposed || stamp != lifecycle || !ReferenceEquals(current, session)) return;
            Error = ok ? "" : "The source declined this command";
            if (ok && command == "seek") clock.Seek(value, Monotonic);
            if (ok)
            { ReadTransport(); Changed?.Invoke(); }
            else
                Changed?.Invoke();
        }
        catch (Exception e) { if (suspended || disposed || stamp != lifecycle || !ReferenceEquals(current, session)) return; Error = "This command is unavailable in the source"; App.Log("media-command", e); Changed?.Invoke(); }
    }
    public void Suspend()
    {
        suspended = true;
        browserTarget = null;
        browserBridge.Stop();
        Playing = active = lyricsMusic = false;
        CanPlay = CanNext = CanPrevious = CanSeek = CanShuffle = CanRepeat = false;
        artworkWork.Cancel();
        artworkKey = "";
        lifecycle++;
        restartRequested = false;
        generation++;
        Detach();
        if (manager != null)
        {
            manager.CurrentSessionChanged -= SessionChanged;
            manager.SessionsChanged -= SessionsChanged;
        }
    }
    public void Dispose()
    {
        disposed = true;
        Suspend();
        session = null;
        manager = null;
        Artwork = null;
        artworkWork.Dispose();
        artwork.Clear();
        browserBridge.Changed -= BrowserChanged;
        browserBridge.Dispose();
    }
}
