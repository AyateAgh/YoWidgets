using System.Diagnostics;
namespace YoWidgets;

// Lightweight native equivalents of the Libraries.dev voice glow and dotted orb.
// Rendering is subscribed only while visible and active; reduced motion stays still.
internal abstract class ActiveVisual : FrameworkElement
{
    readonly Stopwatch clock = Stopwatch.StartNew();
    bool active, subscribed;
    bool motionEnabled=true;
    double last;
    protected bool MotionAllowed=>motionEnabled&&SystemParameters.ClientAreaAnimation&&!SystemParameters.HighContrast;
    protected double Time => MotionAllowed ? clock.Elapsed.TotalSeconds : 0;
    internal bool MotionEnabled {get=>motionEnabled;set{motionEnabled=value;Sync();InvalidateVisual();}}
    internal bool IsAnimating=>subscribed;
    public bool Active { get=>active; set { if(active==value)return; active=value; Sync(); InvalidateVisual(); } }
    protected ActiveVisual()
    {
        IsHitTestVisible=false; Loaded+=(_,_)=>Sync(); Unloaded+=(_,_)=>Unsubscribe(); IsVisibleChanged+=(_,_)=>Sync();
    }
    void Sync() { if (active && IsLoaded && IsVisible && MotionAllowed) { if(!subscribed) { CompositionTarget.Rendering+=Frame; subscribed=true; } } else Unsubscribe(); }
    void Unsubscribe() { if(subscribed) CompositionTarget.Rendering-=Frame; subscribed=false; }
    void Frame(object? sender,EventArgs e) { var now=clock.Elapsed.TotalSeconds; if(now-last<1d/30)return; last=now; InvalidateVisual(); }
}

internal sealed class VoiceGlow : ActiveVisual
{
    public Func<double>? ReadLevel { get; set; }
    public bool Processing { get; set; }
    double envelope;
    static readonly Color[] colors=AssistantSpectrum.Colors;
    protected override void OnRender(DrawingContext dc)
    {
        if(!Active || ActualWidth<1 || ActualHeight<1)return;
        var target=Processing ? .22+.08*Math.Sin(Time*1.7) : Math.Clamp(ReadLevel?.Invoke()??0,0,1);
        envelope+=(target-envelope)*(target>envelope?.65:.2);
        var height=ActualHeight; var width=ActualWidth;
        var radius=Math.Min(32,height/4);
        dc.PushClip(new RectangleGeometry(new Rect(RenderSize),radius,radius));
        for(var i=0;i<colors.Length;i++)
        {
            var wave=Math.Sin(Time*(Processing?1.4:2)+i*1.1);
            var x=width*(.1+i*.2)+wave*width*.065;
            var reach=Math.Min(height*.8,18+envelope*70);
            var brush=new RadialGradientBrush { Center=new(.5,1),GradientOrigin=new(.5,1),RadiusX=.5,RadiusY=1 };
            brush.GradientStops.Add(new(Color.FromArgb((byte)(150+envelope*70),colors[i].R,colors[i].G,colors[i].B),0));
            brush.GradientStops.Add(new(Color.FromArgb(0,colors[i].R,colors[i].G,colors[i].B),1));
            dc.DrawRectangle(brush,null,new Rect(x-width*.25,height-reach,width*.5,reach));
        }
        // A quiet moving spectral edge ties the beam to the glass rather than a box.
        var edge=new LinearGradientBrush { StartPoint=new(0,0),EndPoint=new(1,1),RelativeTransform=new RotateTransform(Time*(Processing?32:20),.5,.5) };
        for(var i=0;i<colors.Length;i++) edge.GradientStops.Add(new(colors[i],i/4d));
        var border=new Rect(1,1,Math.Max(0,width-2),Math.Max(0,height-2));
        foreach(var band in new[] { (16d,.06), (10d,.10), (5d,.19), (1.5,.85) }) {
            dc.PushOpacity(band.Item2*(.65+envelope*.6)); dc.DrawRoundedRectangle(null,new Pen(edge,band.Item1),border,radius,radius); dc.Pop();
        }
        dc.Pop();
    }
}

internal sealed class ThinkingOrb : ActiveVisual
{
    static readonly DependencyProperty InkBrushProperty=DependencyProperty.Register("InkBrush",typeof(Brush),typeof(ThinkingOrb),new FrameworkPropertyMetadata(Brushes.White,FrameworkPropertyMetadataOptions.AffectsRender));
    OrbState state=OrbState.Thinking,previous=OrbState.Thinking;
    double transition;
    public OrbState State { get=>state;set {if(state==value)return;previous=state;state=value;transition=Time;InvalidateVisual();} }
    public ThinkingOrb() { Width=Height=24; Margin=new Thickness(0,0,9,0); SetResourceReference(InkBrushProperty,"WidgetInk"); }
    protected override void OnRender(DrawingContext dc)
    {
        var ink=GetValue(InkBrushProperty) as SolidColorBrush ?? Brushes.White;
        var blend=!Active||!MotionAllowed?1:Math.Clamp((Time-transition)/.22,0,1);blend=blend*blend*(3-2*blend);
        if(blend<1)Draw(dc,ink.Color,previous,1-blend);
        Draw(dc,ink.Color,state,blend);
    }
    void Draw(DrawingContext dc,Color color,OrbState mode,double opacity)
    {
        if(opacity<=0)return;
        if(mode==OrbState.Generating&&!SystemParameters.HighContrast){dc.PushOpacity(opacity);AssistantSpectrum.Orb(dc,new Rect(0,0,24,24),Time);dc.Pop();return;}
        var time=Time;var points=new List<(double X,double Y,double Z,double A)>();
        void Dot(double x,double y,double z,double a=1)=>points.Add((x,y,z,a));
        if(mode is OrbState.Done or OrbState.Error or OrbState.Stopped) {
            for(var i=0;i<24;i++){var a=i*Math.PI/12;Dot(Math.Cos(a),Math.Sin(a),-.2,.55);}
            for(var i=0;i<13;i++) {var p=i/12d;
                if(mode==OrbState.Done) {if(p<.38)Dot(-.55+p*.9,p*1.1,0);else Dot(-.21+(p-.38)*1.25,.42-(p-.38)*1.5,0);}
                else if(mode==OrbState.Error) {Dot(-.4+p*.8,-.4+p*.8,0);Dot(-.4+p*.8,.4-p*.8,0);}
                else Dot(-.4+p*.8,0,0);
            }
        } else if(mode==OrbState.Editing) {
            // Three dotted strands weave around a shared spherical envelope.
            for(var strand=0;strand<3;strand++)for(var i=0;i<30;i++) {
                var y=-.94+i*1.88/29;var radius=Math.Sqrt(1-y*y);var angle=y*4+time*1.8+strand*Math.PI*2/3;
                Dot(Math.Cos(angle)*radius,y,Math.Sin(angle)*radius);
            }
        } else if(mode==OrbState.Streaming) {
            for(var band=-1;band<=1;band++)for(var i=0;i<28;i++) {
                var a=i*Math.PI*2/28+time*.8;Dot(Math.Cos(a)*.95,Math.Sin(a*2-time*2)*.28+band*.22,Math.Sin(a)*.85);
            }
        } else if(mode is OrbState.ToolResult or OrbState.Connecting) {
            for(var ring=0;ring<3;ring++)for(var i=0;i<25;i++) {
                var a=i*Math.PI*2/25+time*(ring%2==0?1:-1);var tilt=ring*Math.PI/3+time*.25;
                var r=mode==OrbState.ToolResult?.72+.16*Math.Sin(time*3+ring):.9;
                Dot(Math.Cos(a)*r,Math.Sin(a)*Math.Cos(tilt)*r,Math.Sin(a)*Math.Sin(tilt)*r);
            }
        } else for(var ring=1;ring<9;ring++) {
            var lat=Math.PI*ring/9;var count=Math.Max(3,(int)(Math.Sin(lat)*15));
            for(var i=0;i<count;i++) {
                var lng=2*Math.PI*i/count+time*(mode==OrbState.Searching?.2:.65);
                var x=Math.Sin(lat)*Math.Cos(lng);var z=Math.Sin(lat)*Math.Sin(lng);var y=Math.Cos(lat);var a=1d;
                if(mode==OrbState.Reading) {x*=.87+.12*Math.Sin(y*5-time*3);y+=.08*Math.Sin(lng*2+time*2);}
                if(mode==OrbState.Searching) {var scan=Math.Pow(Math.Max(0,Math.Cos(lng-time*2.4)),12);a=.35+.65*scan;}
                var tilt=.35;Dot(x,y*Math.Cos(tilt)-z*Math.Sin(tilt),y*Math.Sin(tilt)+z*Math.Cos(tilt),a);
            }
        }
        dc.PushOpacity(opacity);
        foreach(var p in points.OrderBy(p=>p.Z)) {
            var brush=new SolidColorBrush(color) { Opacity=Math.Clamp((.24+(p.Z+1)*.36)*p.A,0,1) };
            var radius=.47+(p.Z+1)*.16;dc.DrawEllipse(brush,null,new Point(12+p.X*9.6,12+p.Y*9.6),radius,radius);
        }dc.Pop();
    }
}
