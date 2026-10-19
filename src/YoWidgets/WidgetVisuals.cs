using System.Windows.Shapes;
namespace YoWidgets;

internal static class WidgetVisuals
{
    public static TextBlock Label(string text, double size = 13, bool strong = false, bool secondary = false)
    {
        var label = new TextBlock { Text = text, FontSize = size, FontFamily = new("Segoe UI Variable Text"), FontWeight = strong ? FontWeights.SemiBold : FontWeights.Normal, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap, VerticalAlignment = VerticalAlignment.Center };
        label.SetResourceReference(TextBlock.ForegroundProperty, "WidgetInk");
        if (secondary)
            label.Opacity = .65;
        return label;
    }
    public static void Tone(bool light)
    {
        System.Windows.Application.Current.Resources["WidgetInk"] = light ? new SolidColorBrush(Color.FromRgb(25, 32, 43)) : Brushes.White;
    }
    public static FrameworkElement Icon(string name, double size = 20)
    {
        var (data, filled) = name switch
        {
            "play" => ("M8 4.5L21 12 8 19.5Z", true),
            "pause" => ("M6 4H10V20H6Z M14 4H18V20H14Z", true),
            "stop" => ("M6 5H18Q19 5 19 6V18Q19 19 18 19H6Q5 19 5 18V6Q5 5 6 5Z", true),
            "previous" => ("M4 5H6V19H4Z M20 5V19L7 12Z", true),
            "next" => ("M18 5H20V19H18Z M4 5V19L17 12Z", true),
            "shuffle" => ("M3 6H5C10 6 12 18 17 18H21 M18 15L21 18 18 21 M3 18H5C7 18 8 16 9 14 M14 8C15 6 16 6 21 6 M18 3L21 6 18 9", false),
            "repeat" => ("M5 7H18L15 4 M18 7L15 10 M19 17H6L9 20 M6 17L9 14 M3 11V10C3 8 4 7 5 7 M21 13V14C21 16 20 17 19 17", false),
            "lyrics" => ("M5 4H19Q21 4 21 6V16Q21 18 19 18H12L7 22V18H5Q3 18 3 16V6Q3 4 5 4Z M7 9H17 M7 13H14", false),
            "expand" => ("M8 3H3V8 M16 3H21V8 M3 16V21H8 M21 16V21H16", false),
            "collapse" => ("M3 8H8V3 M21 8H16V3 M8 21V16H3 M16 21V16H21", false),
            "moon" => ("M20 15.5A9 9 0 0 1 8.5 4 9 9 0 1 0 20 15.5Z", true),
            "sun" => ("M12 7A5 5 0 1 0 12 17A5 5 0 1 0 12 7 M12 1V3 M12 21V23 M1 12H3 M21 12H23 M4 4L6 6 M18 18L20 20 M4 20L6 18 M18 6L20 4", false),
            "volume" => ("M3 9H7L12 5V19L7 15H3Z M16 8Q21 12 16 16 M18 4Q27 12 18 20", false),
            "photo" => ("M5 3H19Q21 3 21 5V19Q21 21 19 21H5Q3 21 3 19V5Q3 3 5 3Z M3 17L9 11 13 15 17 10 21 15 M9 7H9.1", false),
            _ => ("M6 12H18", false)
        };
        var path = new System.Windows.Shapes.Path { Data = Geometry.Parse(data), StrokeThickness = 1.6, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, Stretch = Stretch.Uniform, Width = 24, Height = 24 };
        path.SetResourceReference(filled ? Shape.FillProperty : Shape.StrokeProperty, "WidgetInk");
        return new Viewbox { Width = size, Height = size, Child = path, IsHitTestVisible = false };
    }
    public static Button IconButton(string icon, string label, Action action, double diameter = 34, bool prominent = false)
    {
        var button = new Button { Content = Icon(icon, diameter >= 44 ? 24 : 18), Width = diameter, Height = diameter, Padding = new Thickness(0), Margin = new Thickness(0), Cursor = Cursors.Hand, ToolTip = label, Background = new SolidColorBrush(Color.FromArgb(prominent ? (byte)38 : (byte)0, 255, 255, 255)), BorderBrush = Brushes.Transparent };
        System.Windows.Automation.AutomationProperties.SetName(button, label);
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(diameter / 2));
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);
        template.VisualTree = border;
        button.Template = template;
        var style = new Style(typeof(Button));
        button.ClearValue(Control.BackgroundProperty);
        button.ClearValue(Control.BorderBrushProperty);
        style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(prominent ? (byte)38 : (byte)0, 255, 255, 255))));
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(35, 255, 255, 255))));
        style.Triggers.Add(hover);
        var pressed = new Trigger { Property = System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(UIElement.OpacityProperty, .55));
        style.Triggers.Add(pressed);
        var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
        focus.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.White));
        style.Triggers.Add(focus);
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, .25));
        style.Triggers.Add(disabled);
        button.Style = style;
        button.Click += (_, _) => action();
        return button;
    }
    public static Button Pill(string label, Action action)
    {
        var button = IconButton("", label, action, 32, true);
        button.Width = double.NaN;
        button.Height = 30;
        button.Margin = new Thickness(2);
        button.Padding = new Thickness(0);
        var text = Label(label, 11, true);
        text.Margin = new Thickness(10, 0, 10, 0);
        button.Content = text;
        return button;
    }
}
