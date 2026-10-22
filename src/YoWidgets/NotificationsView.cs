namespace YoWidgets;

internal sealed class NotificationsView:Border
{
    internal NotificationsView(WidgetManager manager)
    {
        WidgetPanel.Style(this);Padding=new(13);var service=manager.Data.Notifications;
        var body=new Grid();body.RowDefinitions.Add(new(){Height=GridLength.Auto});body.RowDefinitions.Add(new());body.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var heading=new DockPanel();var icon=WidgetVisuals.Icon("bell",18);icon.Margin=new(0,0,8,0);heading.Children.Add(icon);heading.Children.Add(WidgetVisuals.Label("Notifications",14,true));body.Children.Add(heading);
        var list=new StackPanel();var scroll=new ScrollViewer{Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new(0,10,0,8)};Grid.SetRow(scroll,1);body.Children.Add(scroll);
        var actions=new WrapPanel();var connect=WidgetVisuals.Pill("Connect",async()=>{manager.Settings.NotificationsEnabled=true;manager.Save();service.Enable();await service.RefreshAsync(true);});connect.Visibility=service.HasIdentity?Visibility.Visible:Visibility.Collapsed;actions.Children.Add(connect);actions.Children.Add(WidgetVisuals.Pill("Refresh",async()=>await service.RefreshAsync(false)));actions.Children.Add(WidgetVisuals.Pill("Windows settings",()=>WidgetContentFactory.Open("ms-settings:notifications")));Grid.SetRow(actions,2);body.Children.Add(actions);Child=body;
        void Refresh(){list.Children.Clear();if(service.Entries.Count==0){var empty=WidgetVisuals.Label(service.Status,12);empty.TextWrapping=TextWrapping.Wrap;empty.Margin=new(4,7,4,7);list.Children.Add(empty);}else foreach(var entry in service.Entries.Take(5)){var source=WidgetVisuals.Label(entry.App+" · "+entry.Time.ToLocalTime().ToString("HH:mm"),10,true);source.Opacity=.75;var title=WidgetVisuals.Label(entry.Title,12,true);var text=WidgetVisuals.Label(entry.Body,11);text.TextWrapping=TextWrapping.Wrap;text.MaxHeight=34;list.Children.Add(new Border{CornerRadius=new(13),Padding=new(10,8,10,8),Margin=new(0,0,0,6),Background=new SolidColorBrush(Color.FromArgb(24,255,255,255)),Child=Ui.Stack(source,Ui.Gap(3),title,Ui.Gap(2),text)});}connect.ToolTip="Request Windows notification access";}
        Loaded+=(_,_)=>{service.Changed+=Refresh;if(manager.Settings.NotificationsEnabled){service.Enable();_=service.RefreshAsync(false);}Refresh();};Unloaded+=(_,_)=>service.Changed-=Refresh;Refresh();
    }
}
