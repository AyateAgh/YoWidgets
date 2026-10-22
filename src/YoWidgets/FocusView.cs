using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
namespace YoWidgets;

internal sealed class FocusView:Border
{
    readonly WidgetManager manager;readonly TextBlock time=WidgetVisuals.Label("25:00",30,true),status=WidgetVisuals.Label("Do Not Disturb",11);
    readonly Button action;readonly Button duration;readonly FrameworkElement play,stop;
    bool? running;Popup? popup;GlassPopupMotion? popupMotion;
    internal FocusView(WidgetManager manager,bool preview=false)
    {
        this.manager=manager;Padding=new Thickness(4);
        var body=new Grid();body.RowDefinitions.Add(new());body.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var top=new Grid();top.ColumnDefinitions.Add(new(){Width=new GridLength(46)});top.ColumnDefinitions.Add(new());top.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        var moon=new Border{Width=38,Height=38,CornerRadius=new(19),Background=new SolidColorBrush(Color.FromArgb(45,195,174,255)),Child=WidgetVisuals.Icon("moon",21),VerticalAlignment=VerticalAlignment.Center};top.Children.Add(moon);
        var title=Ui.Stack(WidgetVisuals.Label("Focus",16,true),status);Grid.SetColumn(title,1);title.VerticalAlignment=VerticalAlignment.Center;top.Children.Add(title);
        Grid.SetColumn(time,2);time.Margin=new Thickness(6,0,0,0);time.FontFamily=Ui.DisplayFont;top.Children.Add(time);body.Children.Add(top);
        var footer=new Grid{Margin=new Thickness(0,10,0,0)};footer.ColumnDefinitions.Add(new());footer.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        duration=WidgetVisuals.Pill("25 min",()=>OpenDuration());WidgetVisuals.GlassButton(duration);duration.HorizontalAlignment=HorizontalAlignment.Left;footer.Children.Add(duration);
        action=WidgetVisuals.IconButton("play","Start focus",()=>{try{if(manager.Data.FocusEnd!=null)manager.Data.StopFocus();else manager.Data.StartFocus(Math.Clamp(manager.Settings.FocusMinutes,1,1440));}catch(InvalidOperationException){}Refresh();},38,true);
        WidgetVisuals.GlassButton(action);
        play=WidgetVisuals.Icon("play",18);stop=WidgetVisuals.Icon("square",18);var icons=new Grid();icons.Children.Add(play);icons.Children.Add(stop);action.Content=icons;action.IsEnabled=!preview;Grid.SetColumn(action,1);footer.Children.Add(action);Grid.SetRow(footer,1);body.Children.Add(footer);Child=body;
        Loaded+=(_,_)=>{manager.Data.Changed+=Refresh;Refresh();};Unloaded+=(_,_)=>{manager.Data.Changed-=Refresh;popupMotion?.Close();};Refresh();
        SizeChanged+=(_,_)=>time.FontSize=ActualWidth<245?24:30;
    }
    void Refresh()
    {
        var active=manager.Data.FocusEnd is {} end;var minutes=Math.Clamp(manager.Settings.FocusMinutes,1,1440);
        var seconds=active?Math.Max(0,(manager.Data.FocusEnd!.Value-DateTimeOffset.Now).TotalSeconds):minutes*60;
        time.Text=$"{(int)seconds/60}:{(int)seconds%60:00}";
        status.Text=manager.Data.Focus.Error.Length>0?"Check Windows settings":active?(manager.Settings.FocusQuietAppsEnabled?"Notifications & calls quiet":"Do Not Disturb on"):(manager.Settings.FocusQuietAppsEnabled?"Notifications & calls":"Do Not Disturb");status.ToolTip=manager.Data.Focus.Error.Length>0?manager.Data.Focus.Error:manager.Settings.FocusQuietAppsEnabled?"Starting Focus quiets notifications and selected call apps. Music players and browsers keep their sound.":"Windows alarms and notification exceptions may still apply.";
        if(duration.Content is TextBlock label)label.Text=minutes+" min";duration.IsEnabled=!active;
        var caption=active?"End focus":"Start focus";action.ToolTip=caption;System.Windows.Automation.AutomationProperties.SetName(action,caption);
        if(running==active)return;var animate=running!=null&&SystemParameters.ClientAreaAnimation&&!SystemParameters.HighContrast;running=active;
        void Show(FrameworkElement icon,bool visible){icon.BeginAnimation(OpacityProperty,null);if(animate)icon.BeginAnimation(OpacityProperty,new DoubleAnimation(visible?0:1,visible?1:0,TimeSpan.FromMilliseconds(180)));else icon.Opacity=visible?1:0;var scale=new ScaleTransform(1,1);icon.RenderTransformOrigin=new(.5,.5);icon.RenderTransform=scale;if(animate){var motion=new DoubleAnimation(visible?.7:1,visible?1:.7,TimeSpan.FromMilliseconds(180)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};scale.BeginAnimation(ScaleTransform.ScaleXProperty,motion);scale.BeginAnimation(ScaleTransform.ScaleYProperty,motion);}}
        Show(play,!active);Show(stop,active);
    }
    void OpenDuration()
    {
        if(popup?.IsOpen==true){popupMotion?.Close();return;}
        var body=new StackPanel();body.Children.Add(WidgetVisuals.Label("Focus duration",15,true));body.Children.Add(Ui.Gap(12));
        var input=new TextBox{Text=Math.Clamp(manager.Settings.FocusMinutes,1,1440).ToString(),FontSize=22,Foreground=Brushes.White,Background=new SolidColorBrush(Color.FromArgb(28,255,255,255)),BorderBrush=Brushes.Transparent,Padding=new(10,7,10,7)};System.Windows.Automation.AutomationProperties.SetName(input,"Focus minutes");body.Children.Add(input);
        var presets=new UniformGrid{Columns=5,Margin=new Thickness(0,8,0,10)};foreach(var value in new[]{5,15,25,45,60})presets.Children.Add(WidgetVisuals.Pill(value.ToString(),()=>input.Text=value.ToString()));body.Children.Add(presets);
        var silence=new CheckBox{Content="Quiet call apps",IsChecked=manager.Settings.FocusQuietAppsEnabled,Foreground=Brushes.White,Margin=new(2,0,0,10),ToolTip="Quiet selected communication apps while music players keep playing. Restores each app when Focus ends."};silence.SetResourceReference(StyleProperty,"Switch");body.Children.Add(silence);
        var selected=new HashSet<string>(manager.Settings.FocusQuietApps??FocusAppAudio.DefaultApps,StringComparer.OrdinalIgnoreCase);var apps=new WrapPanel();
        foreach(var entry in new[]{("Telegram",new[]{"Telegram"}),("Discord",new[]{"Discord","DiscordPTB","DiscordCanary"}),("WhatsApp",new[]{"WhatsApp"}),("Teams",new[]{"ms-teams","Teams"}),("Signal",new[]{"Signal"}),("Slack",new[]{"Slack"}),("Zoom",new[]{"Zoom"}),("Skype",new[]{"Skype"}),("Viber",new[]{"Viber"})}){var option=new CheckBox{Content=entry.Item1,IsChecked=entry.Item2.Any(selected.Contains),Foreground=Brushes.White,Width=120,Margin=new(0,5,0,5)};option.Checked+=(_,_)=>{foreach(var name in entry.Item2)selected.Add(name);};option.Unchecked+=(_,_)=>{foreach(var name in entry.Item2)selected.Remove(name);};apps.Children.Add(option);}
        body.Children.Add(new Expander{Header="Choose call apps",Content=apps,Foreground=Brushes.White,Margin=new(0,0,0,8)});
        var scope=WidgetVisuals.Label("Music keeps playing. Browser calls may share audio with music; use the browser's site settings for those.",10);scope.TextWrapping=TextWrapping.Wrap;scope.Margin=new(0,0,0,10);body.Children.Add(scope);
        var error=WidgetVisuals.Label("1–1440 minutes",11);body.Children.Add(error);
        var apply=WidgetVisuals.Pill("Set duration",()=>{if(!int.TryParse(input.Text,out var value)||value is <1 or >1440){error.Text="Choose 1–1440 minutes";input.Focus();return;}manager.Settings.FocusMinutes=value;manager.Settings.FocusQuietAppsEnabled=silence.IsChecked==true;manager.Settings.FocusQuietApps=selected.ToArray();manager.Save();manager.Data.NotifyWidgets();popupMotion?.Close();Refresh();});apply.Margin=new(0,10,0,0);body.Children.Add(apply);
        body.Children.Add(WidgetVisuals.Pill("Windows notification settings",()=>WidgetContentFactory.Open("ms-settings:notifications")));
        var card=new Border{Width=290,Child=body,Padding=new(16)};WidgetPanel.Style(card);popup=new Popup{Child=card,PlacementTarget=duration,Placement=PlacementMode.Bottom,VerticalOffset=8};popupMotion=new(popup,card,duration,true);popup.Opened+=(_,_)=>{input.Focus();input.SelectAll();};popup.IsOpen=true;
    }
}

internal static class WidgetPanel
{
    internal static void Style(Border panel)
    {
        panel.CornerRadius=new(20);panel.Background=new SolidColorBrush(Color.FromArgb(200,22,24,29));panel.BorderBrush=new SolidColorBrush(Color.FromArgb(24,255,255,255));panel.BorderThickness=new(1);
        panel.Resources["WidgetInk"]=Brushes.White;panel.Resources["WidgetSecondaryOpacity"]=.78;
    }
}
