using System.Windows.Controls.Primitives;
namespace YoWidgets;

internal sealed class MusicView : Grid
{
    readonly WidgetManager manager;
    readonly WidgetWindow? window;
    readonly bool preview;
    readonly MediaService media;
    readonly ColumnDefinition artColumn = new() { Width = new GridLength(68) };
    readonly RowDefinition expandedRow = new() { Height = new GridLength(0) };
    readonly Border smallArt = new() { Width = 54, Height = 54, CornerRadius = new CornerRadius(12), ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Left };
    readonly Image smallImage = new() { Stretch = Stretch.UniformToFill };
    readonly Border album = new() { CornerRadius = new CornerRadius(20), ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    readonly Image albumImage = new() { Stretch = Stretch.UniformToFill };
    readonly TextBlock title = WidgetVisuals.Label("Музыка рядом", 15, true);
    readonly TextBlock artist = WidgetVisuals.Label("Откройте совместимый плеер", 12, secondary: true);
    readonly TextBlock elapsed = WidgetVisuals.Label("0:00", 10, secondary: true);
    readonly TextBlock remaining = WidgetVisuals.Label("−0:00", 10, secondary: true);
    readonly TextBlock lyrics = WidgetVisuals.Label("", 17, true);
    readonly Slider seek = new() { Minimum = 0, Maximum = 1, Height = 18, Margin = new Thickness(0) };
    readonly Button play, previous, next, shuffle, repeat, words, expand;
    bool updating, showLyrics;
    public MusicView(WidgetManager manager, WidgetWindow? window, bool preview)
    {
        this.manager = manager;
        this.window = window;
        this.preview = preview;
        media = manager.Data.Media;
        VerticalAlignment = VerticalAlignment.Center;
        RowDefinitions.Add(new()
        {
            Height = new GridLength(54)
        });
        RowDefinitions.Add(expandedRow);
        RowDefinitions.Add(new()
        {
            Height = new GridLength(18)
        });
        RowDefinitions.Add(new()
        {
            Height = new GridLength(14)
        });
        RowDefinitions.Add(new()
        {
            Height = new GridLength(46)
        });
        var header = new Grid();
        header.ColumnDefinitions.Add(artColumn);
        header.ColumnDefinitions.Add(new());
        header.ColumnDefinitions.Add(new()
        {
            Width = GridLength.Auto
        });
        smallArt.Child = smallImage;
        header.Children.Add(smallArt);
        smallArt.SizeChanged += (_, _) => smallArt.Clip = new RectangleGeometry(new Rect(0, 0, smallArt.ActualWidth, smallArt.ActualHeight), 12, 12);
        var text = Ui.Stack(title, Ui.Gap(4), artist);
        text.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(text, 1);
        header.Children.Add(text);
        var headerActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(6, 0, -4, 0) };
        words = WidgetVisuals.IconButton("lyrics", "Текст песни", () => { showLyrics = !showLyrics; if (showLyrics && window?.Expanded == false) window.ToggleExpanded(); Update(); }, 28);
        expand = WidgetVisuals.IconButton("expand", "Раскрыть плеер", () => window?.ToggleExpanded(), 28);
        headerActions.Children.Add(words);
        headerActions.Children.Add(expand);
        Grid.SetColumn(headerActions, 2);
        header.Children.Add(headerActions);
        Children.Add(header);
        var center = new Grid { Margin = new Thickness(0, 12, 0, 8) };
        album.Child = albumImage;
        center.Children.Add(album);
        album.SizeChanged += (_, _) => album.Clip = new RectangleGeometry(new Rect(0, 0, album.ActualWidth, album.ActualHeight), 20, 20);
        lyrics.TextWrapping = TextWrapping.Wrap;
        lyrics.TextAlignment = TextAlignment.Left;
        lyrics.VerticalAlignment = VerticalAlignment.Center;
        lyrics.LineHeight = 27;
        lyrics.MaxHeight = 180;
        center.Children.Add(lyrics);
        SetRow(center, 1);
        Children.Add(center);
        SetRow(seek, 2);
        Children.Add(seek);
        seek.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(async (_, _) => { if (!updating && media.CanSeek) await media.Command("seek", seek.Value); }));
        seek.PreviewMouseLeftButtonUp += async (_, e) => { if (!updating && media.CanSeek && e.OriginalSource is not Thumb && !seek.IsMouseCaptureWithin) await media.Command("seek", seek.Value); };
        var timeRow = new Grid();
        remaining.HorizontalAlignment = HorizontalAlignment.Right;
        timeRow.Children.Add(elapsed);
        timeRow.Children.Add(remaining);
        SetRow(timeRow, 3);
        Children.Add(timeRow);
        var controls = new Grid { Margin = new Thickness(-4, 0, -4, 0) };
        foreach (var width in new[] { 1d, 1.2, 1.35, 1.2, 1d })
            controls.ColumnDefinitions.Add(new()
            {
                Width = new GridLength(width, GridUnitType.Star)
            });
        shuffle = WidgetVisuals.IconButton("shuffle", "Перемешивание", async () => await media.Command("shuffle"));
        previous = WidgetVisuals.IconButton("previous", "Предыдущий трек", async () => await media.Command("previous"), 40);
        play = WidgetVisuals.IconButton("play", "Воспроизведение / пауза", async () => await media.Command("play"), 46, true);
        next = WidgetVisuals.IconButton("next", "Следующий трек", async () => await media.Command("next"), 40);
        repeat = WidgetVisuals.IconButton("repeat", "Повтор", async () => await media.Command("repeat"));
        var buttons = new[] { shuffle, previous, play, next, repeat };
        for (var i = 0; i < buttons.Length; i++)
        {
            buttons[i].HorizontalAlignment = HorizontalAlignment.Center;
            buttons[i].VerticalAlignment = VerticalAlignment.Center;
            SetColumn(buttons[i], i);
            controls.Children.Add(buttons[i]);
        }
        SetRow(controls, 4);
        Children.Add(controls);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }
    void OnLoaded(object sender, RoutedEventArgs e)
    {
        manager.Data.Changed += Update;
        if (window != null)
            window.ExpansionChanged += Update;
        Update();
    }
    void OnUnloaded(object sender, RoutedEventArgs e)
    {
        manager.Data.Changed -= Update;
        if (window != null)
            window.ExpansionChanged -= Update;
    }
    void Update()
    {
        var expanded = window?.Expanded == true;
        var hasArtwork = media.Artwork != null;
        smallImage.Source = albumImage.Source = media.Artwork;
        smallArt.Visibility = hasArtwork ? Visibility.Visible : Visibility.Collapsed;
        artColumn.Width = new GridLength(hasArtwork ? 68 : 0);
        smallArt.ToolTip = media.ArtworkOrigin == "track" ? "Обложка текущего трека" : "Логотип источника";
        title.Text = media.Title;
        artist.Text = media.Error.Length > 0 ? media.Error : media.Artist;
        expandedRow.Height = new GridLength(expanded ? 218 : 0);
        album.Width = album.Height = media.ArtworkOrigin == "track" ? 190 : 88;
        album.Visibility = expanded && hasArtwork && !showLyrics ? Visibility.Visible : Visibility.Collapsed;
        lyrics.Visibility = expanded && (showLyrics || !hasArtwork) ? Visibility.Visible : Visibility.Collapsed;
        lyrics.Text = showLyrics ? (manager.Data.LyricLines.Count > 0 ? string.Join("\n", manager.Data.LyricLines.Where(l => l.Seconds <= media.Position + 5).TakeLast(4).Select(l => l.Text)) : manager.Data.LyricsText) : media.Artist;
        if (play.Tag is not bool wasPlaying || wasPlaying != media.Playing)
        {
            play.Content = WidgetVisuals.Icon(media.Playing ? "pause" : "play", 24);
            play.Tag = media.Playing;
        }
        play.IsEnabled = media.Available && media.CanPlay && !preview;
        previous.IsEnabled = media.CanPrevious && !preview;
        next.IsEnabled = media.CanNext && !preview;
        shuffle.IsEnabled = media.CanShuffle && !preview;
        repeat.IsEnabled = media.CanRepeat && !preview;
        shuffle.Background = new SolidColorBrush(Color.FromArgb(media.Shuffle ? (byte)40 : (byte)0, 255, 255, 255));
        repeat.Background = new SolidColorBrush(Color.FromArgb(media.Repeat > 0 ? (byte)40 : (byte)0, 255, 255, 255));
        words.IsEnabled = manager.Settings.LyricsEnabled && !preview;
        words.Opacity = showLyrics ? 1 : .65;
        expand.IsEnabled = window != null && !preview;
        expand.Content = WidgetVisuals.Icon(expanded ? "collapse" : "expand", 16);
        updating = true;
        if (!seek.IsMouseCaptureWithin)
        {
            seek.Maximum = Math.Max(1, media.Duration);
            seek.Value = media.Position;
        }
        seek.IsEnabled = media.CanSeek && !preview;
        updating = false;
        elapsed.Text = Format(media.Position);
        remaining.Text = "−" + Format(Math.Max(0, media.Duration - media.Position));
        elapsed.Visibility = remaining.Visibility = media.Duration > 0 ? Visibility.Visible : Visibility.Hidden;
    }
    static string Format(double seconds) => $"{(int)seconds / 60}:{(int)seconds % 60:00}";
}
