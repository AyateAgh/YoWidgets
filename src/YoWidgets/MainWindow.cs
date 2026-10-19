namespace YoWidgets;

internal sealed class MainWindow : Window
{
    readonly WidgetManager manager; readonly ContentControl page = new(); readonly TextBlock status = Ui.Text("Настройки хранятся на этом компьютере", 11, "Muted"); readonly TextBlock active = Ui.Text("", 12, "Muted"); string tab = "widgets"; readonly List<Button> nav = [];
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
        FontFamily = new("Segoe UI Variable Text");
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
        AddNav(navigation, "◫   Виджеты", "widgets");
        AddNav(navigation, "◈   Оформление", "appearance");
        AddNav(navigation, "⚙   Настройки", "settings");
        AddNav(navigation, "↙   Отключение и удаление", "cleanup");
        Grid.SetRow(navigation, 1);
        sidebar.Children.Add(navigation);
        var footer = Ui.Stack(Ui.Text("●  Локально. Под вашим контролем.", 10, "Muted"), Ui.Gap(10), Ui.Text("YoWidgets  0.2 · Windows 11", 10, "Muted"));
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
        actions.Children.Add(Ui.Button("Snooze", () => { manager.SetSnooze(!manager.Snoozed); Status(manager.Snoozed ? "Snooze: виджеты скрыты, обновления приостановлены" : "Обновления возобновлены"); }));
        actions.Children.Add(Ui.Button("Отключить всё", () => { manager.DisableAll(); Status("Все виджеты и автозапуск выключены"); }));
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
        var minimize = Ui.Button("−", () => WindowState = WindowState.Minimized);
        minimize.ToolTip = "Свернуть";
        minimize.Content = new System.Windows.Shapes.Rectangle { Width = 12, Height = 1, Fill = Ui.Brush("Text") };
        var close = Ui.Button("✕", Close);
        close.ToolTip = "Закрыть в трей";
        System.Windows.Automation.AutomationProperties.SetName(minimize, "Свернуть");
        System.Windows.Automation.AutomationProperties.SetName(close, "Закрыть в трей");
        windowButtons.Children.Add(minimize);
        windowButtons.Children.Add(close);
        Grid.SetColumn(windowButtons, 1);
        titlebar.Children.Add(windowButtons);
        frame.Children.Add(titlebar);
        Grid.SetRow(shell, 1);
        frame.Children.Add(shell);
        Content = frame;
        manager.Changed += OnChanged;
        Closed += (_, _) => { manager.Changed -= OnChanged; page.Content = null; App.CurrentApp.ReleaseUnusedImages(); };
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
        nav.Add(b);
        p.Children.Add(b);
    }
    void OnChanged()
    {
        active.Text = $"●  {manager.ActiveCount} активных" + (manager.Snoozed ? " · Snooze" : " · обновления по событиям");
        if (tab == "widgets")
            BuildWidgets();
    }
    void Select(string value)
    {
        tab = value;
        foreach (var b in nav)
            b.Background = Ui.Brush((string)b.Tag == value ? "Hover" : "Field");
        active.Text = $"●  {manager.ActiveCount} активных" + (manager.Snoozed ? " · Snooze" : " · обновления по событиям");
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
    void BuildWidgets()
    {
        var p = Heading("Ваш рабочий стол.", "Нужное рядом. Остальное — по вашему желанию.");
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 };
        foreach (var item in WidgetManager.Catalog)
        {
            var body = new StackPanel();
            var preview = new PreviewGrid(item.Title) { Height = 150, Margin = new Thickness(0, 0, 0, 15), ClipToBounds = true };
            var sample = new RectD(100 + (Array.FindIndex(WidgetManager.Catalog, x => x.Kind == item.Kind) % 3) * 310, 120, 270, 158);
            if (!manager.Snoozed)
            {
                var glass = new GlassSurface { Margin = new Thickness(0) };
                preview.Children.Add(glass);
                preview.SizeChanged += (_, _) => glass.Update(manager.Wallpaper.GetBrush(sample), Math.Max(80, preview.ActualWidth - 40), 110, manager.Settings);
            }
            FrameworkElement visual;
            if (manager.Snoozed)
            {
                visual = Ui.Stack(Ui.Text(item.Icon, 38, "Muted"), Ui.Gap(8), Ui.Text("На паузе", 13, "Muted"));
                visual.HorizontalAlignment = HorizontalAlignment.Center;
            }
            else if (item.Kind is "clock" or "calendar" or "weather" or "music")
            {
                visual = WidgetContentFactory.Create(manager, new WidgetState { Kind = item.Kind, Size = "S" }, preview: true);
                visual.IsHitTestVisible = false;
                if (item.Kind == "music")
                {
                    visual = new Viewbox { Child = visual, Stretch = Stretch.Uniform, Height = 110 };
                }
            }
            else
            {
                var icon = Ui.Text(item.Icon, 46, weight: FontWeights.Light);
                icon.Foreground = Brushes.White;
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
            var enabled = manager.Settings.Widgets.Any(w => w.Kind == item.Kind && w.Enabled);
            var toggle = Ui.Toggle("", enabled, v => { if (manager.Snoozed && v) manager.SetSnooze(false); manager.Toggle(item.Kind); });
            toggle.ToolTip = enabled ? "Выключить " + item.Title : "Включить " + item.Title;
            System.Windows.Automation.AutomationProperties.SetName(toggle, (enabled ? "Выключить " : "Включить ") + item.Title);
            Grid.SetColumn(toggle, 1);
            titleRow.Children.Add(toggle);
            body.Children.Add(titleRow);
            body.Children.Add(Ui.Text(item.Detail, 11, "Muted"));
            var more = Ui.Button("+ Ещё один", () => { if (manager.Snoozed) manager.SetSnooze(false); manager.Add(item.Kind); });
            more.Margin = new Thickness(0, 10, 0, 0);
            more.MinHeight = 28;
            more.Padding = new Thickness(8, 4, 8, 4);
            body.Children.Add(more);
            var card = Ui.Panel(body, 15);
            card.Margin = new Thickness(0, 0, 12, 14);
            grid.Children.Add(card);
        }
        p.Children.Add(grid);
        p.Children.Add(Ui.Text("Перетащите виджет за свободную область. Правый клик — размер и выключение.", 11, "Muted"));
        page.Content = p;
    }
    void BuildAppearance()
    {
        var p = Heading("Форма света.", "Настоящее преломление. Спокойный интерфейс.");
        var body = new StackPanel();
        body.Children.Add(Ui.Text("Материал", 17, weight: FontWeights.SemiBold));
        body.Children.Add(Ui.Gap(15));
        AddSlider(body, "Преломление", 0, 2, manager.Settings.GlassStrength, v => manager.Settings.GlassStrength = v);
        AddSlider(body, "Оттенок стекла", .04, .85, manager.Settings.Opacity, v => manager.Settings.Opacity = v);
        AddSlider(body, "Размытие", 0, 28, manager.Settings.Blur, v => manager.Settings.Blur = v);
        body.Children.Add(Ui.Toggle("Светлый оттенок стекла", manager.Settings.Light, v => { manager.Settings.Light = v; manager.ApplyAppearance(); }));
        body.Children.Add(Ui.Toggle("Аналоговые часы", manager.Settings.Analog, v => { manager.Settings.Analog = v; manager.ApplyAppearance(); }));
        body.Children.Add(Ui.Toggle("Температура в °F", manager.Settings.Fahrenheit, v => { manager.Settings.Fahrenheit = v; manager.ApplyAppearance(); }));
        body.Children.Add(Ui.Toggle("Показывать секунды", manager.Settings.Seconds, v => { manager.Settings.Seconds = v; manager.Save(); }));
        body.Children.Add(Ui.Toggle("24-часовой формат", manager.Settings.Hour24, v => { manager.Settings.Hour24 = v; manager.Save(); }));
        body.Children.Add(Ui.Toggle("Поверх обычных окон", manager.Settings.Topmost, v => { manager.Settings.Topmost = v; manager.ApplyAppearance(); }));
        body.Children.Add(Ui.Text("Свободные окна · привязка к Explorer отключена", 12, "Muted"));
        body.Children.Add(Ui.Gap(14));
        body.Children.Add(Ui.Button("Проверить оптику на сетке", manager.ShowOpticsTest));
        body.Children.Add(Ui.Gap(10));
        body.Children.Add(Ui.Button("Сбросить позиции по центру экрана", manager.ResetPositions));
        body.Children.Add(Ui.Gap(14));
        body.Children.Add(Ui.Text("Движение учитывает системную настройку анимаций. Snooze скрывает поверхности и останавливает обновления. Поведение виртуальных столов зависит от оболочки Windows.", 12, "Muted"));
        p.Children.Add(Ui.Panel(body, 24));
        page.Content = p;
    }
    void AddSlider(Panel p, string label, double min, double max, double value, Action<double> update)
    {
        var text = Ui.Text(label + "  " + value.ToString("0.00"), 13);
        p.Children.Add(text);
        p.Children.Add(Ui.Slider(min, max, value, v => { update(v); text.Text = label + "  " + v.ToString("0.00"); manager.ApplyAppearance(); }));
    }
    void BuildSettings()
    {
        var p = Heading("Всё под контролем.", "Разрешения и подключения включаете только вы.");
        var body = new StackPanel();
        body.Children.Add(Ui.Text("Погода", 17, weight: FontWeights.SemiBold));
        body.Children.Add(Ui.Gap(10));
        body.Children.Add(Ui.Text("Город", 12, "Muted"));
        var city = Ui.Input(manager.Settings.City);
        body.Children.Add(city);
        var weatherButton = Ui.Button("Сохранить и обновить", async () => { manager.Settings.City = city.Text; manager.Save(); await manager.Data.RefreshWeatherAsync(); Status(manager.Data.Weather.Error.Length == 0 ? "Погода обновлена" : manager.Data.Weather.Error); }, true);
        weatherButton.Margin = new Thickness(0, 10, 0, 20);
        body.Children.Add(weatherButton);
        body.Children.Add(Ui.Toggle("Тексты песен · название трека передаётся LRCLIB", manager.Settings.LyricsEnabled, v => { manager.Settings.LyricsEnabled = v; manager.Save(); Status(v ? "Тексты появятся при обновлении трека" : "Получение текстов выключено"); }));
        body.Children.Add(Ui.Toggle("Автозапуск при входе в Windows", StartupRegistration.Enabled, v => { try { StartupRegistration.Set(v); Status(v ? "Автозапуск включён" : "Автозапуск выключен"); } catch (Exception e) { Status(e.Message); } }));
        body.Children.Add(Ui.Gap(20));
        body.Children.Add(Ui.Text("Источник музыки", 17, weight: FontWeights.SemiBold));
        var sources = new ComboBox { Margin = new Thickness(0, 8, 0, 8), MinHeight = 34, ItemsSource = new[] { "Автоматически" }.Concat(manager.Data.Media.Sources).ToArray(), SelectedIndex = 0 };
        sources.SelectionChanged += (_, _) => manager.Data.Media.SelectSource(sources.SelectedIndex == 0 ? null : sources.SelectedItem as string);
        body.Children.Add(sources);
        body.Children.Add(Ui.Text("Список доступен после включения музыкального виджета. Меню выбора принадлежит Windows.", 11, "Muted"));
        body.Children.Add(Ui.Gap(20));
        body.Children.Add(Ui.Text("Уведомления Windows", 17, weight: FontWeights.SemiBold));
        body.Children.Add(Ui.Gap(8));
        body.Children.Add(Ui.Text("Нужны package identity и отдельное разрешение Windows. Лента не блокирует системные всплытия и не удаляет оригиналы.", 12, "Muted"));
        body.Children.Add(Ui.Toggle("Включить чтение уведомлений", manager.Settings.NotificationsEnabled, v => { manager.Settings.NotificationsEnabled = v; manager.Save(); if (!v) manager.Data.Notifications.Suspend(); else if (!manager.Snoozed) manager.Data.Notifications.Enable(); }));
        body.Children.Add(Ui.Button("Запросить доступ", async () => { if (manager.Snoozed) { Status("Сначала выйдите из Snooze"); return; } if (!manager.Settings.NotificationsEnabled) { Status("Сначала включите чтение уведомлений"); return; } await manager.Data.Notifications.RefreshAsync(true); Status(manager.Data.Notifications.Status); }));
        body.Children.Add(Ui.Gap(24));
        body.Children.Add(Ui.Text("Ассистент · подключение провайдера", 17, weight: FontWeights.SemiBold));
        body.Children.Add(Ui.Gap(8));
        body.Children.Add(Ui.Text("Текст отправляется только по кнопке «Отправить». Экран, уведомления и фото не включаются. Стоимость определяет ваш провайдер.", 12, "Muted"));
        body.Children.Add(Ui.Gap(10));
        body.Children.Add(Ui.Text("HTTPS endpoint, совместимый с Chat Completions", 12, "Muted"));
        var endpoint = Ui.Input(manager.Settings.AiEndpoint);
        body.Children.Add(endpoint);
        body.Children.Add(Ui.Gap(8));
        body.Children.Add(Ui.Text("Модель", 12, "Muted"));
        var model = Ui.Input(manager.Settings.AiModel);
        body.Children.Add(model);
        body.Children.Add(Ui.Gap(8));
        body.Children.Add(Ui.Text("API-ключ · защищён учётной записью Windows", 12, "Muted"));
        var key = new PasswordBox { Padding = new Thickness(12), Background = Ui.Brush("Field"), Foreground = Ui.Brush("Text"), BorderBrush = Ui.Brush("Line") };
        body.Children.Add(key);
        var saveAi = Ui.Button("Сохранить подключение", () => { if (!Uri.TryCreate(endpoint.Text, UriKind.Absolute, out var u) || u.Scheme != "https") { Status("Укажите корректный HTTPS-адрес"); return; } manager.Settings.AiEndpoint = endpoint.Text.TrimEnd('/'); manager.Settings.AiModel = model.Text.Trim(); if (!string.IsNullOrWhiteSpace(key.Password)) manager.Settings.AiKeyProtected = AiService.Protect(key.Password); key.Clear(); manager.Save(); Status("Подключение сохранено. Запросы ещё не отправлялись."); });
        saveAi.Margin = new Thickness(0, 10, 0, 0);
        body.Children.Add(saveAi);
        body.Children.Add(Ui.Gap(12));
        body.Children.Add(Ui.Button("Удалить сохранённый ключ", () => { manager.Settings.AiKeyProtected = null; manager.Data.Ai.Cancel(); manager.Save(); Status("Ключ удалён"); }));
        p.Children.Add(Ui.Panel(body, 24));
        page.Content = p;
    }
    void BuildCleanup()
    {
        var p = Heading("Без следов в рабочем столе.", "Отключить сейчас или удалить вашу установку.");
        var body = Ui.Stack(Ui.Text("Отключить всё", 18, weight: FontWeights.SemiBold), Ui.Gap(8), Ui.Text("Уберёт виджеты, остановит обновления, выключит автозапуск. Ваши настройки сохранятся.", 13, "Muted"), Ui.Gap(14), Ui.Button("Отключить всё", () => { manager.DisableAll(); Status("Виджеты, обновления и автозапуск выключены"); }), Ui.Gap(28), Ui.Text("Удалить полностью", 18, "Danger", FontWeights.SemiBold), Ui.Gap(8), Ui.Text("Будут удалены установленная копия YoWidgets, её автозапуск, identity, локальные настройки, кеши, копии фото и сохранённый ключ. Исходные фото, examples и Windhawk сохранятся. Это действие нельзя отменить.", 13, "Muted"), Ui.Gap(14));
        var restore = Ui.Toggle("Также вернуть изменённые YoWidgets громкость и яркость", false, _ => { });
        body.Children.Add(restore);
        var installed = Cleanup.IsInstalled;
        var uninstall = Ui.Button("Удалить полностью", async () => { if (restore.IsChecked == true) { var issues = await manager.Data.Controls.RestoreAsync(); if (issues.Length > 0) { Status("Удаление остановлено: " + issues + ". Можно снять выбор отката и повторить."); return; } } Status("Начинается удаление YoWidgets…"); Cleanup.LaunchUninstall(); });
        uninstall.IsEnabled = installed;
        uninstall.ToolTip = installed ? "Удалить эту per-user установку" : "Сейчас запущена сборка из исходников. Установите через scripts/install.ps1; исходники не удаляются.";
        body.Children.Add(uninstall);
        body.Children.Add(Ui.Gap(12));
        body.Children.Add(Ui.Text(installed ? "Используется проверенная папка установки текущего пользователя." : "Запущена сборка из исходников: полное удаление исходного проекта заблокировано. Для неё доступно отключение и обычный выход.", 12, "Muted"));
        body.Children.Add(Ui.Gap(20));
        body.Children.Add(Ui.Button("Полностью выйти из программы", App.CurrentApp.ExitApp));
        p.Children.Add(Ui.Panel(body, 24));
        page.Content = p;
    }
}
