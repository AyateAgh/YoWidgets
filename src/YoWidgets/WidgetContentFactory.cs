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
            "focus" => new FocusView(m,preview),
            "quick" => new ControlCenterView(m,preview),
            "notifications" => new NotificationsView(m),
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
    internal static void Open(string uri)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (Exception e) { App.Log("launch-settings", e); }
    }
    static FrameworkElement Assistant(WidgetManager m, bool preview, WidgetWindow? window) => new AssistantView(m, preview, window);
}
