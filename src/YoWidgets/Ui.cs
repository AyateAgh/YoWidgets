namespace YoWidgets;

internal static class Ui
{
    public static readonly FontFamily BodyFont = new("Segoe UI Variable Text, Segoe UI");
    public static readonly FontFamily DisplayFont = new("Segoe UI Variable Display, Segoe UI");
    public static Brush Brush(string key) => (Brush)System.Windows.Application.Current.Resources[key];
    public static TextBlock Text(string value, double size = 14, string role = "Text", FontWeight? weight = null)
    {
        var t = new TextBlock { Text = value, FontFamily = size >= 24 ? DisplayFont : BodyFont, FontSize = size, FontWeight = weight ?? FontWeights.Medium, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        t.SetResourceReference(TextBlock.ForegroundProperty, role);
        return t;
    }
    public static Button Button(string label, Action action, bool primary = false)
    {
        var b = new Button { Content = label, Padding = new Thickness(16, 10, 16, 10), Margin = new Thickness(0, 0, 8, 0), MinHeight = 38, Cursor = Cursors.Hand };
        b.SetResourceReference(FrameworkElement.StyleProperty, primary ? "PrimaryButton" : "QuietButton");
        var symbol = label switch { "Snooze"=>"moon", "Disable all" or "Disable this pack"=>"power", "All packs"=>"chevron-left", "Add another"=>"plus", "Center widgets on this screen"=>"layout-dashboard", "Save & refresh"=>"refresh-cw", "Request access"=>"bell", "Remove saved key"=>"trash", "Check connection"=>"sparkles", "Quit YoWidgets"=>"power", _=>null };
        if(symbol!=null) { var row=new StackPanel { Orientation=Orientation.Horizontal }; var icon=WidgetVisuals.Icon(symbol,16,primary ? "PrimaryInk" : "Text"); icon.Margin=new Thickness(0,0,8,0); row.Children.Add(icon); var caption=Text(label,13,primary ? "PrimaryInk" : "Text"); row.Children.Add(caption); b.Content=row; }
        System.Windows.Automation.AutomationProperties.SetName(b,label);
        b.Click += (_, _) => action();
        return b;
    }
    public static Border Panel(UIElement child, double padding = 20) => new() { Child = child, Padding = new Thickness(padding), CornerRadius = new CornerRadius(20), Background = Brush("Panel"), BorderBrush = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255)), BorderThickness = new Thickness(1) };
    public static StackPanel Stack(params UIElement[] children)
    {
        var p = new StackPanel();
        foreach (var c in children)
            p.Children.Add(c);
        return p;
    }
    public static Border Gap(double height) => new() { Height = height };
    public static TextBox Input(string value)
    {
        var t = new TextBox { Text = value, Padding = new Thickness(12, 10, 12, 10), FontSize = 14, Background = Brush("Field"), Foreground = Brush("Text"), BorderBrush = Brush("Line"), BorderThickness = new Thickness(1), CaretBrush = Brush("Accent") };
        return t;
    }
    public static CheckBox Toggle(string label, bool value, Action<bool> changed)
    {
        var c = new CheckBox { Content = label, IsChecked = value, Foreground = Brush("Text"), VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 7, 0, 7), Cursor = Cursors.Hand };
        c.Style = (Style)System.Windows.Application.Current.Resources["Switch"];
        c.Checked += (_, _) => changed(true);
        c.Unchecked += (_, _) => changed(false);
        return c;
    }
    public static Slider Slider(double min, double max, double value, Action<double> changed)
    {
        var s = new Slider { Minimum = min, Maximum = max, Value = value, Margin = new Thickness(0, 12, 0, 16) };
        s.ValueChanged += (_, e) => changed(e.NewValue);
        return s;
    }
    public static void Theme()
    {
        GlassTips.Install();
        var r = System.Windows.Application.Current.Resources;
        r["PrimaryInk"] = new SolidColorBrush(Color.FromRgb(16,16,16));
        r.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/YoWidgets;component/Controls.xaml") });
        foreach (var (key, color) in ThemeTokens.Colors)
            r[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        r[typeof(TextBlock)] = new Style(typeof(TextBlock)) { Setters = { new Setter(TextBlock.FontFamilyProperty, BodyFont), new Setter(TextBlock.FontWeightProperty, FontWeights.Medium) } };
        foreach (var type in new[] { typeof(Window), typeof(TextBox), typeof(PasswordBox), typeof(CheckBox), typeof(ComboBox), typeof(ContextMenu), typeof(MenuItem), typeof(ToolTip) })
            r[type] = new Style(type, type == typeof(ComboBox) ? (Style)r["PremiumComboBox"] : null) { Setters = { new Setter(Control.FontFamilyProperty, BodyFont), new Setter(Control.FontWeightProperty, FontWeights.Medium) } };
        var quiet = new Style(typeof(Button));
        quiet.Setters.Add(new Setter(Control.FontFamilyProperty, BodyFont));
        quiet.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        quiet.Setters.Add(new Setter(Control.ForegroundProperty, Brush("Text")));
        quiet.Setters.Add(new Setter(Control.BackgroundProperty, Brush("Panel")));
        quiet.Setters.Add(new Setter(Control.BorderBrushProperty, Brush("Line")));
        quiet.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        quiet.Setters.Add(new Setter(Control.FontSizeProperty, 13d));
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetBinding(FrameworkElement.MarginProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = new(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        template.VisualTree = border;
        quiet.Setters.Add(new Setter(Control.TemplateProperty, template));
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Control.BackgroundProperty, Brush("Hover")));
        quiet.Triggers.Add(hover);
        var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
        focus.Setters.Add(new Setter(Control.BorderBrushProperty, Brush("Accent")));
        quiet.Triggers.Add(focus);
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, .4));
        quiet.Triggers.Add(disabled);
        r["QuietButton"] = quiet;
        var primary = new Style(typeof(Button), quiet);
        primary.Setters.Add(new Setter(Control.BackgroundProperty, Brush("Accent")));
        primary.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromRgb(13, 28, 48))));
        r["PrimaryButton"] = primary;
    }
}
