namespace YoWidgets;

internal sealed class MainWindow : Window
{
    readonly WidgetManager manager; readonly ContentControl page = new(); readonly TextBlock status = Ui.Text("Settings are stored on this computer", 11, "Muted"); readonly TextBlock active = Ui.Text("", 12, "Muted"); string tab = "widgets"; readonly List<Button> nav = [];
    string? selectedPack;
    AssistantSettingsView? chatSettings;
    internal void OpenChatSettings(string provider){Select("settings");chatSettings?.SelectProvider(provider);}
    readonly TextBlock bridgeStatus = Ui.Text("", 11, "Muted");
    void UpdateBridgeStatus() => bridgeStatus.Text = manager.Data.Media.BrowserBridgeStatus;
    public MainWindow(WidgetManager m)
    {
        manager = m;
        SourceInitialized += (_, _) => { var dark = 1; Native.DwmSetWindowAttribute(Native.Handle(this), 20, ref dark, 4); };
        Title = "YoWidgets";
        WindowStyle = WindowStyle.None;
        System.Windows.Shell.WindowChrome.SetWindowChrome(this, new System.Windows.Shell.WindowChrome { CaptionHeight = 0, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(0) });
        Width = 1100;
        Height = 790;
        MinWidth = 860;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Ui.Brush("Background");
        Foreground = Ui.Brush("Text");
        FontFamily = Ui.BodyFont;
        FontWeight = FontWeights.Medium;
        var shell = new Grid();
        shell.ColumnDefinitions.Add(new()
        {
            Width = new GridLength(210)
        });
        shell.ColumnDefinitions.Add(new());
        var sidebar = new Grid { Background = Ui.Brush("Field"), Margin = new Thickness(0) };
        sidebar.RowDefinitions.Add(new()
        {
            Height = GridLength.Auto
        });
        sidebar.RowDefinitions.Add(new());
        sidebar.RowDefinitions.Add(new()
        {
            Height = GridLength.Auto
        });
        var logo = Ui.Stack(Ui.Text("yo.", 42, weight: FontWeights.Bold), Ui.Text("WIDGETS FOR WINDOWS", 9, "Muted"));
        logo.Margin = new Thickness(26, 30, 20, 36);
        sidebar.Children.Add(logo);
        var navigation = new StackPanel { Margin = new Thickness(14, 0, 14, 0) };
        AddNav(navigation, "Widgets", "widgets");
        AddNav(navigation, "Appearance", "appearance");
        AddNav(navigation, "Settings", "settings");
        AddNav(navigation, "Disable & uninstall", "cleanup");
        Grid.SetRow(navigation, 1);
        sidebar.Children.Add(navigation);
        var footer = Ui.Stack(Ui.Text("●  Local. Yours to control.", 10, "Muted"), Ui.Gap(10), Ui.Text("YoWidgets  0.5.2 · Windows 11", 10, "Muted"));
        footer.Margin = new Thickness(22);
        Grid.SetRow(footer, 2);
        sidebar.Children.Add(footer);
        shell.Children.Add(sidebar);
        var right = new Grid { Margin = new Thickness(34, 30, 32, 16) };
        right.RowDefinitions.Add(new()
        {
            Height = GridLength.Auto
        });
        right.RowDefinitions.Add(new());
        right.RowDefinitions.Add(new()
        {
            Height = GridLength.Auto
        });
        var top = new Grid { Margin = new Thickness(0, 0, 0, 24) };
        top.ColumnDefinitions.Add(new());
        top.ColumnDefinitions.Add(new()
        {
            Width = GridLength.Auto
        });
        top.Children.Add(active);
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(Ui.Button("Snooze", () => { manager.SetSnooze(!manager.Snoozed); Status(manager.Snoozed ? "Snoozed: widgets hidden, updates paused" : "Updates resumed"); }));
        actions.Children.Add(Ui.Button("Disable all", () => { manager.DisableAll(); Status("All widgets and startup disabled"); }));
        Grid.SetColumn(actions, 1);
        top.Children.Add(actions);
        right.Children.Add(top);
        var scroll = new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 10, 0) };
        Grid.SetRow(scroll, 1);
        right.Children.Add(scroll);
        status.Margin = new Thickness(0, 18, 0, 0);
        Grid.SetRow(status, 2);
        right.Children.Add(status);
        Grid.SetColumn(right, 1);
        shell.Children.Add(right);
        var frame = new Grid();
        frame.RowDefinitions.Add(new()
        {
            Height = new GridLength(44)
        });
        frame.RowDefinitions.Add(new());
        var titlebar = new Grid { Background = Ui.Brush("Field") };
        titlebar.ColumnDefinitions.Add(new());
        titlebar.ColumnDefinitions.Add(new()
        {
            Width = GridLength.Auto
        });
        var caption = Ui.Text("yo.   /   YoWidgets", 12, "Muted");
        caption.Margin = new Thickness(22, 0, 0, 0);
        titlebar.Children.Add(caption);
        titlebar.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; else DragMove(); };
        var windowButtons = new StackPanel { Orientation = Orientation.Horizontal };
        var minimize = Ui.Button("Minimize", () => WindowState = WindowState.Minimized); minimize.Content=WidgetVisuals.Icon("minus",16,"Text"); System.Windows.Automation.AutomationProperties.SetName(minimize,"Minimize");
        minimize.ToolTip = "Minimize";

        var close = Ui.Button("Close", Close); close.Content=WidgetVisuals.Icon("close",16,"Text"); System.Windows.Automation.AutomationProperties.SetName(close,"Close");
        close.ToolTip = "Close to tray";
        System.Windows.Automation.AutomationProperties.SetName(minimize, "Minimize");
        System.Windows.Automation.AutomationProperties.SetName(close, "Close to tray");
        windowButtons.Children.Add(minimize);
        windowButtons.Children.Add(close);
        Grid.SetColumn(windowButtons, 1);
        titlebar.Children.Add(windowButtons);
        frame.Children.Add(titlebar);
        Grid.SetRow(shell, 1);
        frame.Children.Add(shell);
        Content = frame;
        manager.Changed += OnChanged;
        manager.Data.Media.Changed += UpdateBridgeStatus;
        Closed += (_, _) => { manager.Changed -= OnChanged; manager.Data.Media.Changed -= UpdateBridgeStatus; page.Content = null; App.CurrentApp.ReleaseUnusedImages(); };
        Select("widgets");
        Loaded += (_, _) => App.CurrentApp.ReleaseUnusedImages();
        if (manager.DesktopStatus.Length > 0)
            Status(manager.DesktopStatus);
    }
    void AddNav(Panel p, string label, string id)
    {
        var b = Ui.Button(label, () => Select(id));
        b.HorizontalContentAlignment = HorizontalAlignment.Left;
        b.Margin = new Thickness(0, 0, 0, 8);
        b.Padding = new Thickness(12);
        b.Tag = id;
        var navIcon = WidgetVisuals.Icon(id switch { "widgets"=>"layout-dashboard", "appearance"=>"palette", "settings"=>"settings", _=>"power" },18,"Text");
        var row = new StackPanel { Orientation = Orientation.Horizontal }; navIcon.Margin = new Thickness(0,0,12,0); row.Children.Add(navIcon); row.Children.Add(Ui.Text(label,13)); b.Content=row;
        System.Windows.Automation.AutomationProperties.SetName(b,label);
        nav.Add(b);
        p.Children.Add(b);
    }
    void OnChanged()
    {
        UpdateBridgeStatus();
        active.Text = $"●  {manager.ActiveCount} active" + (manager.Snoozed ? " · Snooze" : " · event-driven updates");
        if (tab == "widgets")
            BuildWidgets();
    }
    internal void Select(string value)
    {
        tab = value;
        foreach (var b in nav)
            b.Background = Ui.Brush((string)b.Tag == value ? "Hover" : "Field");
        active.Text = $"●  {manager.ActiveCount} active" + (manager.Snoozed ? " · Snooze" : " · event-driven updates");
        switch (value)
        {
            case "widgets":
                BuildWidgets();
                break;
            case "appearance":
                BuildAppearance();
                break;
            case "settings":
                BuildSettings();
                break;
            case "cleanup":
                BuildCleanup();
                break;
        }
    }
    public void Status(string message)
    {
        status.Text = message;
        System.Windows.Automation.AutomationProperties.SetLiveSetting(status, System.Windows.Automation.AutomationLiveSetting.Polite);
    }
    StackPanel Heading(string title, string subtitle)
    {
        var p = Ui.Stack(Ui.Text(title, 30, weight: FontWeights.SemiBold), Ui.Gap(7), Ui.Text(subtitle, 13, "Muted"), Ui.Gap(26));
        return p;
    }
    void BuildPackLibrary()
    {
        var root = Heading("Your widget library.", "Two packs. One desktop. Choose a style, then make it yours.");
        var cards = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        foreach (var pack in WidgetPacks.All)
        {
            var activeCount = manager.Settings.Widgets.Count(w => w.Enabled && w.Pack == pack.Id);
            var body = Ui.Stack(Ui.Text(pack.Character.ToUpperInvariant(), 10, "Muted", FontWeights.SemiBold), Ui.Gap(12), Ui.Text(pack.Name, 28, weight: FontWeights.SemiBold), Ui.Gap(7), Ui.Text(pack.Description, 12, "Muted"), Ui.Gap(20), PackPreview.Create(pack), Ui.Gap(18), Ui.Text($"9 widgets · {activeCount} active", 12, "Muted"), Ui.Gap(16));
            var explore = Ui.Button("Explore " + pack.Name, () => { selectedPack = pack.Id; BuildWidgets(); }, true);
            explore.Margin = new Thickness(0);
            body.Children.Add(explore);
            var card = Ui.Panel(body, 22);
            card.Margin = new Thickness(0, 0, 14, 20);
            cards.Children.Add(card);
        }
        root.Children.Add(cards);
        root.Children.Add(Ui.Text("Mix both packs on the same desktop. Each widget keeps its own style.", 12, "Muted"));
        page.Content = root;
    }
    void BuildWidgets()
    {
        if (selectedPack == null) { BuildPackLibrary(); return; }
        var pack = WidgetPacks.Get(selectedPack);
        var p = new StackPanel();
        var back = Ui.Button("All packs", () => { selectedPack = null; BuildWidgets(); });
        back.HorizontalAlignment = HorizontalAlignment.Left;
        back.Margin = new Thickness(0, 0, 0, 20);
        p.Children.Add(back);
        p.Children.Add(Heading(pack.Name, pack.Description));
        var count = manager.Settings.Widgets.Count(w => w.Enabled && w.Pack == pack.Id);
        var packActions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
        packActions.Children.Add(Ui.Text($"9 widgets · {count} active", 12, "Muted"));
        if (count > 0)
        {
            var disable = Ui.Button("Disable this pack", () => manager.DisablePack(pack.Id));
            disable.Margin = new Thickness(18, 0, 0, 0);
            packActions.Children.Add(disable);
        }
        p.Children.Add(packActions);
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 };
        foreach (var item in WidgetManager.Catalog)
        {
            var body = new StackPanel();
            var preview = new PreviewGrid(item.Title) { Height = 150, Margin = new Thickness(0, 0, 0, 15), ClipToBounds = true };
            preview.SizeChanged += (_, _) => preview.Clip = new RectangleGeometry(new Rect(0, 0, Math.Max(1, preview.ActualWidth), 150), 18, 18);
            if (!manager.Snoozed)
            {
                preview.Background = PackPreview.Backdrop();
                var glass = new GlassSurface { Margin = new Thickness(0) };
                preview.Children.Add(glass);
                preview.SizeChanged += (_, _) => glass.Update(Math.Max(80, preview.ActualWidth - 40), 110, pack.Id);
            }
            FrameworkElement visual;
            if (manager.Snoozed)
            {
                visual = Ui.Stack(WidgetVisuals.Icon(item.Icon,38,"Muted"), Ui.Gap(8), Ui.Text("Paused", 13, "Muted"));
                visual.HorizontalAlignment = HorizontalAlignment.Center;
            }
            else if (item.Kind is "clock" or "calendar" or "weather" or "music")
            {
                visual = WidgetContentFactory.Create(manager, new WidgetState { Kind = item.Kind, Pack = pack.Id, Size = "S" }, preview: true);
                visual.IsHitTestVisible = false;
                if (item.Kind == "music")
                {
                    visual = new Viewbox { Child = visual, Stretch = Stretch.Uniform, Height = 110 };
                }
            }
            else
            {
                var icon = WidgetVisuals.Icon(item.Icon,46,"Muted");

                icon.HorizontalAlignment = HorizontalAlignment.Center;
                visual = icon;
            }
            var fitted = new Viewbox { Stretch = Stretch.Uniform, Margin = new Thickness(26, 22, 26, 22), Child = new Border { Width = 310, Height = 160, Child = visual } };
            preview.Children.Add(fitted);
            body.Children.Add(preview);
            var titleRow = new Grid();
            titleRow.ColumnDefinitions.Add(new());
            titleRow.ColumnDefinitions.Add(new()
            {
                Width = GridLength.Auto
            });
            titleRow.Children.Add(Ui.Text(item.Title, 15, weight: FontWeights.SemiBold));
            var enabled = manager.Settings.Widgets.Any(w => w.Kind == item.Kind && w.Pack == pack.Id && w.Enabled);
            var toggle = Ui.Toggle("", enabled, v => { if (manager.Snoozed && v) manager.SetSnooze(false); manager.Toggle(item.Kind, pack.Id); });
            toggle.ToolTip = (enabled ? "Disable " : "Enable ") + pack.Name + " " + item.Title;
            System.Windows.Automation.AutomationProperties.SetName(toggle, (enabled ? "Disable " : "Enable ") + pack.Name + " " + item.Title);
            Grid.SetColumn(toggle, 1);
            titleRow.Children.Add(toggle);
            body.Children.Add(titleRow);
            body.Children.Add(Ui.Text(item.Detail, 11, "Muted"));
            var more = Ui.Button("Add another", () => { if (manager.Snoozed) manager.SetSnooze(false); manager.Add(item.Kind, pack.Id); });
            more.Margin = new Thickness(0, 10, 0, 0);
            more.MinHeight = 28;
            more.Padding = new Thickness(8, 4, 8, 4);
            body.Children.Add(more);
            var card = Ui.Panel(body, 15);
            card.Margin = new Thickness(0, 0, 12, 14);
            grid.Children.Add(card);
        }
        p.Children.Add(grid);
        p.Children.Add(Ui.Text("Drag any free area to move. Right-click for size and controls.", 11, "Muted"));
        page.Content = p;
    }
    void BuildAppearance()
    {
        var p = Heading("At home on your desktop.", "Materials belong to packs. These options apply to your widgets.");
        var body = new StackPanel();
        body.Children.Add(Ui.Text("Time & placement", 17, weight: FontWeights.SemiBold));
        body.Children.Add(Ui.Gap(14));
        body.Children.Add(Ui.Toggle("Analog clock", manager.Settings.Analog, v => { manager.Settings.Analog = v; manager.ApplyAppearance(); }));
        body.Children.Add(Ui.Toggle("Temperature in °F", manager.Settings.Fahrenheit, v => { manager.Settings.Fahrenheit = v; manager.ApplyAppearance(); }));
        body.Children.Add(Ui.Toggle("Show seconds", manager.Settings.Seconds, v => { manager.Settings.Seconds = v; manager.Save(); }));
        body.Children.Add(Ui.Toggle("24-hour time", manager.Settings.Hour24, v => { manager.Settings.Hour24 = v; manager.Save(); }));
        body.Children.Add(Ui.Toggle("Always on top", manager.Settings.Topmost, v => { manager.Settings.Topmost = v; manager.ApplyAppearance(); }));
        body.Children.Add(Ui.Text("Independent floating windows", 12, "Muted"));
        body.Children.Add(Ui.Gap(14));
        body.Children.Add(Ui.Button("Center widgets on this screen", manager.ResetPositions));
        body.Children.Add(Ui.Gap(14));
        body.Children.Add(Ui.Text("Motion follows Windows animation settings. Snooze hides widgets and pauses updates. Virtual desktop behavior depends on Windows.", 12, "Muted"));
        p.Children.Add(Ui.Panel(body, 24));
        page.Content = p;
    }
    void BuildSettings()
    {
        var p = Heading("Make it yours.", "You choose the connections and permissions.");
        var body = new StackPanel();
        chatSettings=new AssistantSettingsView(manager,Status);body.Children.Add(chatSettings);body.Children.Add(Ui.Gap(28));
        body.Children.Add(Ui.Text("Weather", 17, weight: FontWeights.SemiBold));
        body.Children.Add(Ui.Gap(10));
        body.Children.Add(Ui.Text("City", 12, "Muted"));
        var city = Ui.Input(manager.Settings.City);
        body.Children.Add(city);
        var weatherButton = Ui.Button("Save & refresh", async () => { manager.Settings.City = city.Text; manager.Save(); await manager.Data.RefreshWeatherAsync(); Status(manager.Data.Weather.Error.Length == 0 ? "Weather updated" : manager.Data.Weather.Error); }, true);
        weatherButton.Margin = new Thickness(0, 10, 0, 20);
        body.Children.Add(weatherButton);
        body.Children.Add(Ui.Toggle("Lyrics · track details are shared with LRCLIB", manager.Settings.LyricsEnabled, v => { manager.Settings.LyricsEnabled = v; manager.Save(); manager.Data.RefreshLyrics(); Status(v ? "Finding lyrics for the current track…" : "Lyrics disabled"); }));
        body.Children.Add(Ui.Text("The Lyrics button appears only when text is found. Timed lyrics follow playback; other lyrics are shown as plain text.", 11, "Muted"));
        body.Children.Add(Ui.Toggle("Launch at Windows sign-in", StartupRegistration.Enabled, v => { try { StartupRegistration.Set(v); Status(v ? "Launch at sign-in enabled" : "Launch at sign-in disabled"); } catch (Exception e) { Status(e.Message); } }));
        body.Children.Add(Ui.Gap(20));
        body.Children.Add(Ui.Text("Music source", 17, weight: FontWeights.SemiBold));
        var sources = new ComboBox { Margin = new Thickness(0, 8, 0, 8), MinHeight = 34, ItemsSource = new[] { "Automatic" }.Concat(manager.Data.Media.Sources).ToArray(), SelectedIndex = 0 };
        sources.SelectionChanged += (_, _) => manager.Data.Media.SelectSource(sources.SelectedIndex == 0 ? null : sources.SelectedItem as string);
        body.Children.Add(sources);
        body.Children.Add(Ui.Text("Enable the music widget to see available sources.", 11, "Muted"));
        body.Children.Add(Ui.Gap(16));
        body.Children.Add(Ui.Text("SoundCloud queue controls", 15, weight: FontWeights.SemiBold));
        body.Children.Add(Ui.Gap(6));
        body.Children.Add(Ui.Text("The optional YoWidgets SoundCloud Bridge connects Shuffle and Repeat to the real browser player. Install the extension once, then reload SoundCloud. Native players use Windows media controls.", 12, "Muted"));
        UpdateBridgeStatus();
        body.Children.Add(bridgeStatus);
        body.Children.Add(Ui.Gap(20));
        body.Children.Add(Ui.Text("Windows notifications", 17, weight: FontWeights.SemiBold));
        body.Children.Add(Ui.Gap(8));
        body.Children.Add(Ui.Text("Package identity and Windows permission are required. This feed does not block or delete original notifications.", 12, "Muted"));
        body.Children.Add(Ui.Toggle("Enable notification access", manager.Settings.NotificationsEnabled, v => { manager.Settings.NotificationsEnabled = v; manager.Save(); if (!v) manager.Data.Notifications.Suspend(); else if (!manager.Snoozed) manager.Data.Notifications.Enable(); }));
        body.Children.Add(Ui.Button("Request access", async () => { if (manager.Snoozed) { Status("Resume widgets before requesting access"); return; } if (!manager.Settings.NotificationsEnabled) { Status("Enable notification access first"); return; } await manager.Data.Notifications.RefreshAsync(true); Status(manager.Data.Notifications.Status); }));
        body.Children.Add(Ui.Gap(24));
        p.Children.Add(Ui.Panel(body, 24));
        page.Content = p;
    }
    void BuildCleanup()
    {
        var p = Heading("Leave your desktop clear.", "Pause everything, or remove your installation.");
        var body = Ui.Stack(Ui.Text("Disable all", 18, weight: FontWeights.SemiBold), Ui.Gap(8), Ui.Text("Hide widgets, stop updates and disable launch at sign-in. Your settings stay saved.", 13, "Muted"), Ui.Gap(14), Ui.Button("Disable all", () => { manager.DisableAll(); Status("Widgets, updates and launch at sign-in disabled"); }), Ui.Gap(28), Ui.Text("Uninstall completely", 18, "Danger", FontWeights.SemiBold), Ui.Gap(8), Ui.Text("Removes this YoWidgets installation, startup entry, package identity, settings, caches, copied photos and saved key. Original photos, examples and Windhawk are preserved. This cannot be undone.", 13, "Muted"), Ui.Gap(14));
        var restore = Ui.Toggle("Also restore volume and brightness changed by YoWidgets", false, _ => { });
        body.Children.Add(restore);
        var installed = Cleanup.IsInstalled;
        var uninstall = Ui.Button("Uninstall completely", async () => { if (restore.IsChecked == true) { var issues = await manager.Data.Controls.RestoreAsync(); if (issues.Length > 0) { Status("Uninstall stopped: " + issues + ". You can deselect restore and try again."); return; } } Status("Uninstalling YoWidgets…"); Cleanup.LaunchUninstall(); });
        uninstall.IsEnabled = installed;
        uninstall.ToolTip = installed ? "Remove this user installation" : "This is a development build. Install with scripts/install.ps1; source files are preserved.";
        body.Children.Add(uninstall);
        body.Children.Add(Ui.Gap(12));
        body.Children.Add(Ui.Text(installed ? "Uses the verified installation folder for this Windows account." : "Development build: source deletion is blocked. You can disable widgets or exit.", 12, "Muted"));
        body.Children.Add(Ui.Gap(20));
        body.Children.Add(Ui.Button("Quit YoWidgets", App.CurrentApp.ExitApp));
        p.Children.Add(Ui.Panel(body, 24));
        page.Content = p;
    }
}
