using System.Windows.Data;
namespace YoWidgets;

internal sealed class ProviderOption : StackPanel
{
    public static readonly DependencyProperty ProviderProperty=DependencyProperty.Register(nameof(Provider),typeof(string),typeof(ProviderOption),new PropertyMetadata("",Changed));
    public string Provider { get=>(string)GetValue(ProviderProperty); set=>SetValue(ProviderProperty,value); }
    public ProviderOption() { Orientation=Orientation.Horizontal; }
    static void Changed(DependencyObject d,DependencyPropertyChangedEventArgs e)
    {
        var row=(ProviderOption)d; row.Children.Clear(); var name=e.NewValue as string ?? "";
        if(!AssistantProtocol.Providers.Contains(name))return;
        row.Children.Add(new PremiumIcon(name.ToLowerInvariant(),20,"Text",true) { Margin=new Thickness(0,0,10,0) });
        row.Children.Add(Ui.Text(name,13));
    }
    public static DataTemplate Template()
    {
        var element=new FrameworkElementFactory(typeof(ProviderOption)); element.SetBinding(ProviderProperty,new Binding()); return new DataTemplate { VisualTree=element };
    }
}
