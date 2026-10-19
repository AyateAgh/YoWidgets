using System.Windows.Media.Effects;
using System.Windows.Shapes;
namespace YoWidgets;

internal sealed class GlassEffect : ShaderEffect
{
    static readonly PixelShader Shader = new() { UriSource = new Uri("pack://application:,,,/YoWidgets;component/Glass.ps"), ShaderRenderMode = ShaderRenderMode.HardwareOnly };
    public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty("Input", typeof(GlassEffect), 0);
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register("Size", typeof(System.Windows.Media.Media3D.Point4D), typeof(GlassEffect), new UIPropertyMetadata(new System.Windows.Media.Media3D.Point4D(300, 168, 28, 20), PixelShaderConstantCallback(0)));
    public static readonly DependencyProperty OpticsProperty = DependencyProperty.Register("Optics", typeof(System.Windows.Media.Media3D.Point4D), typeof(GlassEffect), new UIPropertyMetadata(new System.Windows.Media.Media3D.Point4D(1, .2, 0, 1), PixelShaderConstantCallback(1)));
    public static readonly DependencyProperty LightProperty = DependencyProperty.Register("Light", typeof(System.Windows.Media.Media3D.Point4D), typeof(GlassEffect), new UIPropertyMetadata(new System.Windows.Media.Media3D.Point4D(.5, .5, 0, 0), PixelShaderConstantCallback(2)));
    public GlassEffect()
    {
        PixelShader = Shader;
        UpdateShaderValue(InputProperty);
        UpdateShaderValue(SizeProperty);
        UpdateShaderValue(OpticsProperty);
        UpdateShaderValue(LightProperty);
    }
    public void Configure(double w, double h, double strength, double tint, bool light)
    {
        SetValue(SizeProperty, new System.Windows.Media.Media3D.Point4D(w, h, Math.Min(32, Math.Min(w, h) / 4), 20));
        SetValue(OpticsProperty, new System.Windows.Media.Media3D.Point4D(strength, tint, light ? 1 : 0, 1));
    }
    public void Pointer(Point p, double w, double h, bool over) => SetValue(LightProperty, new System.Windows.Media.Media3D.Point4D(p.X / Math.Max(1, w), p.Y / Math.Max(1, h), over ? 1 : 0, 0));
}
internal sealed class GlassSurface : Grid
{
    static readonly List<WeakReference<GlassSurface>> surfaces = [];
    static bool shaderFailed;
    bool usingFallback;
    static GlassSurface()
    {
        PixelShader.InvalidPixelShaderEncountered += (_, _) => System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            shaderFailed = true;
            foreach (var reference in surfaces.ToArray())
                if (reference.TryGetTarget(out var surface))
                    surface.UseFallback();
            surfaces.RemoveAll(reference => !reference.TryGetTarget(out _));
            App.Trace("glass", "Render-thread shader rejected; transparent gradient fallback enabled");
        });
    }
    readonly Rectangle source = new(); readonly Border preblur = new(); readonly GlassEffect? effect; readonly Grid optical = new();
    readonly BlurEffect blur = new() { RenderingBias = RenderingBias.Performance };
    double previousWidth, previousHeight, previousStrength = -1, previousOpacity = -1;
    bool previousLight;
    readonly Border fallback = new() { CornerRadius = new CornerRadius(28), Margin = new Thickness(20), BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)), Background = new LinearGradientBrush(Color.FromArgb(28, 255, 255, 255), Color.FromArgb(12, 180, 205, 230), 65), Visibility = Visibility.Collapsed };
    public string Mode => usingFallback ? "transparent-gradient-fallback" : "refracted-alpha";
    public GlassSurface()
    {
        IsHitTestVisible = false;
        Margin = new Thickness(-20);
        ClipToBounds = false;
        preblur.Child = source;
        optical.Children.Add(preblur);
        try
        {
            if (!shaderFailed && (RenderCapability.Tier >> 16) >= 2 && RenderCapability.IsPixelShaderVersionSupported(3, 0) && Environment.GetEnvironmentVariable("YOWIDGETS_GLASS_FALLBACK") != "1")
                effect = new GlassEffect();
        }
        catch (Exception e) { App.Log("glass-shader", e); }
        optical.Effect = effect;
        Children.Add(optical);
        Children.Add(fallback);
        surfaces.RemoveAll(reference => !reference.TryGetTarget(out _));
        surfaces.Add(new(this));
        if (effect == null)
            UseFallback();
    }
    void UseFallback()
    {
        usingFallback = true;
        optical.Effect = null;
        preblur.Effect = null;
        source.Fill = null;
        optical.Visibility = Visibility.Collapsed;
        fallback.Visibility = Visibility.Visible;
    }
    public void Update(Brush brush, double w, double h, AppSettings settings)
    {
        if (usingFallback)
            return;
        source.Fill = brush;
        if (Math.Abs(blur.Radius - settings.Blur) > .01)
            blur.Radius = settings.Blur;
        if (preblur.Effect != (settings.Blur > 0 ? blur : null))
            preblur.Effect = settings.Blur > 0 ? blur : null;
        if (w != previousWidth || h != previousHeight || previousStrength != settings.GlassStrength || previousOpacity != settings.Opacity || previousLight != settings.Light)
        {
            effect?.Configure(w, h, settings.GlassStrength, settings.Opacity, settings.Light);
            previousWidth = w;
            previousHeight = h;
            previousStrength = settings.GlassStrength;
            previousOpacity = settings.Opacity;
            previousLight = settings.Light;
        }
    }
    public void Pointer(Point p, double w, double h, bool over) => effect?.Pointer(p, w, h, over);
}
