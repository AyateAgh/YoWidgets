using System.Windows.Controls.Primitives;
namespace YoWidgets;

internal sealed class ControlCenterView:Border
{
    internal ControlCenterView(WidgetManager manager,bool preview)
    {
        WidgetPanel.Style(this);Padding=new(12);
        var controls=manager.Data.Controls;var body=new Grid();body.RowDefinitions.Add(new(){Height=new GridLength(58)});body.RowDefinitions.Add(new());body.RowDefinitions.Add(new());
        var tiles=new UniformGrid{Columns=3};
        Button Tile(string icon,string title,Action click){var button=WidgetVisuals.IconButton(icon,title,click,42,true);button.Width=double.NaN;button.Height=52;button.Margin=new(3,0,3,0);var stack=Ui.Stack(WidgetVisuals.Icon(icon,19),Ui.Gap(4),WidgetVisuals.Label(title,10,true));stack.HorizontalAlignment=HorizontalAlignment.Center;button.Content=stack;return button;}
        tiles.Children.Add(Tile("wifi","Wi-Fi",()=>WidgetContentFactory.Open("ms-settings:network-wifi")));tiles.Children.Add(Tile("bluetooth","Bluetooth",()=>WidgetContentFactory.Open("ms-settings:bluetooth")));var mute=Tile("volume","Mute",()=>{if(!preview)controls.SetMute();});tiles.Children.Add(mute);body.Children.Add(tiles);
        bool updating=false;var volume=new Slider{Minimum=0,Maximum=100};var brightness=new Slider{Minimum=1,Maximum=100};
        System.Windows.Automation.AutomationProperties.SetName(volume,"System volume");System.Windows.Automation.AutomationProperties.SetName(brightness,"Display brightness");
        var volumeLabel=WidgetVisuals.Label("Sound",11,true);var brightnessLabel=WidgetVisuals.Label("Display",11,true);
        Border Row(string icon,TextBlock title,Slider slider,int row){slider.Height=24;slider.SetResourceReference(StyleProperty,"ControlCenterSlider");var line=new DockPanel();var symbol=WidgetVisuals.Icon(icon,15);symbol.Margin=new(0,0,8,0);line.Children.Add(symbol);line.Children.Add(title);var panel=new Border{Background=new SolidColorBrush(Color.FromArgb(24,255,255,255)),CornerRadius=new(13),Padding=new(10,7,10,5),Margin=new(0,6,0,0),Child=Ui.Stack(line,slider)};Grid.SetRow(panel,row);body.Children.Add(panel);return panel;}
        Row("volume",volumeLabel,volume,1);Row("sun",brightnessLabel,brightness,2);Child=body;
        volume.ValueChanged+=(_,e)=>{if(!updating&&!preview)controls.SetVolume(e.NewValue);};var debounce=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(150)};brightness.ValueChanged+=(_,_)=>{if(!updating&&!preview){debounce.Stop();debounce.Start();}};debounce.Tick+=async(_,_)=>{debounce.Stop();if(IsLoaded&&!manager.Snoozed)await controls.SetBrightnessAsync(brightness.Value);};
        void Refresh(){updating=true;try{if(!volume.IsMouseCaptureWithin)volume.Value=controls.Volume;if(!brightness.IsMouseCaptureWithin)brightness.Value=controls.Brightness??50;volumeLabel.Text=controls.Error.Length>0?"Sound unavailable":$"Sound · {controls.Volume:0}%"+(controls.Muted?" · muted":"");brightnessLabel.Text=controls.Brightness==null?"Display · unavailable":$"Display · {controls.Brightness:0}%";brightness.IsEnabled=!preview&&controls.Brightness!=null;volume.IsEnabled=!preview&&controls.Error.Length==0;mute.IsEnabled=!preview&&controls.Error.Length==0;mute.ToolTip=controls.Muted?"Unmute sound":"Mute sound";System.Windows.Automation.AutomationProperties.SetName(mute,controls.Muted?"Unmute sound":"Mute sound");if(mute.Content is StackPanel labels&&labels.Children.OfType<TextBlock>().LastOrDefault() is {} text)text.Text=controls.Muted?"Unmute":"Mute";}finally{updating=false;}}
        Loaded+=async(_,_)=>{controls.Changed+=Refresh;if(!preview)await controls.RefreshAsync();if(IsLoaded)Refresh();};Unloaded+=(_,_)=>{debounce.Stop();controls.Changed-=Refresh;};Refresh();
    }
}
