using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
namespace YoWidgets;

internal sealed class CallOrb:ActiveVisual
{
    internal Func<double>? ReadLevel;
    double envelope;
    protected override void OnRender(DrawingContext dc)
    {
        var level=Math.Clamp(ReadLevel?.Invoke()??0,0,1);envelope+=(level-envelope)*(level>envelope?.5:.16);
        var size=Math.Min(ActualWidth,ActualHeight);if(size<1)return;
        var radius=size*(.30+envelope*.035);var center=new Point(ActualWidth/2,ActualHeight/2);
        if(!SystemParameters.HighContrast){
            var halo=new RadialGradientBrush();halo.GradientStops.Add(new(Color.FromArgb((byte)(28+envelope*25),154,111,245),0));halo.GradientStops.Add(new(Colors.Transparent,1));dc.DrawEllipse(halo,null,center,radius*1.6,radius*1.6);
            for(var i=0;i<3;i++){var phase=MotionAllowed?(Time*.35+i/3d)%1:i/3d;var color=AssistantSpectrum.Colors[(i+2)%5];dc.PushOpacity((1-phase)*(.12+envelope*.15));dc.DrawEllipse(null,new Pen(new SolidColorBrush(color),1),center,radius*(1+phase*.6),radius*(1+phase*.6));dc.Pop();}
            var r=radius*.84;
            dc.PushClip(new EllipseGeometry(center,r,r));
            var baseColor=new RadialGradientBrush{Center=new(.35,.22),GradientOrigin=new(.35,.22),RadiusX=.85,RadiusY=.85,GradientStops=new(){new(Color.FromRgb(160,183,255),0),new(Color.FromRgb(112,91,211),.52),new(Color.FromRgb(65,80,153),1)}};
            dc.DrawEllipse(baseColor,null,center,r,r);
            for(var i=0;i<AssistantSpectrum.Colors.Length;i++){
                var color=AssistantSpectrum.Colors[i];var angle=Time*.65+i*Math.PI*.4;var lens=new RadialGradientBrush{GradientStops=new(){new(color with{A=215},0),new(color with{A=0},1)}};
                dc.DrawEllipse(lens,null,new Point(center.X+Math.Cos(angle)*r*.6,center.Y+Math.Sin(angle)*r*.6),r*.9,r*.9);
            }
            var shine=new RadialGradientBrush{GradientStops=new(){new(Color.FromArgb(150,245,250,255),0),new(Colors.Transparent,1)}};dc.DrawEllipse(shine,null,new Point(center.X-r*.25,center.Y-r*.65),r*.65,r*.42);dc.Pop();
        }else dc.DrawEllipse(SystemColors.HighlightBrush,null,center,radius,radius);
    }
}

internal sealed class VoiceCallView:Border
{
    readonly WidgetManager manager;
    readonly Action closed;
    readonly TextBlock state=WidgetVisuals.Label("Connecting…",16);
    readonly TextBlock detail=WidgetVisuals.Label("",11);
    readonly TextBlock caption=WidgetVisuals.Label("",13);
    readonly TextBlock action=WidgetVisuals.Label("",11);
    readonly CallOrb orb=new(){Width=170,Height=156};
    readonly Button voices,mute,reconnect;
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(100)};
    readonly Func<string> context;
    readonly Func<string,string,string,string,string,LiveVoiceSession> createSession;
    readonly Action<string,string,IReadOnlyList<(string Role,string Text)>> transcript;
    LiveVoiceSession? session;
    string provider="",protectedKey="",identity="";
    bool leaving,committed;
    Popup? picker;
    GlassPopupMotion? pickerMotion;
    internal bool Calling=>session?.Active==true;
    internal VoiceCallView(WidgetManager manager,Action closed,Func<string> context,Action<string,string,IReadOnlyList<(string Role,string Text)>> transcript,Func<string,string,string,string,string,LiveVoiceSession>? createSession=null)
    {
        this.manager=manager;this.closed=closed;this.context=context;this.transcript=transcript;
        this.createSession=createSession??((p,m,v,k,c)=>new LiveVoiceSession(p,m,v,k,c));
        Padding=new Thickness(12,4,12,8);Background=Brushes.Transparent;Focusable=true;
        var grid=new Grid();grid.RowDefinitions.Add(new(){Height=GridLength.Auto});grid.RowDefinitions.Add(new());grid.RowDefinitions.Add(new(){Height=GridLength.Auto});Child=grid;
        var header=new DockPanel();var back=WidgetVisuals.IconButton("chevron-down","Back to chat",Leave,28);DockPanel.SetDock(back,Dock.Left);header.Children.Add(back);
        detail.HorizontalAlignment=HorizontalAlignment.Right;detail.VerticalAlignment=VerticalAlignment.Center;detail.Opacity=.7;header.Children.Add(detail);grid.Children.Add(header);
        var center=new StackPanel{VerticalAlignment=VerticalAlignment.Center};orb.HorizontalAlignment=HorizontalAlignment.Center;orb.ReadLevel=()=>session?.Level??0;center.Children.Add(orb);
        state.HorizontalAlignment=HorizontalAlignment.Center;state.FontWeight=FontWeights.Medium;AutomationProperties.SetLiveSetting(state,AutomationLiveSetting.Polite);center.Children.Add(state);
        caption.TextWrapping=TextWrapping.Wrap;caption.TextAlignment=TextAlignment.Center;caption.MaxHeight=52;caption.Margin=new Thickness(4,9,4,8);caption.Opacity=.8;center.Children.Add(caption);action.TextAlignment=TextAlignment.Center;action.TextTrimming=TextTrimming.CharacterEllipsis;action.Opacity=.7;action.Margin=new Thickness(2,0,2,4);center.Children.Add(action);Grid.SetRow(center,1);grid.Children.Add(center);
        voices=WidgetVisuals.IconButton("sliders-horizontal","Choose voice",OpenVoices,36);voices.Width=double.NaN;voices.Padding=new Thickness(11,4,11,4);
        mute=WidgetVisuals.IconButton("microphone","Mute microphone",()=>{session?.ToggleMute();Refresh();},36);
        var end=WidgetVisuals.IconButton("close","End call",Leave,38);end.Background=new SolidColorBrush(Color.FromArgb(65,248,99,148));
        reconnect=WidgetVisuals.Pill("Start call",()=>_ =Start());reconnect.Margin=new Thickness(0,0,6,0);
        var buttons=new DockPanel();DockPanel.SetDock(voices,Dock.Left);buttons.Children.Add(voices);var right=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};right.Children.Add(reconnect);right.Children.Add(mute);right.Children.Add(end);buttons.Children.Add(right);
        var bottom=new StackPanel();bottom.Children.Add(buttons);var hint=WidgetVisuals.Label("AI voice · Uses your chat API key",10);hint.TextAlignment=TextAlignment.Center;hint.TextWrapping=TextWrapping.Wrap;hint.ToolTip="Headphones recommended";hint.Opacity=.6;hint.Margin=new Thickness(0,10,0,0);bottom.Children.Add(hint);Grid.SetRow(bottom,2);grid.Children.Add(bottom);
        timer.Tick+=(_,_)=>Refresh();Unloaded+=(_,_)=>{timer.Stop();session?.Stop();Commit();pickerMotion?.Close();orb.Active=false;};
        IsVisibleChanged+=(_,_)=>{if(!IsVisible&&IsLoaded){session?.Stop();Commit();timer.Stop();orb.Active=false;}};
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){if(picker?.IsOpen==true)pickerMotion?.Close();else Leave();e.Handled=true;}};
    }
    internal async Task Start()
    {
        if(leaving||Calling)return;Commit();
        var settings=manager.Settings;provider=settings.AiProvider;protectedKey=settings.AiKeyProtected??"";identity=provider+protectedKey;
        var voice=settings.VoiceCall.VoiceFor(provider);voices.Content=VoiceLabel(voice);
        voices.IsEnabled=VoiceCallOptions.Supported(provider);
        if(!VoiceCallOptions.Supported(provider)){state.Text="Choose Gemini or OpenAI";caption.Text="Switch the chat connection to use a live voice API.";reconnect.Visibility=Visibility.Collapsed;mute.IsEnabled=false;return;}
        if(protectedKey.Length==0){state.Text="Connect your API key";caption.Text="Save a Gemini or OpenAI chat key in Settings to start a call.";mute.IsEnabled=false;return;}
        string key;try{key=AiService.UnlockGenerationKey(protectedKey);}catch{state.Text="Saved key unavailable";caption.Text="Re-enter your chat API key in Settings.";return;}
        if(session!=null)session.Changed-=Refresh;
        session=createSession(provider,settings.VoiceCall.ModelFor(provider),voice,key,context());committed=false;var activeSession=session;
        using var bridge=new VoiceActionBridge(manager);activeSession.Language=settings.VoiceCall.Language;if(settings.VoiceCall.AllowActions){activeSession.ExecuteTool=bridge.Execute;bridge.Progress=activeSession.ReportAction;}
        session.Changed+=Refresh;orb.Active=true;timer.Start();Refresh();await activeSession.Run();if(ReferenceEquals(session,activeSession)){Commit();Refresh();}
    }
    UIElement VoiceLabel(string name){var row=new StackPanel{Orientation=Orientation.Horizontal};row.Children.Add(new PremiumIcon("audio-lines",15){Margin=new Thickness(0,0,6,0)});row.Children.Add(WidgetVisuals.Label(name,12));return row;}
    void Refresh()
    {
        if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(Refresh);return;}
        var call=session;var active=call?.Active==true;
        action.Text=call?.CurrentAction.Length>0?call.CurrentAction:call?.ActionLog.LastOrDefault()??"";
        if(call!=null){state.Text=call.State;detail.Text=provider+(call.ConnectedAt is {} start?" · "+ToolActivity.Offset(DateTimeOffset.Now-start).Split('.')[0]:" · Live voice");var last=call.Transcript.LastOrDefault();caption.Text=call.Error.Length>0?call.Error:last.Text??"Speak naturally. You can interrupt a reply.";}
        mute.IsEnabled=call?.Connected==true;mute.ToolTip=call?.Muted==true?"Unmute microphone":"Mute microphone";AutomationProperties.SetName(mute,(string)mute.ToolTip);WidgetVisuals.UpdateIcon(mute,call?.Muted==true?"mic-off":"microphone",17,"WidgetInk");
        reconnect.Visibility=active?Visibility.Collapsed:Visibility.Visible;orb.Active=active;
        if(!active)timer.Stop();else if(!timer.IsEnabled)timer.Start();
    }
    void Commit(){if(session==null||committed)return;committed=true;transcript(provider,protectedKey,session.Transcript.ToArray());}
    internal void ConfigurationChanged(){if(identity.Length>0&&(manager.Snoozed||identity!=manager.Settings.AiProvider+(manager.Settings.AiKeyProtected??"")))Leave();}
    internal void Escape(){if(picker?.IsOpen==true)pickerMotion?.Close();else Leave();}
    internal void Leave()
    {
        if(leaving)return;leaving=true;pickerMotion?.Close();timer.Stop();session?.Stop();Commit();orb.Active=false;
        if(SystemParameters.ClientAreaAnimation){BeginAnimation(OpacityProperty,new DoubleAnimation(0,TimeSpan.FromMilliseconds(130)));Dispatcher.BeginInvoke(async()=>{await Task.Delay(130);closed();});}else closed();
    }
    void OpenVoices()
    {
        if(picker?.IsOpen==true){pickerMotion?.Close();return;}
        var body=new StackPanel();var title=WidgetVisuals.Label("Choose a voice",13);title.Margin=new Thickness(10,8,10,7);body.Children.Add(title);
        foreach(var voice in VoiceCallOptions.Voices(provider)){
            var chosen=voice;var button=WidgetVisuals.IconButton("audio-lines","Use "+chosen,()=>SelectVoice(chosen),46);button.Width=double.NaN;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Margin=new Thickness(2,1,2,1);
            var row=new DockPanel{Margin=new Thickness(12,3,12,3)};var icon=new PremiumIcon("audio-lines",17,"Text"){Margin=new Thickness(0,0,12,0)};row.Children.Add(icon);var check=WidgetVisuals.Icon(manager.Settings.VoiceCall.VoiceFor(provider)==chosen?"check":"minus",13,"Muted");check.Opacity=manager.Settings.VoiceCall.VoiceFor(provider)==chosen?1:0;DockPanel.SetDock(check,Dock.Right);row.Children.Add(check);
            row.Children.Add(Ui.Stack(Ui.Text(chosen,13),Ui.Text(VoiceCharacter(chosen),10,"Muted")));button.Content=row;body.Children.Add(button);
        }
        var hint=WidgetVisuals.Label("Changing voice ends the current call.\nPress Start call to use the new voice.",10);hint.TextWrapping=TextWrapping.Wrap;hint.Margin=new Thickness(10,9,10,6);hint.Opacity=.65;body.Children.Add(hint);
        var card=new Border{Child=new ScrollViewer{Content=body,MaxHeight=350,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},Width=250,Padding=new Thickness(6),Background=Ui.Brush("Panel"),BorderBrush=Ui.Brush("Line"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(20)};
        picker=new Popup{Child=card,PlacementTarget=voices,Placement=PlacementMode.Top,VerticalOffset=-6};pickerMotion=new(picker,card,voices);card.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){pickerMotion.Close();e.Handled=true;}};picker.Opened+=(_,_)=>body.Children.OfType<Button>().FirstOrDefault()?.Focus();picker.IsOpen=true;
    }
    string VoiceCharacter(string name)=>name switch{"Aoede"=>"Breezy","Kore"=>"Firm","Puck"=>"Upbeat","Charon"=>"Informative","Fenrir"=>"Excitable","Leda"=>"Youthful","Orus"=>"Firm","Zephyr"=>"Bright",_=>"OpenAI Realtime"};
    void SelectVoice(string value)
    {
        pickerMotion?.Close();var options=manager.Settings.VoiceCall;var previous=options.VoiceFor(provider);if(value==previous)return;
        if(provider=="Gemini")options.GeminiVoice=value;else options.OpenAIVoice=value;
        if(!manager.TrySave()){if(provider=="Gemini")options.GeminiVoice=previous;else options.OpenAIVoice=previous;caption.Text="Could not save the voice. Try again.";return;}
        session?.Stop();Commit();voices.Content=VoiceLabel(value);Refresh();caption.Text=value+" selected. Start a new call when ready.";
    }
}
