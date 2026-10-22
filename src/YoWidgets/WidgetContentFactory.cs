using System.Windows.Controls.Primitives;
namespace YoWidgets;

internal static class WidgetContentFactory
{
    static Brush White => Brushes.White; static Brush Faded => new SolidColorBrush(Color.FromArgb(230, 245, 249, 255));
    static TextBlock T(string text, double size = 14, bool bold = false)
    {
        var t = WidgetVisuals.Label(text, size, bold);
        return t;
    }
    static Button Small(string text, Action action)
    {
        return WidgetVisuals.Pill(text, action);
    }
    public static FrameworkElement Create(WidgetManager m, WidgetState s, WidgetWindow? window = null, bool preview = false)
    {
        return s.Kind switch
        {
            "clock" => WidgetCards.Clock(m, s),
            "calendar" => WidgetCards.Calendar(m),
            "weather" => WidgetCards.Weather(m, s),
            "music" => Music(m, window, preview),
            "photo" => Photo(m, s),
            "focus" => Focus(m),
            "quick" => Quick(m, preview),
            "notifications" => Notifications(m),
            "assistant" => Assistant(m, preview, window),
            _ => T("Widget unavailable")
        };
    }
    static void Subscribe(FrameworkElement root, DataServices data, Action refresh)
    {
        root.Loaded += (_, _) => { data.Changed += refresh; refresh(); };
        root.Unloaded += (_, _) => data.Changed -= refresh;
    }
    static FrameworkElement Music(WidgetManager m, WidgetWindow? window, bool preview) => new MusicView(m, window, preview);
    static string Format(double sec) => $"{(int)sec / 60}:{(int)sec % 60:00}";
    static FrameworkElement Photo(WidgetManager m, WidgetState s)
    {
        var image = new Image { Stretch = Stretch.UniformToFill };
        var label = T("Your photo", 13);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        var p = new Grid();
        p.SizeChanged += (_, _) => p.Clip = new RectangleGeometry(new Rect(0, 0, p.ActualWidth, p.ActualHeight), 18, 18);
        p.Children.Add(image);
        p.Children.Add(label);
        void Load()
        {
            if (s.ImagePath != null && OwnedPaths.IsInside(App.DataRoot, s.ImagePath) && File.Exists(s.ImagePath))
                try
                {
                    var b = new BitmapImage();
                    b.BeginInit();
                    b.UriSource = new(s.ImagePath);
                    b.CacheOption = BitmapCacheOption.OnLoad;
                    b.DecodePixelWidth = 720;
                    b.EndInit();
                    b.Freeze();
                    image.Source = b;
                    label.Visibility = Visibility.Collapsed;
                }
                catch { label.Text = "Photo unavailable. Choose another."; }
        }
        var choose = Small("Choose photo", () => { var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp" }; if (dialog.ShowDialog() != true) return; try { var dir = Path.Combine(App.DataRoot, "images"); Directory.CreateDirectory(dir); var path = Path.Combine(dir, Guid.NewGuid().ToString("N") + Path.GetExtension(dialog.FileName).ToLowerInvariant()); OwnedData.RegisterImage(Path.GetFileName(path)); File.Copy(dialog.FileName, path); s.ImagePath = path; m.Save(); Load(); } catch (Exception e) { label.Text = "Could not open photo"; App.Log("photo", e); } });
        choose.VerticalAlignment = VerticalAlignment.Bottom;
        choose.HorizontalAlignment = HorizontalAlignment.Center;
        p.Children.Add(choose);
        Load();
        return p;
    }
    static FrameworkElement Focus(WidgetManager m)
    {
        var title = T("Focus", 13, true);
        var timer = T("25:00", 44);
        timer.FontWeight = FontWeights.Medium;
        timer.FontFamily = Ui.DisplayFont;
        var desc = T("One thing at a time", 12);
        var actions = new UniformGrid { Columns = 3 };
        foreach (var button in new[] { WidgetVisuals.IconButton("play", "Start 25 minutes", () => m.Data.StartFocus(25), 30, true), WidgetVisuals.IconButton("stop", "Stop focus", m.Data.StopFocus, 30), WidgetVisuals.IconButton("moon", "Snooze · pause widgets", () => m.SetSnooze(true), 30) })
        {
            button.HorizontalAlignment = HorizontalAlignment.Center;
            actions.Children.Add(button);
        }
        var heading = new StackPanel { Orientation = Orientation.Horizontal };
        heading.Children.Add(WidgetVisuals.Icon("moon", 16));
        title.Margin = new Thickness(8, 0, 0, 0);
        heading.Children.Add(title);
        var p = new Grid();
        p.RowDefinitions.Add(new() { Height = new GridLength(18) });
        p.RowDefinitions.Add(new());
        p.RowDefinitions.Add(new() { Height = new GridLength(18) });
        p.RowDefinitions.Add(new() { Height = new GridLength(34) });
        p.Children.Add(heading);
        Grid.SetRow(timer, 1); p.Children.Add(timer);
        Grid.SetRow(desc, 2); p.Children.Add(desc);
        Grid.SetRow(actions, 3); p.Children.Add(actions);
        desc.ToolTip = "Local timer. Configure system Do Not Disturb in Windows.";
        void Update()
        {
            var left = m.Data.FocusEnd - DateTimeOffset.Now;
            timer.Text = left is TimeSpan t ? Format(Math.Max(0, t.TotalSeconds)) : "25:00";
            desc.Text = left != null ? "Focus in progress" : "Time for yourself";
        }
        Subscribe(p, m.Data, Update);
        return p;
    }
    static FrameworkElement Quick(WidgetManager m, bool preview)
    {
        var c = m.Data.Controls;
        var p = Ui.Stack(T("Controls", 14, true), Ui.Gap(12));
        var volume = new Slider { Minimum = 0, Maximum = 100, Margin = new Thickness(0, 7, 0, 8) };
        var brightness = new Slider { Minimum = 1, Maximum = 100, Margin = new Thickness(0, 7, 0, 8) };
        var volumeLabel = T("Volume", 12);
        var brightnessLabel = T("Brightness", 12);
        bool updating = false;
        volume.ValueChanged += (_, e) => { if (!updating && !preview) c.SetVolume(e.NewValue); };
        var debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        debounce.Tick += async (_, _) => { debounce.Stop(); if (!m.Snoozed && !preview) await c.SetBrightnessAsync(brightness.Value); };
        brightness.ValueChanged += (_, _) => { if (!updating && !preview) { debounce.Stop(); debounce.Start(); } };
        p.Children.Add(volumeLabel);
        p.Children.Add(volume);
        p.Children.Add(brightnessLabel);
        p.Children.Add(brightness);
        var row = new WrapPanel();
        row.Children.Add(Small("Sound / mute", c.SetMute));
        row.Children.Add(Small("Wi-Fi", () => Open("ms-settings:network-wifi")));
        row.Children.Add(Small("DND", () => Open("ms-settings:notifications")));
        p.Children.Add(row);
        void Update()
        {
            if (m.Snoozed)
            {
                debounce.Stop();
                return;
            }
            updating = true;
            if (!volume.IsMouseCaptureWithin)
                volume.Value = c.Volume;
            if (!brightness.IsMouseCaptureWithin)
                brightness.Value = c.Brightness ?? 50;
            volumeLabel.Text = $"Volume · {c.Volume:0}%" + (c.Muted ? " · mute" : "");
            brightnessLabel.Text = c.Brightness == null ? "Brightness unavailable on this display" : $"Brightness · {c.Brightness:0}%";
            brightness.IsEnabled = c.Brightness != null && !preview;
            volume.IsEnabled = !preview;
            updating = false;
        }
        p.Loaded += async (_, _) => { c.Changed += Update; if (!preview) await c.RefreshAsync(); Update(); };
        p.Unloaded += (_, _) => { debounce.Stop(); c.Changed -= Update; };
        return p;
    }
    internal static void Open(string uri)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (Exception e) { App.Log("launch-settings", e); }
    }
    static FrameworkElement Notifications(WidgetManager m)
    {
        var status = T("Notifications", 13, true);
        var list = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        var refresh = Small("Refresh", async () => await m.Data.Notifications.RefreshAsync(false));
        var p = new Grid();
        p.RowDefinitions.Add(new() { Height = GridLength.Auto });
        p.RowDefinitions.Add(new());
        p.RowDefinitions.Add(new() { Height = GridLength.Auto });
        p.Children.Add(status);
        var scroll = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1); p.Children.Add(scroll);
        Grid.SetRow(refresh, 2); p.Children.Add(refresh);
        refresh.HorizontalAlignment = HorizontalAlignment.Left;
        void Update()
        {
            status.Text = m.Data.Notifications.Status;
            status.ToolTip = status.Text;
            list.Children.Clear();
            foreach (var n in m.Data.Notifications.Entries.Take(3))
            {
                var source = T(n.App, 10);
                source.Opacity = .6;
                var message = T(n.Body, 11);
                message.TextWrapping = TextWrapping.Wrap;
                message.MaxHeight = 30;
                var body = Ui.Stack(source, Ui.Gap(3), T(n.Title, 12, true), Ui.Gap(2), message);
                list.Children.Add(new Border { Child = body, Padding = new Thickness(12, 9, 12, 9), Margin = new Thickness(0, 0, 0, 7), CornerRadius = new CornerRadius(14), Background = new SolidColorBrush(Color.FromArgb(14, 255, 255, 255)), BorderBrush = new SolidColorBrush(Color.FromArgb(16, 255, 255, 255)), BorderThickness = new Thickness(1) });
            }
            refresh.IsEnabled = m.Settings.NotificationsEnabled;
        }
        Subscribe(p, m.Data, Update);
        return p;
    }
    static FrameworkElement Assistant(WidgetManager m, bool preview, WidgetWindow? window) => new AssistantView(m, preview, window);
}
