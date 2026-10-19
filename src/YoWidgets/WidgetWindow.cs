using System.Windows.Interop;
using System.Windows.Media.Animation;
namespace YoWidgets;

internal sealed class WidgetWindow : Window
{
    readonly WidgetManager manager; readonly WidgetState state; readonly GlassSurface glass = new(); readonly Grid surface = new() { Background = null }; readonly bool checker; int expansionVersion; bool closing; bool loaded; bool moving; bool animating; ImageBrush? movingBrush; RectD movingArea = new(0, 0, 1, 1); long moveFrames; RectD? paintedBounds;
    Brush? motionSource;
    RectD? motionSourceArea;
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
        Title = "YoWidgets · " + s.Kind;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
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
        content.Margin = new Thickness(18);
        surface.Children.Add(content);
        Content = surface;
        SetDimensions();
        SourceInitialized += (_, _) => { Native.ToolWindow(this); var source = (HwndSource)PresentationSource.FromVisual(this); source.AddHook(Hook); };
        Loaded += (_, _) => { loaded = true; Position(); RefreshGlass(); Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, () => TraceVisibility()); Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, PrewarmMove); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) { motionSource = null; motionSourceArea = null; } else if (loaded) Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, PrewarmMove); };
        LocationChanged += (_, _) => { if (moving || animating) RenderMove(null, EventArgs.Empty); else if (loaded) QueueGlassRefresh(); };
        SizeChanged += (_, _) => { if (animating) RenderMove(null, EventArgs.Empty); else if (loaded && !moving) QueueGlassRefresh(); };
        MouseMove += (_, e) => { if (!m.Snoozed && !moving) glass.Pointer(e.GetPosition(surface), surface.ActualWidth, surface.ActualHeight, true); };
        MouseLeave += (_, _) => glass.Pointer(new(.5, .5), 1, 1, false);
        MouseLeftButtonDown += (_, e) => { if (e.OriginalSource is System.Windows.Controls.Primitives.Thumb || IsInteractive(e.OriginalSource as DependencyObject)) return; e.Handled = true; MoveNative(); };
        KeyDown += (_, e) => { var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1; var b = Native.Bounds(this); var x = b.X; var y = b.Y; switch (e.Key) { case Key.Left: x -= step; break; case Key.Right: x += step; break; case Key.Up: y -= step; break; case Key.Down: y += step; break; case Key.Escape: App.CurrentApp.ShowShell(); return; default: return; } PlacePhysical(x, y); SavePosition(); e.Handled = true; };
        var menu = new ContextMenu();
        void Item(string label, Action a)
        {
            var i = new MenuItem { Header = label };
            i.Click += (_, _) => a();
            menu.Items.Add(i);
        }
        Item("Настроить виджеты", App.CurrentApp.ShowShell);
        foreach (var size in new[] { "S", "M", "L" })
        {
            var z = size;
            Item("Размер " + z, () => { state.Size = z; SetDimensions(); manager.Save(); });
        }
        Item("Вернуть в видимую область", Position);
        Item("Выключить виджет", () => { if (checker) CloseOwned(); else manager.Disable(state); });
        surface.ContextMenu = menu;
        Closing += (_, e) => { if (!closing && !checker) { e.Cancel = true; manager.Disable(state); } };
    }
    bool refreshQueued;
    void PrewarmMove()
    {
        if (closing || !IsVisible || manager.Snoozed) return;
        var screen = System.Windows.Forms.Screen.FromHandle(Native.Handle(this)).Bounds;
        var area = new RectD(screen.X,screen.Y,screen.Width,screen.Height);
        if (motionSource == null || motionSourceArea != area)
        {
            motionSource = manager.Wallpaper.GetBrush(area,checker,moving:true);
            motionSourceArea = area;
        }
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
        var screen = System.Windows.Forms.Screen.FromHandle(Native.Handle(this)).Bounds;
        movingArea = new(screen.X, screen.Y, screen.Width, screen.Height);
        var prepared = motionSourceArea == movingArea && motionSource != null ? motionSource : manager.Wallpaper.GetBrush(movingArea, checker, moving: true);
        motionSource = prepared;
        motionSourceArea = movingArea;
        movingBrush = prepared is ImageBrush image ? image.CloneCurrentValue() : null;
        paintedBounds = null;
        if (movingBrush != null)
        {
            var view = BackdropViewport.For(Native.Bounds(this), movingArea);
            movingBrush.Viewbox = new Rect(view.X, view.Y, view.Width, view.Height);
            glass.Update(movingBrush, Math.Max(1, ActualWidth - 40), Math.Max(1, ActualHeight - 40), manager.Settings);
        }
    }
    void MoveNative()
    {
        if (moving || manager.Snoozed || Mouse.LeftButton != MouseButtonState.Pressed)
            return;
        if (animating) FinishExpansion();
        var started = System.Diagnostics.Stopwatch.StartNew();
        PrepareMotion();
        moveFrames = 0;
        paintedBounds = null;
        moving = true;
        if (movingBrush != null)
            glass.Update(movingBrush, Math.Max(1, ActualWidth - 40), Math.Max(1, ActualHeight - 40), manager.Settings);
        CompositionTarget.Rendering += RenderMove;
        var preparedMs = started.ElapsedMilliseconds;
        var decodesBeforeLoop = manager.Wallpaper.DecodeCount;
        var cpu = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime;
        var loop = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            DragMove();
        }
        catch (InvalidOperationException) { /* Mouse may have been released while preparing the first frame. */ }
        finally
        {
            CompositionTarget.Rendering -= RenderMove;
            moving = false;
            movingBrush = null;
            paintedBounds = null;
            if (!closing)
            {
                SavePosition();
                QueueGlassRefresh();
            }
            manager.Wallpaper.ClearCache();
            var used = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime - cpu;
            App.Trace("drag", $"native; prepareMs={preparedMs}; loopMs={loop.ElapsedMilliseconds}; frames={moveFrames}; decodesDuringLoop={manager.Wallpaper.DecodeCount-decodesBeforeLoop}; cpuMs={used.TotalMilliseconds:F1}");
        }
    }
    void RenderMove(object? sender, EventArgs e)
    {
        if ((!moving && !animating) || movingBrush == null)
            return;
        var bounds = Native.Bounds(this);
        if (bounds == paintedBounds)
            return;
        paintedBounds = bounds;
        var view = BackdropViewport.For(bounds, movingArea);
        movingBrush.Viewbox = new Rect(view.X, view.Y, view.Width, view.Height);
        movingBrush.ViewboxUnits = BrushMappingMode.RelativeToBoundingBox;
        if (animating)
            glass.Update(movingBrush, Math.Max(1, ActualWidth - 40), Math.Max(1, ActualHeight - 40), manager.Settings);
        moveFrames++;
    }
    static bool IsInteractive(DependencyObject? p)
    {
        while (p != null)
        {
            if (p is System.Windows.Controls.Primitives.ButtonBase or System.Windows.Controls.Primitives.RangeBase or TextBox or PasswordBox)
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
    void TraceVisibility()
    {
        var b = Native.Bounds(this);
        App.Trace("widget-visible", $"{state.Kind}; HWND={Native.Handle(this)}; visible={IsVisible}; parent={Native.GetParent(Native.Handle(this))}; x={b.X}; y={b.Y}; width={b.Width}; height={b.Height}; topmost={Topmost}; transparent={AllowsTransparency}; glass={glass.Mode}");
    }
    void SavePosition()
    {
        var b = Native.Bounds(this);
        state.X = b.X;
        state.Y = b.Y;
        manager.Save();
    }
    public void UpdateHost()
    {
        if (!loaded)
            return;
        Topmost = manager.Settings.Topmost;
        if (!manager.Snoozed)
            Show();
        Position();
    }
    public void RefreshGlass()
    {
        if (!loaded || moving || animating || closing)
            return;
        var b = Native.Bounds(this);
        var brush = manager.Wallpaper.GetBrush(b, checker);
        glass.Update(brush, Math.Max(1, ActualWidth - 40), Math.Max(1, ActualHeight - 40), manager.Settings);
        Topmost = manager.Settings.Topmost;
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
            anim.Completed += (_, _) => { if (version == expansionVersion) FinishExpansion(); };
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
        Height = (Expanded ? 410 : 184) + 40;
        animating = false;
        CompositionTarget.Rendering -= RenderMove;
        movingBrush = null;
        paintedBounds = null;
        Position();
        QueueGlassRefresh();
    }
    IntPtr Hook(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if (msg == 0x02E0)
            Dispatcher.BeginInvoke(() => { if (!moving) { Position(); QueueGlassRefresh(); } });
        if (msg == 0x007E || msg == 0x001A)
            Dispatcher.BeginInvoke(() => { motionSource = null; motionSourceArea = null; Position(); RefreshGlass(); Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, PrewarmMove); });
        return IntPtr.Zero;
    }
    public void CloseOwned()
    {
        closing = true;
        motionSource = null;
        CompositionTarget.Rendering -= RenderMove;
        try
        {
            Hide();
            Close();
        }
        catch (InvalidOperationException) { }
    }
}
