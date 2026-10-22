using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Windows.Media.Animation;
using System.Xml.Linq;
namespace YoWidgets;

// Bundled SVG geometry: no remote requests, fonts or browser runtime at render time.
internal sealed class PremiumIcon : FrameworkElement
{
    static readonly DependencyProperty InkBrushProperty=DependencyProperty.Register("InkBrush",typeof(Brush),typeof(PremiumIcon),new FrameworkPropertyMetadata(Brushes.White,FrameworkPropertyMetadataOptions.AffectsRender));
    readonly DrawingGroup drawing;
    readonly string ink;
    readonly bool monochrome;
    readonly string name;
    static readonly Dictionary<string, DrawingGroup> cache = [];
    internal bool AnimateOnHover {get;set;}=true;
    internal PremiumIcon(string name, double size, string ink = "WidgetInk", bool brand = false)
    {
        this.name = name; this.ink = ink; monochrome = !brand || name is "openai" or "openrouter" or "rust" or "go" or "markdown" or "json";
        SetResourceReference(InkBrushProperty,ink);
        Width = Height = size; IsHitTestVisible = false;
        var key = (brand ? "Brands." : "Icons.") + name;
        if (!cache.TryGetValue(key, out drawing!)) cache[key] = drawing = Load(key, name);
        Loaded += (_, _) => BindHover();
    }
    protected override void OnRender(DrawingContext dc)
    {
        var bounds = drawing.Bounds;
        var scale = Math.Min(ActualWidth / bounds.Width, ActualHeight / bounds.Height);
        dc.PushTransform(new TranslateTransform((ActualWidth-bounds.Width*scale)/2,(ActualHeight-bounds.Height*scale)/2));
        dc.PushTransform(new ScaleTransform(scale,scale)); dc.PushTransform(new TranslateTransform(-bounds.X,-bounds.Y));
        if (monochrome)
        {
            dc.PushOpacityMask(new DrawingBrush(drawing) { Viewbox = bounds, ViewboxUnits = BrushMappingMode.Absolute, Stretch = Stretch.Fill });
            dc.DrawRectangle((Brush)GetValue(InkBrushProperty),null,bounds); dc.Pop();
        }
        else dc.DrawDrawing(drawing);
        dc.Pop(); dc.Pop(); dc.Pop();
    }
    static DrawingGroup Load(string key, string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("YoWidgets.Assets."+key+".svg") ?? throw new InvalidOperationException("Missing icon: "+key);
        var root = XDocument.Load(stream).Root!; var result = new DrawingGroup();
        var viewport = ((string?)root.Attribute("viewBox") ?? "0 0 24 24").Split(' ',StringSplitOptions.RemoveEmptyEntries).Select(N).ToArray();
        result.Children.Add(new GeometryDrawing(Brushes.Transparent,null,new RectangleGeometry(new Rect(viewport[0],viewport[1],viewport[2],viewport[3]))));
        // Gemini's supplied SVG uses filtered ellipses through a star mask. Use its
        // original mask geometry with a native multicolor brush at small icon sizes.
        if (name == "gemini")
        {
            var mask = root.Descendants().First(e => e.Name.LocalName == "mask").Descendants().First(e=>e.Name.LocalName=="path");
            var gradient = new LinearGradientBrush(); gradient.StartPoint=new(0,0); gradient.EndPoint=new(1,1);
            gradient.GradientStops.Add(new(Color.FromRgb(250,67,64),0)); gradient.GradientStops.Add(new(Color.FromRgb(246,192,19),.28)); gradient.GradientStops.Add(new(Color.FromRgb(20,187,105),.5)); gradient.GradientStops.Add(new(Color.FromRgb(54,137,255),.72));
            result.Children.Add(new GeometryDrawing(gradient,null,Geometry.Parse((string)mask.Attribute("d")!)));
        }
        else foreach (var e in root.Descendants().Where(e => !e.Ancestors().Any(a=>a.Name.LocalName is "defs" or "clipPath")))
        {
            string A(string attr,string fallback="0") => (string?)e.Attribute(attr) ?? fallback;
            Geometry? geo = e.Name.LocalName switch {
                "path" => Geometry.Parse((Inherit("fill-rule","nonzero")=="evenodd"?"F0 ":"F1 ")+A("d","")),
                "line" => new LineGeometry(new(N(A("x1")),N(A("y1"))),new(N(A("x2")),N(A("y2")))),
                "circle" => new EllipseGeometry(new(N(A("cx")),N(A("cy"))),N(A("r")),N(A("r"))),
                "rect" => new RectangleGeometry(new(N(A("x")),N(A("y")),N(A("width")),N(A("height"))),N(A("rx")),N(A("rx"))),
                "polyline" or "polygon" => Geometry.Parse("M"+A("points","")+(e.Name.LocalName=="polygon" ? "Z" : "")), _=>null };
            if (geo == null) continue;
            string Inherit(string attr,string fallback) => e.AncestorsAndSelf().Select(n=>(string?)n.Attribute(attr)).FirstOrDefault(v=>v!=null) ?? fallback;
            Brush? Paint(string value)
            {
                if(value=="none")return null;
                if(value.StartsWith("url(#")) {
                    var id=value[5..].TrimEnd(')'); var definition=root.Descendants().FirstOrDefault(n=>(string?)n.Attribute("id")==id);
                    if(definition!=null) {
                        double Coordinate(string key,double fallback) { var raw=(string?)definition.Attribute(key); return raw==null?fallback:raw.EndsWith('%')?N(raw.TrimEnd('%'))/100:N(raw); }
                        GradientBrush gradient=definition.Name.LocalName=="radialGradient" ? new RadialGradientBrush { Center=new(Coordinate("cx",.5),Coordinate("cy",.5)),GradientOrigin=new(Coordinate("fx",Coordinate("cx",.5)),Coordinate("fy",Coordinate("cy",.5))),RadiusX=Coordinate("r",.5),RadiusY=Coordinate("r",.5) } : new LinearGradientBrush { StartPoint=new(Coordinate("x1",0),Coordinate("y1",0)),EndPoint=new(Coordinate("x2",1),Coordinate("y2",0)) };
                        gradient.MappingMode=(string?)definition.Attribute("gradientUnits")=="userSpaceOnUse"?BrushMappingMode.Absolute:BrushMappingMode.RelativeToBoundingBox;
                        foreach(var stop in definition.Elements().Where(n=>n.Name.LocalName=="stop")) { var color=(Color)ColorConverter.ConvertFromString((string?)stop.Attribute("stop-color")??"#FFFFFF"); var offset=(string?)stop.Attribute("offset")??"0"; gradient.GradientStops.Add(new(color,offset.EndsWith('%')?N(offset.TrimEnd('%'))/100:N(offset))); }
                        if(gradient.GradientStops.Count>0)return gradient;
                    }
                    return Brushes.White;
                }
                if(value=="currentColor")return Brushes.White;
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value.Length==5 && value.StartsWith('#') ? "#"+string.Concat(value.Skip(1).Select(c=>$"{c}{c}")) : value));
            }
            var fill = Paint(Inherit("fill","black")); var stroke = Paint(Inherit("stroke","none"));
            var pen = stroke == null ? null : new Pen(stroke,N(Inherit("stroke-width","1.75"))) { StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round };
            var drawing=new GeometryDrawing(fill,pen,geo);
            var transforms=new TransformGroup();
            foreach(var ancestor in e.AncestorsAndSelf())
                foreach(System.Text.RegularExpressions.Match command in System.Text.RegularExpressions.Regex.Matches((string?)ancestor.Attribute("transform")??"",@"(translate|scale|rotate|matrix)\(([^)]+)\)").Cast<System.Text.RegularExpressions.Match>().Reverse()) {
                    var numbers=command.Groups[2].Value.Split([' ',','],StringSplitOptions.RemoveEmptyEntries).Select(N).ToArray();
                    Transform transform=command.Groups[1].Value switch { "translate"=>new TranslateTransform(numbers[0],numbers.Length>1?numbers[1]:0), "scale"=>new ScaleTransform(numbers[0],numbers.Length>1?numbers[1]:numbers[0]), "rotate"=>new RotateTransform(numbers[0],numbers.Length>2?numbers[1]:0,numbers.Length>2?numbers[2]:0), _=>new MatrixTransform(new Matrix(numbers[0],numbers[1],numbers[2],numbers[3],numbers[4],numbers[5])) };
                    transforms.Children.Add(transform);
                }
            if(transforms.Children.Count>0) result.Children.Add(new DrawingGroup { Transform=transforms,Children={ drawing } }); else result.Children.Add(drawing);
        }
        result.ClipGeometry = new RectangleGeometry(new Rect(viewport[0],viewport[1],viewport[2],viewport[3]));
        result.Freeze(); return result;
    }
    static double N(string value) => double.Parse(value,CultureInfo.InvariantCulture);
    internal bool Matches(string glyph,double size,string resource) => name==glyph && Width==size && ink==resource;
    void BindHover()
    {
        if(!AnimateOnHover)return;
        var immediate = VisualTreeHelper.GetParent(this) as FrameworkElement;
        DependencyObject? parent = immediate;
        while (parent is not null && parent is not System.Windows.Controls.Primitives.ButtonBase && parent is not ComboBoxItem && parent is not MenuItem) parent = VisualTreeHelper.GetParent(parent);
        if (parent is not FrameworkElement host) host = immediate ?? this;
        HoverMotion.Attach(host,this,name);
    }
}

internal static class HoverMotion
{
    // One timer per currently hovered host; five seconds between starts, including
    // leaving/re-entering. No idle rendering or timers on the rest of the UI.
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement,State> states = new();
    internal static void Attach(FrameworkElement host, FrameworkElement icon, string name)
    { states.GetValue(host,h=>new State(h)).SetIcon(icon,name); }
    sealed class State
    {
        readonly FrameworkElement host; readonly DispatcherTimer timer = new();
        FrameworkElement? icon; string name=""; long last = -5000;
        public State(FrameworkElement host)
        {
            this.host=host; timer.Tick+=(_,_)=>Play();
            host.MouseEnter+=(_,_)=>Play(); host.MouseLeave+=(_,_)=>Stop(); host.Unloaded+=(_,_)=>Stop();
            host.IsVisibleChanged+=(_,_)=> { if(!host.IsVisible) Stop(); };
        }
        public void SetIcon(FrameworkElement value,string kind) { icon=value; name=kind; }
        void Play()
        {
            timer.Stop(); if (!host.IsMouseOver || !host.IsVisible || !host.IsEnabled || !SystemParameters.ClientAreaAnimation || icon==null) return;
            var now=Environment.TickCount64; var remaining=5000-(now-last);
            if (remaining<=0)
            {
                last=now; remaining=5000; var group=new TransformGroup(); var rotate=new RotateTransform(); var move=new TranslateTransform(); var scale=new ScaleTransform(1,1); group.Children.Add(scale); group.Children.Add(rotate); group.Children.Add(move); icon.RenderTransformOrigin=new(.5,.5); icon.RenderTransform=group;
                var gear=name is "settings" or "gear" or "rotate-cw" or "refresh-cw";
                var keys=new DoubleAnimationUsingKeyFrames { Duration=TimeSpan.FromMilliseconds(650) };
                keys.KeyFrames.Add(new EasingDoubleKeyFrame(0,KeyTime.FromPercent(0)));
                keys.KeyFrames.Add(new EasingDoubleKeyFrame(gear?30:-2.8,KeyTime.FromPercent(.4),new CubicEase {EasingMode=EasingMode.EaseOut}));
                keys.KeyFrames.Add(new EasingDoubleKeyFrame(gear?-6:1,KeyTime.FromPercent(.75)));
                keys.KeyFrames.Add(new EasingDoubleKeyFrame(0,KeyTime.FromPercent(1)));
                if(gear) rotate.BeginAnimation(RotateTransform.AngleProperty,keys); else move.BeginAnimation(name.Contains("arrow") || name=="send" ? TranslateTransform.YProperty : TranslateTransform.XProperty,keys);
            }
            timer.Interval=TimeSpan.FromMilliseconds(Math.Max(1,remaining)); timer.Start();
        }
        void Stop() { timer.Stop(); if(icon!=null) icon.RenderTransform=Transform.Identity; }
    }
}
