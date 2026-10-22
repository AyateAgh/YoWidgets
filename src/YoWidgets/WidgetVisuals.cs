using System.Windows.Shapes;
namespace YoWidgets;

internal static class WidgetVisuals
{
    public static TextBlock Label(string text, double size = 13, bool strong = false, bool secondary = false)
    {
        var label = new TextBlock { Text = text, FontSize = size, FontFamily = Ui.BodyFont, FontWeight = strong ? FontWeights.SemiBold : FontWeights.Medium, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap, VerticalAlignment = VerticalAlignment.Center };
        label.SetResourceReference(TextBlock.ForegroundProperty, "WidgetInk");
        if (secondary)
            label.SetResourceReference(UIElement.OpacityProperty, "WidgetSecondaryOpacity");
        return label;
    }
    public static void Tone(bool light, bool live = false)
    {
        System.Windows.Application.Current.Resources["AssistantInstructionInk"]=new SolidColorBrush(light?Color.FromRgb(18,76,168):Color.FromRgb(120,173,255));
        System.Windows.Application.Current.Resources["WidgetInk"] = light ? new SolidColorBrush(Color.FromRgb(25, 32, 43)) : Brushes.White;
        System.Windows.Application.Current.Resources["WidgetSecondaryOpacity"] = live ? .9 : .65;
        System.Windows.Application.Current.Resources["WidgetAccent"] = new SolidColorBrush(Color.FromRgb(255, 153, 147));
        System.Windows.Application.Current.Resources["AssistantButtonInk"] = light ? Brushes.White : Brushes.Black;
        System.Windows.Application.Current.Resources["AssistantCodeInk"] = light ? new SolidColorBrush(Color.FromRgb(20,73,134)) : new SolidColorBrush(Color.FromRgb(190,219,255));
        System.Windows.Application.Current.Resources["AssistantInset"] = new SolidColorBrush(Color.FromArgb(25, light ? (byte)255 : (byte)0, light ? (byte)255 : (byte)0, light ? (byte)255 : (byte)0));
        System.Windows.Application.Current.Resources["AssistantLine"] = new SolidColorBrush(Color.FromArgb(35,255,255,255));
        System.Windows.Application.Current.Resources["AssistantBubble"] = new SolidColorBrush(Color.FromArgb(55,255,255,255));
    }
    static string IconName(string name) => name switch { "stop"=>"square", "previous"=>"skip-back", "next"=>"skip-forward", "repeat-one"=>"repeat-1", "lyrics"=>"captions", "expand"=>"maximize", "collapse"=>"minimize", "volume"=>"volume-2", "photo"=>"image", "send"=>"arrow-up", "microphone"=>"mic", "new-chat"=>"plus", "retry"=>"rotate-cw", "sparkle"=>"sparkles", "close"=>"x", ""=>"minus", _=>name };
    public static FrameworkElement Icon(string name,double size=20,string ink="WidgetInk") => new PremiumIcon(IconName(name),size,ink);
    public static void UpdateIcon(ContentControl target,string name,double size=20,string ink="WidgetInk")
    { if(target.Content is not PremiumIcon current || !current.Matches(IconName(name),size,ink)) target.Content=Icon(name,size,ink); }

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
        content.SetBinding(FrameworkElement.HorizontalAlignmentProperty,new System.Windows.Data.Binding("HorizontalContentAlignment") { RelativeSource=new(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        content.SetBinding(FrameworkElement.VerticalAlignmentProperty,new System.Windows.Data.Binding("VerticalContentAlignment") { RelativeSource=new(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.AppendChild(content);
        template.VisualTree = border;
        button.Template = template;
        button.HorizontalContentAlignment=HorizontalAlignment.Center;button.VerticalContentAlignment=VerticalAlignment.Center;
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
        var symbol = label switch { "Wi-Fi"=>"wifi", "DND"=>"bell", "Sound / mute"=>"volume", "Refresh"=>"refresh-cw", "Choose photo"=>"photo", _=>null };
        if(symbol!=null) { var row=new StackPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(9,0,9,0) }; var icon=Icon(symbol,14); icon.Margin=new Thickness(0,0,6,0); text.Margin=new Thickness(0); row.Children.Add(icon); row.Children.Add(text); button.Content=row; }
        else button.Content=text;
        return button;
    }
}
