using System.Windows.Media.Effects;
using System.Windows.Shapes;
namespace YoWidgets;
internal sealed class GlassEffect : ShaderEffect {
 static readonly PixelShader Shader=new(){UriSource=new Uri("pack://application:,,,/YoWidgets;component/Glass.ps"),ShaderRenderMode=ShaderRenderMode.HardwareOnly};
 public static readonly DependencyProperty InputProperty=RegisterPixelShaderSamplerProperty("Input",typeof(GlassEffect),0);
 public static readonly DependencyProperty SizeProperty=DependencyProperty.Register("Size",typeof(System.Windows.Media.Media3D.Point4D),typeof(GlassEffect),new UIPropertyMetadata(new System.Windows.Media.Media3D.Point4D(300,168,28,20),PixelShaderConstantCallback(0)));
 public static readonly DependencyProperty OpticsProperty=DependencyProperty.Register("Optics",typeof(System.Windows.Media.Media3D.Point4D),typeof(GlassEffect),new UIPropertyMetadata(new System.Windows.Media.Media3D.Point4D(1,.2,0,1),PixelShaderConstantCallback(1)));
 public static readonly DependencyProperty LightProperty=DependencyProperty.Register("Light",typeof(System.Windows.Media.Media3D.Point4D),typeof(GlassEffect),new UIPropertyMetadata(new System.Windows.Media.Media3D.Point4D(.5,.5,0,0),PixelShaderConstantCallback(2)));
 public GlassEffect(){PixelShader=Shader;UpdateShaderValue(InputProperty);UpdateShaderValue(SizeProperty);UpdateShaderValue(OpticsProperty);UpdateShaderValue(LightProperty);}
 public void Configure(double w,double h,double strength,double tint,bool light){SetValue(SizeProperty,new System.Windows.Media.Media3D.Point4D(w,h,Math.Min(28,Math.Min(w,h)/4),20));SetValue(OpticsProperty,new System.Windows.Media.Media3D.Point4D(strength,tint,light?1:0,1));}
 public void Pointer(Point p,double w,double h,bool over)=>SetValue(LightProperty,new System.Windows.Media.Media3D.Point4D(p.X/Math.Max(1,w),p.Y/Math.Max(1,h),over?1:0,0));
}
internal sealed class GlassSurface : Grid {
 readonly Rectangle source=new();readonly Border preblur=new();readonly GlassEffect effect=new();readonly Grid optical=new();
 public GlassSurface(){IsHitTestVisible=false;Margin=new Thickness(-20);ClipToBounds=false;preblur.Child=source;optical.Children.Add(preblur);optical.Effect=effect;Children.Add(optical);}
 public void Update(Brush brush,double w,double h,AppSettings settings){source.Fill=brush;preblur.Effect=settings.Blur>0?new BlurEffect{Radius=settings.Blur,RenderingBias=RenderingBias.Performance}:null;effect.Configure(w,h,settings.GlassStrength,settings.Opacity,settings.Light);}
 public void Pointer(Point p,double w,double h,bool over)=>effect.Pointer(p,w,h,over);
}
