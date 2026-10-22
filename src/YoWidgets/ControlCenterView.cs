using System.Windows.Controls.Primitives;
namespace YoWidgets;

internal sealed class ControlCenterView:Border
{
    internal ControlCenterView(WidgetManager manager,bool preview)
    {
        Padding=new(0);
        var controls=manager.Data.Controls;var body=new Grid();body.RowDefinitions.Add(new(){Height=new GridLength(78)});body.RowDefinitions.Add(new());body.RowDefinitions.Add(new());
        var tiles=new UniformGrid{Columns=3};
        Button Tile(string icon,string title,Action click){var button=WidgetVisuals.IconButton(icon,title,click,50,true);button.Margin=new(0);button.Content=WidgetVisuals.Icon(icon,21);WidgetVisuals.GlassButton(button);return button;}
        void AddTile(Button button,string title){var caption=WidgetVisuals.Label(title,10,true);caption.HorizontalAlignment=HorizontalAlignment.Center;var stack=Ui.Stack(button,Ui.Gap(4),caption);stack.HorizontalAlignment=HorizontalAlignment.Center;tiles.Children.Add(stack);}
        AddTile(Tile("wifi","Wi-Fi",()=>WidgetContentFactory.Open("ms-settings:network-wifi")),"Wi-Fi");AddTile(Tile("bluetooth","Bluetooth",()=>WidgetContentFactory.Open("ms-settings:bluetooth")),"Bluetooth");var mute=Tile("volume","Mute",()=>{if(!preview)controls.SetMute();});AddTile(mute,"Sound");body.Children.Add(tiles);
        bool updating=false;var volume=new WidgetSlider{Minimum=0,Maximum=100};var brightness=new WidgetSlider{Minimum=1,Maximum=100};
        System.Windows.Automation.AutomationProperties.SetName(volume,"System volume");System.Windows.Automation.AutomationProperties.SetName(brightness,"Display brightness");
        var volumeLabel=WidgetVisuals.Label("Sound",11,true);var brightnessLabel=WidgetVisuals.Label("Display",11,true);
        Border Row(string icon,TextBlock title,Slider slider,int row){slider.Height=32;slider.SetResourceReference(StyleProperty,"ControlCenterSlider");var line=new DockPanel();var symbol=WidgetVisuals.Icon(icon,15);symbol.Margin=new(0,0,8,0);line.Children.Add(symbol);line.Children.Add(title);var panel=new Border{CornerRadius=new(16),Padding=new(10,6,10,0),Margin=new(0,4,0,0),Child=Ui.Stack(line,slider)};panel.Background=WidgetVisuals.GlassFill();panel.BorderBrush=WidgetVisuals.GlassEdge();panel.BorderThickness=new(1);Grid.SetRow(panel,row);body.Children.Add(panel);return panel;}
        Row("volume",volumeLabel,volume,1);Row("sun",brightnessLabel,brightness,2);Child=body;
        var volumeWrites=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(40)};double? pendingVolume=null;
        void FlushVolume(){volumeWrites.Stop();if(pendingVolume is not {} value)return;pendingVolume=null;if(IsLoaded&&!manager.Snoozed)controls.SetVolume(value);}
        volume.ValueChanged+=(_,e)=>{if(!updating&&!preview){volumeLabel.Text=$"Sound · {e.NewValue:0}%";pendingVolume=e.NewValue;if(!volumeWrites.IsEnabled)volumeWrites.Start();}};volumeWrites.Tick+=(_,_)=>FlushVolume();volume.LostMouseCapture+=(_,_)=>FlushVolume();
        var debounce=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(150)};int brightnessWrites=0;
        brightness.ValueChanged+=(_,_)=>{if(!updating&&!preview){brightnessLabel.Text=$"Display · {brightness.Value:0}%";debounce.Stop();debounce.Start();}};
        debounce.Tick+=async(_,_)=>{debounce.Stop();if(!IsLoaded||manager.Snoozed)return;brightnessWrites++;try{await controls.SetBrightnessAsync(brightness.Value);}finally{brightnessWrites--;if(IsLoaded)Refresh();}};
        void Refresh(){updating=true;try{if(!volume.IsMouseCaptureWithin&&pendingVolume==null)volume.Value=controls.Volume;if(!brightness.IsMouseCaptureWithin&&!debounce.IsEnabled&&brightnessWrites==0)brightness.Value=controls.Brightness??50;volumeLabel.Text=controls.Error.Length>0?"Sound unavailable":$"Sound · {volume.Value:0}%"+(controls.Muted?" · muted":"");brightnessLabel.Text=controls.Brightness==null?"Display · unavailable":$"Display · {brightness.Value:0}%";brightness.IsEnabled=!preview&&controls.Brightness!=null;volume.IsEnabled=!preview&&controls.Error.Length==0;mute.IsEnabled=!preview&&controls.Error.Length==0;mute.ToolTip=controls.Muted?"Unmute sound":"Mute sound";System.Windows.Automation.AutomationProperties.SetName(mute,controls.Muted?"Unmute sound":"Mute sound");WidgetVisuals.UpdateIcon(mute,controls.Muted?"volume-x":"volume",21);}finally{updating=false;}}
        Loaded+=async(_,_)=>{controls.Changed+=Refresh;if(!preview)await controls.RefreshAsync();if(IsLoaded)Refresh();};Unloaded+=(_,_)=>{debounce.Stop();volumeWrites.Stop();pendingVolume=null;controls.Changed-=Refresh;};Refresh();
    }
}
