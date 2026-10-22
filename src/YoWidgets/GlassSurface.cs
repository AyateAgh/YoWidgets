using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Media.Media3D;
using System.Windows.Media.Animation;
namespace YoWidgets;

internal sealed class GlassEffect : ShaderEffect
{
    static readonly PixelShader Shader = new() { UriSource = new Uri("pack://application:,,,/YoWidgets;component/Glass.ps"), ShaderRenderMode = ShaderRenderMode.HardwareOnly };
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register("Size", typeof(Point4D), typeof(GlassEffect), new UIPropertyMetadata(new Point4D(300, 168, 32, 20), PixelShaderConstantCallback(0)));
    public static readonly DependencyProperty MaterialProperty = DependencyProperty.Register("Material", typeof(Point4D), typeof(GlassEffect), new UIPropertyMetadata(new Point4D(1, .14, 0, 0), PixelShaderConstantCallback(1)));
    public static readonly DependencyProperty LightProperty = DependencyProperty.Register("Light", typeof(Point4D), typeof(GlassEffect), new UIPropertyMetadata(new Point4D(.5, .5, 0, 0), PixelShaderConstantCallback(2)));
    public static readonly DependencyProperty MotionProperty = DependencyProperty.Register("Motion", typeof(double), typeof(GlassEffect), new UIPropertyMetadata(0d, PixelShaderConstantCallback(3)));
    public GlassEffect()
    {
        PixelShader = Shader;
        UpdateShaderValue(SizeProperty); UpdateShaderValue(MaterialProperty); UpdateShaderValue(LightProperty); UpdateShaderValue(MotionProperty);
    }
    public void Configure(double width, double height, WidgetPack pack)
    {
        SetValue(SizeProperty, new Point4D(width, height, Math.Min(32, Math.Min(width, height) / 4), 20));
        SetValue(MaterialProperty, new Point4D(pack.Liquid ? 1 : 0, pack.Tint, 0, 0));
    }
    public void Pointer(Point point, double width, double height, bool over) => SetValue(LightProperty, new Point4D(point.X / Math.Max(1, width), point.Y / Math.Max(1, height), over ? 1 : 0, 0));
}
internal sealed class GlassSurface : Grid
{
    static readonly List<WeakReference<GlassSurface>> surfaces = [];
    static bool shaderFailed;
    readonly GlassEffect? effect;
    readonly Rectangle overlay = new() { Fill = Brushes.Transparent };
    readonly Border fallback = new() { CornerRadius = new(32), Margin = new(20), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Color.FromArgb(65, 255, 255, 255)), Visibility = Visibility.Collapsed };
    double previousWidth, previousHeight;
    string previousPack = "";
    bool usingFallback;
    public string Mode => usingFallback ? "live-background-with-gradient-rim" : "live-background-with-optical-rim";
    static GlassSurface()
    {
        PixelShader.InvalidPixelShaderEncountered += (_, _) => System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            shaderFailed = true;
            foreach (var reference in surfaces.ToArray()) if (reference.TryGetTarget(out var surface)) surface.UseFallback();
            surfaces.RemoveAll(reference => !reference.TryGetTarget(out _));
            App.Trace("glass", "Shader unavailable; neutral transparent rim enabled. No wallpaper fallback.");
        });
    }
    public GlassSurface()
    {
        IsHitTestVisible = false;
        Margin = new(-20);
        try
        {
            if (!shaderFailed && (RenderCapability.Tier >> 16) >= 2 && RenderCapability.IsPixelShaderVersionSupported(3, 0) && Environment.GetEnvironmentVariable("YOWIDGETS_GLASS_FALLBACK") != "1") effect = new();
        }
        catch (Exception e) { App.Log("glass-shader", e); }
        overlay.Effect = effect;
        Children.Add(overlay); Children.Add(fallback);
        surfaces.RemoveAll(reference => !reference.TryGetTarget(out _));
        surfaces.Add(new(this));
        if (effect == null) UseFallback();
    }
    void UseFallback()
    {
        usingFallback = true;
        overlay.Effect = null;
        overlay.Visibility = Visibility.Collapsed;
        fallback.Visibility = Visibility.Visible;
    }
    public void Update(double width, double height, string package)
    {
        var pack = WidgetPacks.Get(package);
        if (width == previousWidth && height == previousHeight && pack.Id == previousPack) return;
        effect?.Configure(width, height, pack);
        if (pack.Id != previousPack)
            fallback.Background = new LinearGradientBrush(Color.FromArgb((byte)((pack.Tint + .04) * 255), 255, 255, 255), Color.FromArgb((byte)(pack.Tint * 255), 192, 205, 218), 65);
        previousWidth = width; previousHeight = height; previousPack = pack.Id;
    }
    public void Pointer(Point point, double width, double height, bool over) => effect?.Pointer(point, width, height, over);
    public void Motion(bool active)
    {
        if (effect == null || !SystemParameters.ClientAreaAnimation) return;
        effect.BeginAnimation(GlassEffect.MotionProperty, new DoubleAnimation(active ? 1 : 0, TimeSpan.FromMilliseconds(active ? 120 : 260)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }
}
