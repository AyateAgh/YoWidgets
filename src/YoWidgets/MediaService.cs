using Windows.Media.Control;
using Windows.Media;
using Windows.Storage.Streams;
namespace YoWidgets;

internal sealed class MediaService : IDisposable
{
    GlobalSystemMediaTransportControlsSessionManager? manager; GlobalSystemMediaTransportControlsSession? session; int generation; int lifecycle; bool starting; bool restartRequested; bool disposed; bool suspended; string? selectedId; DateTimeOffset positionTime; double position;
    public IReadOnlyList<string> Sources => manager?.GetSessions().Select(s => s.SourceAppUserModelId).Distinct().ToArray() ?? Array.Empty<string>(); public void SelectSource(string? id)
    {
        selectedId = id;
        Attach(id == null ? manager?.GetCurrentSession() : manager?.GetSessions().FirstOrDefault(s => s.SourceAppUserModelId == id));
    }
    public event Action? Changed; public string Title { get; private set; } = "Музыка рядом"; public string Artist { get; private set; } = "Откройте совместимый плеер"; public string Error { get; private set; } = ""; public BitmapSource? Artwork
    {
        get; private set;
    }
    public bool Playing
    {
        get; private set;
    }
    public bool Available => session != null; public bool Running => !suspended && manager != null; public bool CanSeek
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
    public double Position => Math.Clamp(position + (Playing ? (DateTimeOffset.Now - positionTime).TotalSeconds : 0), 0, Math.Max(0, Duration));
    public async Task StartAsync()
    {
        if (disposed)
            return;
        suspended = false;
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
            SelectSource(selectedId);
        }
        catch (Exception e) { if (!suspended && !disposed && stamp == lifecycle) { Error = "Медиасессии Windows недоступны"; App.Log("media-init", e); Changed?.Invoke(); } }
        finally { starting = false; if (restartRequested && !suspended && !disposed) { restartRequested = false; _ = StartAsync(); } }
    }
    void SessionChanged(GlobalSystemMediaTransportControlsSessionManager s, CurrentSessionChangedEventArgs e)
    {
        if (suspended || disposed)
            return;
        var stamp = lifecycle;
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() => { if (!suspended && !disposed && stamp == lifecycle) SelectSource(selectedId); });
    }
    void Detach()
    {
        if (session == null)
            return;
        session.MediaPropertiesChanged -= PropertiesChanged;
        session.PlaybackInfoChanged -= PlaybackChanged;
        session.TimelinePropertiesChanged -= TimelineChanged;
    }
    void Attach(GlobalSystemMediaTransportControlsSession? next)
    {
        Detach();
        session = next;
        generation++;
        if (session == null)
        {
            Title = "Музыка рядом";
            Artist = "Откройте совместимый плеер";
            Artwork = null;
            Playing = false;
            Duration = position = 0;
            CanPlay = CanNext = CanPrevious = CanSeek = false;
            Changed?.Invoke();
            return;
        }
        session.MediaPropertiesChanged += PropertiesChanged;
        session.PlaybackInfoChanged += PlaybackChanged;
        session.TimelinePropertiesChanged += TimelineChanged;
        _ = RefreshAsync();
    }
    void PropertiesChanged(GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs e) => DispatchRefresh(); void PlaybackChanged(GlobalSystemMediaTransportControlsSession s, PlaybackInfoChangedEventArgs e) => DispatchRefresh(); void TimelineChanged(GlobalSystemMediaTransportControlsSession s, TimelinePropertiesChangedEventArgs e) => DispatchRefresh();
    void DispatchRefresh()
    {
        if (!suspended)
            System.Windows.Application.Current.Dispatcher.BeginInvoke(() => { _ = RefreshAsync(); });
    }
    async Task RefreshAsync()
    {
        var current = session;
        var version = ++generation;
        if (current == null || suspended)
            return;
        try
        {
            var props = await current.TryGetMediaPropertiesAsync();
            if (suspended || version != generation)
                return;
            Title = string.IsNullOrWhiteSpace(props.Title) ? "Без названия" : props.Title;
            Artist = string.IsNullOrWhiteSpace(props.Artist) ? current.SourceAppUserModelId : props.Artist;
            var info = current.GetPlaybackInfo();
            Playing = info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            var c = info.Controls;
            CanPlay = c.IsPlayPauseToggleEnabled;
            CanNext = c.IsNextEnabled;
            CanPrevious = c.IsPreviousEnabled;
            CanSeek = c.IsPlaybackPositionEnabled;
            CanShuffle = c.IsShuffleEnabled;
            CanRepeat = c.IsRepeatEnabled;
            Shuffle = info.IsShuffleActive ?? false;
            Repeat = (int)(info.AutoRepeatMode ?? MediaPlaybackAutoRepeatMode.None);
            var t = current.GetTimelineProperties();
            Duration = Math.Max(0, (t.EndTime - t.StartTime).TotalSeconds);
            position = t.Position.TotalSeconds;
            positionTime = DateTimeOffset.Now;
            if (props.Thumbnail != null)
            {
                using var stream = await props.Thumbnail.OpenReadAsync();
                if (stream.Size <= 8_000_000)
                {
                    using var reader = new DataReader(stream.GetInputStreamAt(0));
                    await reader.LoadAsync((uint)stream.Size);
                    var bytes = new byte[(int)stream.Size];
                    reader.ReadBytes(bytes);
                    if (suspended || version != generation)
                        return;
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.StreamSource = new MemoryStream(bytes);
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.DecodePixelWidth = 400;
                    image.EndInit();
                    image.Freeze();
                    Artwork = image;
                }
            }
            else
                Artwork = null;
            Error = "";
            Changed?.Invoke();
        }
        catch (Exception e) { Error = "Не удалось обновить данные плеера"; App.Log("media-refresh", e); Changed?.Invoke(); }
    }
    public void Tick()
    {
    } // Position is extrapolated; no polling of the player per frame.
    public async Task Command(string command, double value = 0)
    {
        if (session == null || suspended)
            return;
        try
        {
            bool ok = command switch
            {
                "play" => await session.TryTogglePlayPauseAsync(),
                "next" => await session.TrySkipNextAsync(),
                "previous" => await session.TrySkipPreviousAsync(),
                "seek" => await session.TryChangePlaybackPositionAsync((long)(Math.Clamp(value, 0, Duration) * TimeSpan.TicksPerSecond)),
                "shuffle" => await session.TryChangeShuffleActiveAsync(!Shuffle),
                "repeat" => await session.TryChangeAutoRepeatModeAsync((MediaPlaybackAutoRepeatMode)((Repeat + 1) % 3)),
                _ => false
            };
            Error = ok ? "" : "Источник отклонил команду";
            if (ok)
                await RefreshAsync();
            else
                Changed?.Invoke();
        }
        catch (Exception e) { Error = "Команда недоступна в этом плеере"; App.Log("media-command", e); Changed?.Invoke(); }
    }
    public void Suspend()
    {
        suspended = true;
        lifecycle++;
        restartRequested = false;
        generation++;
        Detach();
        if (manager != null)
            manager.CurrentSessionChanged -= SessionChanged;
    }
    public void Dispose()
    {
        disposed = true;
        Suspend();
        session = null;
        manager = null;
        Artwork = null;
    }
}
