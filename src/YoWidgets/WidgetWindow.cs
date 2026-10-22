using System.Windows.Interop;
using System.Windows.Media.Animation;
namespace YoWidgets;

internal sealed class WidgetWindow : Window
{
    readonly WidgetManager manager; readonly WidgetState state; readonly GlassSurface glass = new(); readonly Grid surface = new() { Background = null }; readonly bool checker; int expansionVersion; bool closing; bool loaded; bool moving; bool animating; long moveFrames;
    LiveBackdrop? liveBackdrop;
    bool assistantReading;
    readonly Border assistantDrop = new() { IsHitTestVisible=false,CornerRadius=new CornerRadius(24),BorderThickness=new Thickness(2),BorderBrush=Brushes.Transparent,Margin=new Thickness(1) };
    internal void AttachAssistantGlow(VoiceGlow glow) { surface.Children.Insert(1,glow); surface.Children.Add(assistantDrop); glow.SizeChanged+=(_,_)=>assistantDrop.CornerRadius=new CornerRadius(Math.Min(32,glow.ActualHeight/4)); }
    internal void SetAssistantDrop(bool active) { assistantDrop.BorderBrush=active ? Ui.Brush("Accent") : Brushes.Transparent; }
    string appliedPackage = "";
    bool? darkInk;
    RectD? assistantAnchor;
    double assistantContentHeight = AssistantLayout.CompactHeight;
    double assistantTargetTop;
    double assistantTargetHeight;
    public bool AssistantExpandsUp { get; private set; }
    public event Action? AssistantPlacementChanged;
    readonly SolidColorBrush ink = new(Colors.White);
    readonly System.Windows.Media.Effects.DropShadowEffect inkShadow = new() { Color = Colors.Black, BlurRadius = 3, ShadowDepth = 1, Opacity = .42, RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance };
    public event Action? ExpansionChanged;
    public bool Expanded
    {
        get; private set;
    }
    public WidgetWindow(WidgetManager m, WidgetState s, bool test = false)
    {
        manager = m;
        state = s;
        checker = test;
        Resources["WidgetInk"] = ink;
        SetAssistantInk(false);
        Title = "YoWidgets · " + WidgetPacks.Get(s.Pack).Name + " · " + s.Kind;
        WindowStyle = WindowStyle.None;
        FontFamily = Ui.BodyFont;
        FontWeight = FontWeights.Medium;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = App.InspectWidgetWindows;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.Manual;
        Topmost = m.Settings.Topmost;
        Left = s.X / Math.Max(1, System.Windows.Media.VisualTreeHelper.GetDpi(this).DpiScaleX);
        Top = s.Y / Math.Max(1, System.Windows.Media.VisualTreeHelper.GetDpi(this).DpiScaleY);
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        surface.Margin = new Thickness(20);
        surface.Children.Add(glass);
        var content = WidgetContentFactory.Create(m, s, this);
        content.Margin = new Thickness(s.Kind == "assistant" ? 8 : 18);
        content.Effect = inkShadow;
        surface.Children.Add(content);
        Content = surface;
        SetDimensions();
        SourceInitialized += (_, _) => { if (!App.InspectWidgetWindows) Native.ToolWindow(this); var source = (HwndSource)PresentationSource.FromVisual(this); source.AddHook(Hook); };
        Loaded += (_, _) => { loaded = true; Position(); RefreshGlass(); Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, () => TraceVisibility()); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) { liveBackdrop?.Hide(); } else if (loaded) { liveBackdrop?.Sync(this); } };
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) liveBackdrop?.Hide(); else liveBackdrop?.Sync(this); };
        LocationChanged += (_, _) => { if (moving || animating) RenderMove(null, EventArgs.Empty); else if (loaded) QueueGlassRefresh(); };
        SizeChanged += (_, _) => { if (animating) RenderMove(null, EventArgs.Empty); else if (loaded && !moving) QueueGlassRefresh(); };
        MouseMove += (_, e) => { if (!m.Snoozed && !moving) glass.Pointer(e.GetPosition(surface), surface.ActualWidth, surface.ActualHeight, true); };
        MouseLeave += (_, _) => glass.Pointer(new(.5, .5), 1, 1, false);
        MouseLeftButtonDown += (_, e) => { if (e.OriginalSource is System.Windows.Controls.Primitives.Thumb || IsInteractive(e.OriginalSource as DependencyObject)) return; e.Handled = true; MoveNative(); };
        KeyDown += (_, e) => { if(e.Handled||IsInteractive(Keyboard.FocusedElement as DependencyObject))return; var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1; var b = Native.Bounds(this); var x = b.X; var y = b.Y; switch (e.Key) { case Key.Left: x -= step; break; case Key.Right: x += step; break; case Key.Up: y -= step; break; case Key.Down: y += step; break; case Key.Escape: e.Handled=true;return; default: return; } PlacePhysical(x, y); SavePosition(); e.Handled = true; };
        var menu = new ContextMenu();
        void Item(string label, Action a)
        {
            var i = new MenuItem { Header = label };
            i.Click += (_, _) => a();
            menu.Items.Add(i);
        }
        Item("Manage widgets", App.CurrentApp.ShowShell);
        if (content is AssistantView assistant) Item("Show conversation", assistant.Reveal);
        foreach (var size in new[] { "S", "M", "L" })
        {
            var z = size;
            Item("Size " + z, () => { state.Size = z; SetDimensions(); manager.Save(); });
        }
        Item("Move into view", Position);
        Item("Disable widget", () => { if (checker) CloseOwned(); else manager.Disable(state); });
        surface.ContextMenu = menu;
        Closing += (_, e) => { if (!closing && !checker) { e.Cancel = true; manager.Disable(state); } };
    }
    bool refreshQueued;
    public void AdaptInk(double luminance)
    {
        if (closing) return;
        var next = ContrastPolicy.DarkInk(luminance, darkInk);
        if (next == darkInk) return;
        darkInk = next;
        SetAssistantInk(next);
        var color = next ? Color.FromRgb(25, 32, 43) : Colors.White;
        if (SystemParameters.ClientAreaAnimation)
            ink.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(color, TimeSpan.FromMilliseconds(220)));
        else { ink.BeginAnimation(SolidColorBrush.ColorProperty, null); ink.Color = color; }
        Resources["WidgetAccent"] = new SolidColorBrush(next ? Color.FromRgb(172, 42, 55) : Color.FromRgb(255, 153, 147));
        inkShadow.Opacity = next ? .06 : .42;
    }
    void SetAssistantInk(bool dark)
    {
        Resources["AssistantInstructionInk"]=new SolidColorBrush(dark?Color.FromRgb(18,76,168):Color.FromRgb(120,173,255));
        Resources["AssistantButtonInk"] = dark ? Brushes.White : new SolidColorBrush(Color.FromRgb(20,25,32));
        Resources["AssistantCodeInk"] = new SolidColorBrush(dark ? Color.FromRgb(20,73,134) : Color.FromRgb(190,219,255));
        Resources["AssistantInset"] = new SolidColorBrush(dark ? Color.FromArgb(38,255,255,255) : Color.FromArgb(42,0,0,0));
        Resources["AssistantBubble"] = new SolidColorBrush(dark ? Color.FromArgb(65,255,255,255) : Color.FromArgb(55,255,255,255));
        Resources["AssistantLine"] = new SolidColorBrush(dark ? Color.FromArgb(38,0,0,0) : Color.FromArgb(38,255,255,255));
    }
    void QueueGlassRefresh()
    {
        if (refreshQueued || moving || animating || closing)
            return;
        refreshQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Render, () => { refreshQueued = false; if (!moving && !animating && !closing) RefreshGlass(); });
    }
    void PrepareMotion()
    {
        glass.Motion(true);
        liveBackdrop?.Interaction(true);
    }
    void MoveNative()
    {
        if (moving || manager.Snoozed || Mouse.LeftButton != MouseButtonState.Pressed) return;
        if (animating) FinishExpansion();
        PrepareMotion();
        moveFrames = 0;
        moving = true;
        CompositionTarget.Rendering += RenderMove;
        var cpu = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime;
        var loop = System.Diagnostics.Stopwatch.StartNew();
        try { DragMove(); }
        catch (InvalidOperationException) { /* The pointer may already be released. */ }
        finally
        {
            CompositionTarget.Rendering -= RenderMove;
            moving = false;
            glass.Motion(false);
            liveBackdrop?.Interaction(false);
            if (!closing) { if (state.Kind == "assistant") assistantAnchor = null; SavePosition(); QueueGlassRefresh(); }
            var used = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime - cpu;
            App.Trace("drag", $"native; pack={state.Pack}; loopMs={loop.ElapsedMilliseconds}; frames={moveFrames}; wallpaperDecodes=0; cpuMs={used.TotalMilliseconds:F1}");
        }
    }
    void RenderMove(object? sender, EventArgs e)
    {
        if ((!moving && !animating) || closing) return;
        liveBackdrop?.Sync(this);
        if (animating) glass.Update(Math.Max(1, ActualWidth - 40), Math.Max(1, ActualHeight - 40), state.Pack);
        moveFrames++;
    }
    static bool IsInteractive(DependencyObject? p)
    {
        while (p != null)
        {
            if (p is System.Windows.Controls.Primitives.ButtonBase or System.Windows.Controls.Primitives.RangeBase or TextBox or PasswordBox or RichTextBox or ComboBox or ScrollViewer)
                return true;
            p = p is Visual ? VisualTreeHelper.GetParent(p) : p is FrameworkContentElement content ? content.Parent : LogicalTreeHelper.GetParent(p);
        }
        return false;
    }
    void SetDimensions()
    {
        var small = state.Size == "S";
        var large = state.Size == "L";
        var kind = state.Kind;
        var w = small ? 168 : 358;
        var h = large ? 354 : 168;
        if (kind is "music" or "quick" or "assistant" or "notifications")
        {
            w = kind == "music" && small ? 268 : 358;
            h = kind == "music" ? 184 : kind == "quick" ? 230 : 260;
            if (large)
                h = kind == "music" ? 410 : 354;
            if (kind == "music")
                Expanded = large;
        }
        Width = w + 40;
        Height = h + 40;
        if (kind == "assistant")
        {
            Width = (small ? 360 : large ? 480 : 400) + 40;
            Height = assistantContentHeight + 40;
        }
    }
    internal void SetAssistantReadability(bool expanded) { assistantReading = expanded; liveBackdrop?.Readability(expanded); }
    public void ResizeAssistant(double contentHeight)
    {
        if (closing) return;
        var changed = Math.Abs(assistantContentHeight - contentHeight) > .5;
        assistantContentHeight = contentHeight;
        if (!loaded) return;
        if (!changed) return;
        var bounds = Native.Bounds(this);
        var dpi = Math.Max(.5, Native.GetDpiForWindow(Native.Handle(this)) / 96d);
        var compact = Math.Min(bounds.Height,(AssistantLayout.CompactHeight + 40) * dpi);
        assistantAnchor ??= new RectD(bounds.X, AssistantExpandsUp ? bounds.Y + bounds.Height - compact : bounds.Y, bounds.Width, compact);
        var area = System.Windows.Forms.Screen.FromRectangle(new System.Drawing.Rectangle((int)bounds.X,(int)bounds.Y,(int)bounds.Width,(int)bounds.Height)).WorkingArea;
        var plan = AssistantLayout.Expand(assistantAnchor, (contentHeight + 40) * dpi, new(area.X,area.Y,area.Width,area.Height));
        AssistantExpandsUp = plan.Up;
        assistantTargetHeight = plan.Bounds.Height / dpi; assistantTargetTop = plan.Bounds.Y / dpi;
        AssistantPlacementChanged?.Invoke();
        var version = ++expansionVersion;
        if (SystemParameters.ClientAreaAnimation && !manager.Snoozed && IsVisible)
        {
            if (!animating) { PrepareMotion(); animating = true; CompositionTarget.Rendering += RenderMove; }
            var duration = TimeSpan.FromMilliseconds(260);
            var height = new DoubleAnimation(assistantTargetHeight,duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };
            height.Completed += (_,_) => { if (version == expansionVersion && !closing) FinishExpansion(); };
            BeginAnimation(HeightProperty,height);
            BeginAnimation(TopProperty,new DoubleAnimation(assistantTargetTop,duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
        }
        else FinishExpansion();
    }
    void Position()
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        var screen = screens.FirstOrDefault(x => x.WorkingArea.Contains((int)state.X, (int)state.Y)) ?? System.Windows.Forms.Screen.PrimaryScreen!;
        var a = screen.WorkingArea;
        var dpi = Native.GetDpiForWindow(Native.Handle(this)) / 96d;
        if (dpi <= 0)
            dpi = 1;
        var p = Placement.Clamp(state.X, state.Y, Width * dpi, Height * dpi, new(a.X, a.Y, a.Width, a.Height));
        PlacePhysical(p.X, p.Y);
    }
    void PlacePhysical(double x, double y)
    {
        var h = Native.Handle(this);
        Native.SetWindowPos(h, IntPtr.Zero, (int)x, (int)y, 0, 0, 0x15);
        QueueGlassRefresh();
    }
    public void CenterOnCurrentMonitor(int index, int count = 1, double cellWidth = 0, double cellHeight = 0)
    {
        var screen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);
        var a = screen.WorkingArea;
        var dpi = Math.Max(1, Native.GetDpiForWindow(Native.Handle(this)) / 96d);
        var p = Placement.CenterGrid(Width * dpi, Height * dpi, new(a.X, a.Y, a.Width, a.Height), index, count, Math.Max(Width * dpi, cellWidth), Math.Max(Height * dpi, cellHeight));
        state.X = p.X;
        state.Y = p.Y;
        Position();
        TraceVisibility();
    }
    internal void CenterInWorkArea(RectD area,int index)
    {
        Native.SetWindowPos(Native.Handle(this),IntPtr.Zero,(int)(area.X+24),(int)(area.Y+24),0,0,0x15);
        var dpi=Math.Max(.5,Native.GetDpiForWindow(Native.Handle(this))/96d);
        var center=Placement.Center(Width*dpi,Height*dpi,area);var offset=(index%7)*22*dpi;
        var point=Placement.Clamp(center.X+offset,center.Y+offset,Width*dpi,Height*dpi,area);state.X=point.X;state.Y=point.Y;PlacePhysical(point.X,point.Y);
    }
    void TraceVisibility()
    {
        var b = Native.Bounds(this);
        App.Trace("widget-visible", $"{state.Kind}; HWND={Native.Handle(this)}; visible={IsVisible}; parent={Native.GetParent(Native.Handle(this))}; x={b.X}; y={b.Y}; width={b.Width}; height={b.Height}; topmost={Topmost}; transparent={AllowsTransparency}; pack={state.Pack}; glass={glass.Mode}");
    }
    void SavePosition()
    {
        var b = Native.Bounds(this);
        state.X = b.X;
        state.Y = b.Y;
        if (state.Kind == "assistant" && AssistantExpandsUp) state.Y = b.Y + b.Height - (AssistantLayout.CompactHeight + 40) * Math.Max(.5,Native.GetDpiForWindow(Native.Handle(this))/96d);
        manager.Save();
    }
    public void UpdateHost()
    {
        if (!loaded)
            return;
        Topmost = manager.Settings.Topmost;
        if (!manager.Snoozed && !IsVisible)
            Show();
        Position();
    }
    public void RefreshGlass()
    {
        if (!loaded || moving || animating || closing) return;
        ApplyMaterial();
        glass.Update(Math.Max(1, ActualWidth - 40), Math.Max(1, ActualHeight - 40), state.Pack);
        Topmost = manager.Settings.Topmost;
        liveBackdrop?.Sync(this);
    }
    public void InvalidateBackground()
    {
        if ((liveBackdrop == null && LiveBackdrop.Supported) || (liveBackdrop != null && !LiveBackdrop.Supported)) appliedPackage = "";
        if (!moving && !animating) Position();
        RefreshGlass();
    }
    void ApplyMaterial()
    {
        var requested = WidgetPacks.Get(state.Pack).Id;
        if (appliedPackage == requested) return;
        appliedPackage = requested;
        liveBackdrop?.Dispose();
        liveBackdrop = null;
        if (LiveBackdrop.Supported)
        {
            try { liveBackdrop = new LiveBackdrop(requested); liveBackdrop.Readability(assistantReading); }
            catch (Exception e) { App.Log("live-backdrop", e); }
        }
        if (liveBackdrop == null)
            manager.DesktopStatus = "Live blur is unavailable. Widgets use a transparent tint; no wallpaper image is substituted.";
        App.Trace("material", $"pack={requested}; liveBackdrop={liveBackdrop != null}; wallpaperSampling=False");
    }
    public void ToggleExpanded()
    {
        var version = ++expansionVersion;
        Expanded = !Expanded;
        ExpansionChanged?.Invoke();
        var target = (Expanded ? 410 : 184) + 40;
        if (SystemParameters.ClientAreaAnimation && !manager.Snoozed)
        {
            if (!animating)
            {
                PrepareMotion();
                animating = true;
                CompositionTarget.Rendering += RenderMove;
            }
            var anim = new DoubleAnimation(target, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };
            anim.Completed += (_, _) => { if (version == expansionVersion && !closing) FinishExpansion(); };
            BeginAnimation(HeightProperty, anim);
        }
        else
        {
            FinishExpansion();
        }
    }
    void FinishExpansion()
    {
        ++expansionVersion;
        BeginAnimation(HeightProperty, null);
        if (state.Kind == "assistant") { BeginAnimation(TopProperty,null); Height = assistantTargetHeight; Top = assistantTargetTop; if (assistantContentHeight == AssistantLayout.CompactHeight) assistantAnchor = null; }
        else Height = (Expanded ? 410 : 184) + 40;
        animating = false;
        glass.Motion(false);
        CompositionTarget.Rendering -= RenderMove;
        liveBackdrop?.Interaction(false);
        if (state.Kind != "assistant") Position();
        QueueGlassRefresh();
    }
    IntPtr Hook(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if (msg is 0x0047 or 0x0006 && !closing) liveBackdrop?.Sync(this);
        if (msg == 0x0018 && w == IntPtr.Zero) liveBackdrop?.Hide();
        if (msg == 0x02E0)
            Dispatcher.BeginInvoke(() => { if (!moving) { Position(); QueueGlassRefresh(); } });
        if (msg is 0x007E or 0x001A or 0x031E or 0x031A)
            Dispatcher.BeginInvoke(() => { if (!closing) InvalidateBackground(); });
        return IntPtr.Zero;
    }
    public void CloseOwned()
    {
        closing = true;
        ++expansionVersion;
        BeginAnimation(HeightProperty, null);
        BeginAnimation(TopProperty, null);
        liveBackdrop?.Dispose();
        liveBackdrop = null;
        CompositionTarget.Rendering -= RenderMove;
        try
        {
            Hide();
            Close();
        }
        catch (InvalidOperationException) { }
    }
}
