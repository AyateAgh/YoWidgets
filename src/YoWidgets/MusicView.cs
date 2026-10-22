using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
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
    readonly TextBlock title = WidgetVisuals.Label("Not playing", 15, true);
    readonly TextBlock artist = WidgetVisuals.Label("Open a compatible music app", 12, secondary: true);
    readonly TextBlock elapsed = WidgetVisuals.Label("0:00", 10, secondary: true);
    readonly TextBlock remaining = WidgetVisuals.Label("−0:00", 10, secondary: true);
    readonly TextBlock noArtwork = WidgetVisuals.Label("", 17, true);
    readonly LyricsView lyrics = new();
    readonly Slider seek = new() { Minimum = 0, Maximum = 1, Height = 18, Margin = new Thickness(0) };
    readonly Button play, previous, next, shuffle, repeat, words, expand;
    readonly Grid controls = new() { Margin = new Thickness(-4, 0, -4, 0) };
    readonly TranslateTransform shuffleSlide = new(), repeatSlide = new();
    readonly DispatcherTimer progressTimer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };
    bool? shownShuffle, shownRepeat;
    static readonly DependencyProperty SideRevealProperty = DependencyProperty.Register("SideReveal", typeof(double), typeof(MusicView), new PropertyMetadata(0d, (d, _) => ((MusicView)d).LayoutSides()));
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
        words = WidgetVisuals.IconButton("lyrics", "Lyrics", () => { showLyrics = !showLyrics; if (showLyrics && window?.Expanded == false) window.ToggleExpanded(); Update(); }, 28);
        words.Visibility = Visibility.Collapsed;
        expand = WidgetVisuals.IconButton("expand", "Expand player", () => window?.ToggleExpanded(), 28);
        headerActions.Children.Add(words);
        headerActions.Children.Add(expand);
        Grid.SetColumn(headerActions, 2);
        header.Children.Add(headerActions);
        Children.Add(header);
        var center = new Grid { Margin = new Thickness(0, 12, 0, 8) };
        album.Child = albumImage;
        center.Children.Add(album);
        album.SizeChanged += (_, _) => album.Clip = new RectangleGeometry(new Rect(0, 0, album.ActualWidth, album.ActualHeight), 20, 20);
        noArtwork.TextWrapping = TextWrapping.Wrap;
        noArtwork.VerticalAlignment = VerticalAlignment.Center;
        center.Children.Add(noArtwork);
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
        foreach (var width in new[] { 0d, 1.2, 1.35, 1.2, 0d })
            controls.ColumnDefinitions.Add(new()
            {
                Width = width == 0 ? new GridLength(0) : new GridLength(width, GridUnitType.Star)
            });
        shuffle = WidgetVisuals.IconButton("shuffle", "Shuffle", async () => await media.Command("shuffle"));
        previous = WidgetVisuals.IconButton("previous", "Previous track", async () => await media.Command("previous"), 40);
        play = WidgetVisuals.IconButton("play", "Play / pause", async () => await media.Command("play"), 46, true);
        next = WidgetVisuals.IconButton("next", "Next track", async () => await media.Command("next"), 40);
        repeat = WidgetVisuals.IconButton("repeat", "Repeat", async () => await media.Command("repeat"));
        var buttons = new[] { shuffle, previous, play, next, repeat };
        for (var i = 0; i < buttons.Length; i++)
        {
            buttons[i].HorizontalAlignment = HorizontalAlignment.Center;
            buttons[i].VerticalAlignment = VerticalAlignment.Center;
            var slot = new Border { Child = buttons[i], ClipToBounds = true };
            SetColumn(slot, i);
            controls.Children.Add(slot);
        }
        shuffle.RenderTransform = shuffleSlide;
        repeat.RenderTransform = repeatSlide;
        shuffle.Opacity = repeat.Opacity = 0;
        controls.SizeChanged += (_, _) => LayoutSides();
        progressTimer.Tick += (_, _) => UpdateProgress();
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
        IsVisibleChanged += VisibilityChanged;
        Update();
    }
    void OnUnloaded(object sender, RoutedEventArgs e)
    {
        manager.Data.Changed -= Update;
        progressTimer.Stop();
        IsVisibleChanged -= VisibilityChanged;
        BeginAnimation(SideRevealProperty, null);
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
        smallArt.ToolTip = media.ArtworkOrigin == "track" ? "Current track artwork" : "Source logo";
        title.Text = media.Title;
        artist.Text = media.Error.Length > 0 ? media.Error : media.DisplayArtist;
        expandedRow.Height = new GridLength(expanded ? 218 : 0);
        album.Width = album.Height = media.ArtworkOrigin == "track" ? 190 : 88;
        var hasLyrics = manager.Data.LyricsAvailable && !preview;
        var displayingLyrics = showLyrics && hasLyrics;
        album.Visibility = expanded && hasArtwork && !displayingLyrics ? Visibility.Visible : Visibility.Collapsed;
        lyrics.Visibility = expanded && displayingLyrics ? Visibility.Visible : Visibility.Collapsed;
        noArtwork.Visibility = expanded && !hasArtwork && !displayingLyrics ? Visibility.Visible : Visibility.Collapsed;
        noArtwork.Text = media.DisplayArtist;
        lyrics.SetLyrics(manager.Data.TrackLyrics, media.Position);
        if (play.Tag is not bool wasPlaying || wasPlaying != media.Playing)
        {
            play.Content = WidgetVisuals.Icon(media.Playing ? "pause" : "play", 24);
            play.Tag = media.Playing;
        }
        play.IsEnabled = media.Available && media.CanPlay && !preview;
        previous.IsEnabled = media.CanPrevious && !preview;
        next.IsEnabled = media.CanNext && !preview;
        UpdateSides();
        shuffle.Background = new SolidColorBrush(Color.FromArgb(media.Shuffle ? (byte)40 : (byte)0, 255, 255, 255));
        repeat.Background = new SolidColorBrush(Color.FromArgb(media.Repeat > 0 ? (byte)40 : (byte)0, 255, 255, 255));
        shuffle.ToolTip = media.Shuffle ? "Shuffle on" : "Shuffle off";
        repeat.ToolTip = media.Repeat switch { 1 => "Repeat one", 2 => "Repeat all", _ => "Repeat off" };
        if (repeat.Tag is not int previousRepeat || previousRepeat != media.Repeat)
        {
            repeat.Tag = media.Repeat;
            repeat.Content = WidgetVisuals.Icon(media.Repeat == 1 ? "repeat-one" : "repeat", 18);
        }
        words.IsEnabled = hasLyrics;
        words.Visibility = hasLyrics ? Visibility.Visible : Visibility.Collapsed;
        words.ToolTip = manager.Data.TrackLyrics.Synced ? "Show synced lyrics" : "Show lyrics · timing unavailable";
        words.Opacity = displayingLyrics ? 1 : .65;
        expand.IsEnabled = window != null && !preview;
        WidgetVisuals.UpdateIcon(expand,expanded ? "collapse" : "expand",16);
        UpdateProgress();
        if (media.Playing && media.Available && !preview && IsVisible && !manager.Snoozed) progressTimer.Start(); else progressTimer.Stop();
    }
    void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) { if (IsVisible) Update(); else progressTimer.Stop(); }
    void UpdateProgress()
    {
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
        lyrics.UpdatePosition(media.Position);
    }
    void LayoutSides()
    {
        var reveal = (double)GetValue(SideRevealProperty);
        var width = Math.Min(56, controls.ActualWidth * .16) * reveal;
        controls.ColumnDefinitions[0].Width = controls.ColumnDefinitions[4].Width = new GridLength(Math.Max(0, width));
        shuffleSlide.X = (1 - reveal) * 20;
        repeatSlide.X = -(1 - reveal) * 20;
    }
    void UpdateSides()
    {
        var showShuffle = media.Available && media.CanShuffle && !preview;
        var showRepeat = media.Available && media.CanRepeat && !preview;
        shuffle.IsEnabled = shuffle.IsHitTestVisible = showShuffle;
        repeat.IsEnabled = repeat.IsHitTestVisible = showRepeat;
        if (shownShuffle == showShuffle && shownRepeat == showRepeat) return;
        var animate = shownShuffle != null && SystemParameters.ClientAreaAnimation && !manager.Snoozed && IsVisible;
        shownShuffle = showShuffle;
        shownRepeat = showRepeat;
        var duration = TimeSpan.FromMilliseconds(220);
        DoubleAnimation Transition(double goal) => new(goal, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        var target = showShuffle || showRepeat ? 1d : 0d;
        if (animate)
        {
            BeginAnimation(SideRevealProperty, Transition(target), HandoffBehavior.SnapshotAndReplace);
            shuffle.BeginAnimation(OpacityProperty, Transition(showShuffle ? 1 : 0));
            repeat.BeginAnimation(OpacityProperty, Transition(showRepeat ? 1 : 0));
        }
        else
        {
            BeginAnimation(SideRevealProperty, null);
            SetValue(SideRevealProperty, target);
            shuffle.BeginAnimation(OpacityProperty, null);
            repeat.BeginAnimation(OpacityProperty, null);
            shuffle.Opacity = showShuffle ? 1 : 0;
            repeat.Opacity = showRepeat ? 1 : 0;
        }
    }
    static string Format(double seconds) => $"{(int)seconds / 60}:{(int)seconds % 60:00}";
}
