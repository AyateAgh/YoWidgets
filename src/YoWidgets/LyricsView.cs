using System.Windows.Media.Animation;
namespace YoWidgets;

// Line timing comes from the provider. No invented word timing or audio capture.
internal sealed class LyricsView : Grid
{
    readonly TextBlock caption = WidgetVisuals.Label("", 10, secondary: true);
    readonly TextBlock previous = WidgetVisuals.Label("", 15, secondary: true);
    readonly TextBlock current = WidgetVisuals.Label("", 23, true);
    readonly TextBlock next = WidgetVisuals.Label("", 15, secondary: true);
    readonly TextBlock plainText = WidgetVisuals.Label("", 17, true);
    readonly StackPanel timed = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly ScrollViewer plain;
    LyricResult result = LyricResult.Empty;
    int index = int.MinValue;
    public LyricsView()
    {
        RowDefinitions.Add(new() { Height = new GridLength(23) });
        RowDefinitions.Add(new());
        Children.Add(caption);
        foreach (var line in new[] { previous, current, next })
        {
            line.TextWrapping = TextWrapping.Wrap;
            line.TextTrimming = TextTrimming.CharacterEllipsis;
            line.Margin = new Thickness(0, 5, 0, 5);
            line.MaxHeight = ReferenceEquals(line, current) ? 84 : 42;
            timed.Children.Add(line);
        }
        plainText.TextWrapping = TextWrapping.Wrap;
        plain = new() { Content = plainText, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 8, 0) };
        Grid.SetRow(timed, 1); Grid.SetRow(plain, 1);
        Children.Add(timed); Children.Add(plain);
    }
    public void SetLyrics(LyricResult value, double seconds)
    {
        if (!ReferenceEquals(result, value))
        {
            result = value;
            index = int.MinValue;
            caption.Text = value.Synced ? "SYNCED LYRICS · LRCLIB" : "LYRICS · NO TIMESTAMPS";
            timed.Visibility = value.Synced ? Visibility.Visible : Visibility.Collapsed;
            plain.Visibility = value.Synced ? Visibility.Collapsed : Visibility.Visible;
            plainText.Text = value.Synced ? "" : value.Plain;
            plain.ScrollToTop();
        }
        UpdatePosition(seconds);
    }
    public void UpdatePosition(double seconds)
    {
        if (!IsVisible || !result.Synced) return;
        var selected = Lyrics.CurrentIndex(result.Lines, seconds);
        if (selected == index) return;
        index = selected;
        string Line(int i) => i >= 0 && i < result.Lines.Count ? result.Lines[i].Text : "";
        previous.Text = Line(index - 1);
        current.Text = Line(index) is { Length: > 0 } text ? text : "♪";
        next.Text = Line(index + 1);
        if (SystemParameters.ClientAreaAnimation)
            current.BeginAnimation(OpacityProperty, new DoubleAnimation(.55, 1, TimeSpan.FromMilliseconds(160)));
    }
}
