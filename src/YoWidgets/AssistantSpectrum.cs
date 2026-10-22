using System.Globalization;
namespace YoWidgets;

// One palette for the widget edge, generation orb and status sweep.
internal static class AssistantSpectrum
{
    internal static readonly Color[] Colors=[Color.FromRgb(247,181,72),Color.FromRgb(248,99,148),Color.FromRgb(154,111,245),Color.FromRgb(69,158,248),Color.FromRgb(63,216,181)];
    internal static void Orb(DrawingContext dc,Rect bounds,double seconds)
    {
        var r=Math.Min(bounds.Width,bounds.Height)*.42;var c=new Point(bounds.X+bounds.Width/2,bounds.Y+bounds.Height/2);var breath=1+.035*Math.Sin(seconds*2.2);r*=breath;
        var halo=new RadialGradientBrush{GradientStops=new(){new(Color.FromArgb(72,154,111,245),.35),new(Colors[3] with{A=0},1)}};dc.DrawEllipse(halo,null,c,r*1.18,r*1.18);
        dc.PushClip(new EllipseGeometry(c,r,r));
        dc.DrawEllipse(new RadialGradientBrush(Color.FromRgb(47,43,79),Color.FromRgb(15,23,38)){Center=new(.42,.3),GradientOrigin=new(.42,.3)},null,c,r,r);
        for(var i=0;i<Colors.Length;i++){
            var a=seconds*.85+i*Math.PI*2/Colors.Length;var color=Colors[i];
            var brush=new RadialGradientBrush{GradientStops=new(){new(color,.12),new(color with{A=0},1)}};
            dc.DrawEllipse(brush,null,new Point(c.X+Math.Cos(a)*r*.7,c.Y+Math.Sin(a)*r*.7),r*.95,r*.95);
        }
        var shade=new RadialGradientBrush{Center=new(.48,.64),GradientOrigin=new(.48,.64),RadiusX=.7,RadiusY=.7,GradientStops=new(){new(Color.FromArgb(180,10,15,29),0),new(Color.FromArgb(130,17,24,39),.46),new(Color.FromArgb(0,17,24,39),1)}};dc.DrawEllipse(shade,null,c,r,r);
        var rim=new LinearGradientBrush{StartPoint=new(0,0),EndPoint=new(1,1),RelativeTransform=new RotateTransform(seconds*44,.5,.5)};for(var i=0;i<Colors.Length;i++)rim.GradientStops.Add(new(Colors[i],i/(double)(Colors.Length-1)));
        dc.DrawEllipse(null,new Pen(rim,.75),c,r-.4,r-.4);
        var highlight=new RadialGradientBrush(Colors[4] with{A=200},Colors[4] with{A=0});dc.DrawEllipse(highlight,null,new Point(c.X-r*.28,c.Y-r*.7),r*.5,r*.2);dc.Pop();
    }
    internal static void Sweep(DrawingContext dc,TextBlock source,double width,double seconds)
    {
        if(width<1||source.Text.Length==0)return;var p=seconds-Math.Floor(seconds);var center=-.4+p*1.8;
        var dark=source.Foreground is SolidColorBrush ink&&ink.Color.R*.2126+ink.Color.G*.7152+ink.Color.B*.0722<128;
        Color Tone(Color c)=>dark?Color.FromRgb((byte)(c.R*.25),(byte)(c.G*.25),(byte)(c.B*.25)):c;
        var brush=new LinearGradientBrush{StartPoint=new(0,0),EndPoint=new(1,0)};brush.GradientStops.Add(new(Tone(Colors[0]) with{A=0},center-.32));
        for(var i=0;i<Colors.Length;i++)brush.GradientStops.Add(new(Tone(Colors[i]),center-.23+i*.115));brush.GradientStops.Add(new(Tone(Colors[^1]) with{A=0},center+.32));
        var text=new FormattedText(source.Text,CultureInfo.CurrentUICulture,source.FlowDirection,new Typeface(source.FontFamily,source.FontStyle,source.FontWeight,source.FontStretch),source.FontSize,brush,VisualTreeHelper.GetDpi(source).PixelsPerDip){MaxTextWidth=width,MaxLineCount=1,Trimming=TextTrimming.CharacterEllipsis};
        dc.PushOpacity(.65);dc.DrawText(text,new Point(0,0));dc.Pop();
    }
}

// The real TextBlock remains the legible, accessible live region beneath this wash.
internal sealed class StatusTextSweep(TextBlock source):ActiveVisual
{
    protected override void OnRender(DrawingContext dc)
    {if(!Active||!MotionAllowed)return;dc.PushClip(new RectangleGeometry(new Rect(RenderSize)));AssistantSpectrum.Sweep(dc,source,ActualWidth,Time);dc.Pop();}
}
