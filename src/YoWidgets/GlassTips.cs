using System.Windows.Controls.Primitives;
namespace YoWidgets;

internal static class GlassTips
{
    static bool installed;static Popup? popup;static GlassPopupMotion? motion;static FrameworkElement? anchor;static readonly DispatcherTimer timeout=new(){Interval=TimeSpan.FromSeconds(6)};
    internal static void Install()
    {
        if(installed)return;installed=true;ToolTipService.InitialShowDelayProperty.OverrideMetadata(typeof(FrameworkElement),new FrameworkPropertyMetadata(500));ToolTipService.BetweenShowDelayProperty.OverrideMetadata(typeof(FrameworkElement),new FrameworkPropertyMetadata(150));
        EventManager.RegisterClassHandler(typeof(FrameworkElement),ToolTipService.ToolTipOpeningEvent,new ToolTipEventHandler(Opening));timeout.Tick+=(_,_)=>Close();
    }
    internal static string Short(string value,bool path=false)
    {var text=System.Text.RegularExpressions.Regex.Replace(value,@"\s+"," ").Trim();var max=path?600:100;return text.Length>max?text[..(max-1)].TrimEnd()+"…":text;}
    static void Opening(object sender,ToolTipEventArgs e)
    {
        if(e.Handled||sender is not FrameworkElement element)return;var raw=element.ToolTip;var value=raw as string??(raw as ToolTip)?.Content as string;if(string.IsNullOrWhiteSpace(value))return;e.Handled=true;Show(element,value,raw is ToolTip);
    }
    internal static void Show(FrameworkElement element,string value,bool path=false)
    {
        if(popup!=null)popup.IsOpen=false;anchor=element;var text=Ui.Text(Short(value,path),11);text.MaxWidth=path?390:240;text.TextWrapping=TextWrapping.Wrap;
        var card=new Border{Child=text,Padding=new Thickness(11,8,11,8),CornerRadius=new CornerRadius(12),Background=Ui.Brush("Panel"),BorderBrush=Ui.Brush("Line"),BorderThickness=new Thickness(1)};
        var current=new Popup{PlacementTarget=element,Placement=PlacementMode.Top,VerticalOffset=-7,Child=card,IsHitTestVisible=false};popup=current;motion=new GlassPopupMotion(current,card,element,true);
        element.MouseLeave+=Leave;element.PreviewMouseDown+=MouseDown;element.Unloaded+=Unloaded;
        current.Closed+=(_,_)=>{element.MouseLeave-=Leave;element.PreviewMouseDown-=MouseDown;element.Unloaded-=Unloaded;if(ReferenceEquals(popup,current)){popup=null;motion=null;anchor=null;timeout.Stop();}};
        current.IsOpen=true;timeout.Stop();timeout.Start();
    }
    static void Leave(object sender,MouseEventArgs e){if(anchor?.IsMouseOver!=true)Close();}
    static void MouseDown(object sender,MouseButtonEventArgs e)=>Close();
    static void Unloaded(object sender,RoutedEventArgs e)=>Close();
    internal static void Close(){timeout.Stop();motion?.Close();}
}
