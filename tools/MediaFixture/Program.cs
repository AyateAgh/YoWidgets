using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Windows.Media;
using Windows.Media.Playback;

// An explicitly labelled, silent native SMTC producer for integration tests.
// It sends real Windows transport events; it does not impersonate a music service.
internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var app = new Application();
        using var player = new MediaPlayer();
        player.CommandManager.IsEnabled = false;
        var transport = player.SystemMediaTransportControls;
        var window = new Window { Title = "YoWidgets · synthetic media fixture", Width = 420, Height = 470, Left = 15, Top = 15 };
        var panel = new StackPanel { Margin = new Thickness(20) };
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
        panel.Children.Add(new TextBlock { Text = "SYNTHETIC TEST · NO AUDIO", FontWeight = FontWeights.Bold });
        panel.Children.Add(status);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var publication = System.Diagnostics.Stopwatch.StartNew();
        double offset = 0;
        var video = false;
        var playing = true;
        var sample = args.Contains("--lyrics-sample");
        double Position() => (offset + (playing ? clock.Elapsed.TotalSeconds : 0)) % 233;
        void Snapshot() { transport.UpdateTimelineProperties(new SystemMediaTransportControlsTimelineProperties { StartTime = TimeSpan.Zero, EndTime = TimeSpan.FromSeconds(233), MinSeekTime = TimeSpan.Zero, MaxSeekTime = TimeSpan.FromSeconds(233), Position = TimeSpan.FromSeconds(Position()) }); publication.Restart(); }
        void ShowStatus() { var seconds = (int)Position(); status.Text = $"{(video ? "VIDEO" : "MUSIC")} · source time {seconds / 60}:{seconds % 60:00}\nShuffle={transport.ShuffleEnabled}; Repeat={transport.AutoRepeatMode}\nTimeline published every 8 seconds."; }
        void Playing(bool value) { offset = Position(); playing = value; clock.Restart(); }
        void Source(bool isVideo)
        {
            video = isVideo;
            transport.IsEnabled = true;
            transport.IsPlayEnabled = transport.IsPauseEnabled = transport.IsNextEnabled = transport.IsPreviousEnabled = true;
            transport.DisplayUpdater.Type = isVideo ? MediaPlaybackType.Video : MediaPlaybackType.Music;
            if (isVideo) transport.DisplayUpdater.VideoProperties.Title = "TEST FIXTURE | YouTube";
            else { transport.DisplayUpdater.MusicProperties.Title = sample ? "addiction (Slowed)" : "TEST FIXTURE · music"; transport.DisplayUpdater.MusicProperties.Artist = sample ? "LONOWN" : "Synthetic transport source"; }
            transport.DisplayUpdater.Update();
            transport.PlaybackRate = 1;
            transport.PlaybackStatus = playing ? MediaPlaybackStatus.Playing : MediaPlaybackStatus.Paused;
            Snapshot();
            ShowStatus();
        }
        void Button(string title, Action action)
        {
            var button = new Button { Content = title, Margin = new Thickness(0, 4, 0, 4), Padding = new Thickness(10, 7, 10, 7) };
            button.Click += (_, _) => action();
            panel.Children.Add(button);
        }
        Button("Music · advertise queue capabilities", () => Source(false));
        Button("Video · queue controls must hide", () => Source(true));
        Button("Pause / resume", () => { Playing(!playing); transport.PlaybackStatus = playing ? MediaPlaybackStatus.Playing : MediaPlaybackStatus.Paused; Snapshot(); ShowStatus(); });
        Button("Close media session", () => { transport.PlaybackStatus = MediaPlaybackStatus.Closed; transport.IsEnabled = false; });
        Button("Lyrics sample · provider lookup test", () => { sample = true; offset = 0; clock.Restart(); Source(false); });
        Button("Unknown track · lyrics must hide", () => { sample = false; offset = 0; clock.Restart(); Source(false); });
        Button("Seek to 0:40", () => { offset = 40; clock.Restart(); Snapshot(); ShowStatus(); });
        transport.ShuffleEnabledChangeRequested += (_, e) => window.Dispatcher.BeginInvoke(() => { transport.ShuffleEnabled = e.RequestedShuffleEnabled; ShowStatus(); });
        transport.AutoRepeatModeChangeRequested += (_, e) => window.Dispatcher.BeginInvoke(() => { transport.AutoRepeatMode = e.RequestedAutoRepeatMode; ShowStatus(); });
        transport.PlaybackPositionChangeRequested += (_, e) => window.Dispatcher.BeginInvoke(() => { offset = e.RequestedPlaybackPosition.TotalSeconds; clock.Restart(); Snapshot(); ShowStatus(); });
        transport.ButtonPressed += (_, e) => window.Dispatcher.BeginInvoke(() => { if (e.Button is SystemMediaTransportControlsButton.Play or SystemMediaTransportControlsButton.Pause) Playing(e.Button == SystemMediaTransportControlsButton.Play); else if (e.Button is SystemMediaTransportControlsButton.Next or SystemMediaTransportControlsButton.Previous) { offset = 0; clock.Restart(); } Source(video); });
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        double previousPosition = 0;
        timer.Tick += (_, _) => { var current = Position(); if (transport.IsEnabled && (publication.Elapsed.TotalSeconds >= 8 || current < previousPosition)) Snapshot(); previousPosition = current; ShowStatus(); };
        window.Content = panel;
        window.Loaded += (_, _) => { Source(false); timer.Start(); };
        window.Closed += (_, _) => { timer.Stop(); transport.IsEnabled = false; };
        app.Run(window);
    }
}
