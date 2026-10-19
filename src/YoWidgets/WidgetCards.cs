using System.Windows.Controls.Primitives;
namespace YoWidgets;

internal static class WidgetCards
{
    static void Bind(FrameworkElement element, DataServices data, Action update)
    {
        element.Loaded += (_, _) => { data.Changed += update; update(); };
        element.Unloaded += (_, _) => data.Changed -= update;
    }
    public static FrameworkElement Clock(WidgetManager manager, WidgetState state)
    {
        var date = WidgetVisuals.Label("", 12, true, secondary: true);
        date.HorizontalAlignment = HorizontalAlignment.Center;
        var time = WidgetVisuals.Label("", state.Size == "S" ? 47 : 76);
        time.FontFamily = new("Segoe UI Variable Display");
        time.FontWeight = FontWeights.Medium;
        time.HorizontalAlignment = HorizontalAlignment.Center;
        var digital = Ui.Stack(date, Ui.Gap(3), time);
        digital.VerticalAlignment = VerticalAlignment.Center;
        var analog = new AnalogClock();
        var root = new Grid();
        var fit = new Viewbox { Child = digital, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
        root.Children.Add(fit);
        root.Children.Add(analog);
        void Update()
        {
            var now = DateTime.Now;
            fit.Visibility = manager.Settings.Analog ? Visibility.Collapsed : Visibility.Visible;
            analog.Visibility = manager.Settings.Analog ? Visibility.Visible : Visibility.Collapsed;
            date.Text = now.ToString("ddd, d MMMM");
            time.Text = now.ToString(manager.Settings.Hour24 ? (manager.Settings.Seconds ? "HH:mm:ss" : "HH:mm") : (manager.Settings.Seconds ? "h:mm:ss" : "h:mm"));
            if (manager.Settings.Analog)
            {
                analog.Time = now;
                analog.Seconds = manager.Settings.Seconds;
                analog.InvalidateVisual();
            }
        }
        Bind(root, manager.Data, Update);
        return root;
    }
    public static FrameworkElement Calendar(WidgetManager manager)
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new()
        {
            Width = new GridLength(0)
        });
        root.ColumnDefinitions.Add(new());
        var weekday = WidgetVisuals.Label("", 11, true);
        weekday.Foreground = new SolidColorBrush(Color.FromRgb(255, 153, 147));
        var day = WidgetVisuals.Label("", 48);
        day.FontWeight = FontWeights.Light;
        var today = Ui.Stack(weekday, Ui.Gap(2), day);
        today.VerticalAlignment = VerticalAlignment.Center;
        root.Children.Add(today);
        var month = WidgetVisuals.Label("", 12, true);
        month.Margin = new Thickness(0, 0, 0, 7);
        var dates = new UniformGrid { Columns = 7, Rows = 7, Height = 105 };
        var right = Ui.Stack(month, dates);
        right.VerticalAlignment = VerticalAlignment.Center;
        SetColumn(right, 1);
        root.Children.Add(right);
        var rendered = DateTime.MinValue;
        void Update()
        {
            var now = DateTime.Today;
            if (rendered == now)
                return;
            rendered = now;
            weekday.Text = now.ToString("dddd");
            day.Text = now.Day.ToString();
            month.Text = now.ToString("MMMM yyyy");
            dates.Children.Clear();
            foreach (var letter in new[] { "П", "В", "С", "Ч", "П", "С", "В" })
            {
                var label = WidgetVisuals.Label(letter, 9, secondary: true);
                label.HorizontalAlignment = HorizontalAlignment.Center;
                dates.Children.Add(label);
            }
            var first = new DateTime(now.Year, now.Month, 1);
            for (var i = 0; i < ((int)first.DayOfWeek + 6) % 7; i++)
                dates.Children.Add(new Border());
            for (var i = 1; i <= DateTime.DaysInMonth(now.Year, now.Month); i++)
            {
                var number = WidgetVisuals.Label(i.ToString(), 10, i == now.Day);
                number.HorizontalAlignment = HorizontalAlignment.Center;
                var cell = new Border { Width = 16, Height = 15, CornerRadius = new CornerRadius(7.5), Child = number, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                if (i == now.Day)
                {
                    cell.Background = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255));
                    number.Foreground = new SolidColorBrush(Color.FromRgb(25, 32, 43));
                }
                dates.Children.Add(cell);
            }
        }
        root.SizeChanged += (_, _) => { var wide = root.ActualWidth > 230; root.ColumnDefinitions[0].Width = new GridLength(wide ? 86 : 0); today.Visibility = wide ? Visibility.Visible : Visibility.Collapsed; right.Margin = new Thickness(wide ? 12 : 0, 0, 0, 0); };
        Bind(root, manager.Data, Update);
        return root;
    }
    public static FrameworkElement Weather(WidgetManager manager, WidgetState state)
    {
        var root = new Grid();
        root.RowDefinitions.Add(new()
        {
            Height = new GridLength(22)
        });
        root.RowDefinitions.Add(new());
        root.RowDefinitions.Add(new()
        {
            Height = GridLength.Auto
        });
        var header = new Grid();
        var city = WidgetVisuals.Label("Погода", 13, true);
        header.Children.Add(city);
        var icon = WidgetVisuals.Icon("sun", 22);
        icon.HorizontalAlignment = HorizontalAlignment.Right;
        header.Children.Add(icon);
        root.Children.Add(header);
        var degrees = WidgetVisuals.Label("—°", 52);
        degrees.FontWeight = FontWeights.Light;
        var condition = WidgetVisuals.Label("", 11, secondary: true);
        condition.HorizontalAlignment = HorizontalAlignment.Right;
        condition.VerticalAlignment = VerticalAlignment.Center;
        condition.TextWrapping = TextWrapping.Wrap;
        condition.MaxWidth = 160;
        var current = new Grid();
        current.ColumnDefinitions.Add(new());
        current.ColumnDefinitions.Add(new());
        current.Children.Add(degrees);
        SetColumn(condition, 1);
        current.Children.Add(condition);
        SetRow(current, 1);
        root.Children.Add(current);
        var forecast = new UniformGrid { Rows = 1, Height = 34, Margin = new Thickness(0, 5, 0, 0) };
        SetRow(forecast, 2);
        root.Children.Add(forecast);
        string forecastKey = "";
        double Temp(double value) => manager.Settings.Fahrenheit ? value * 1.8 + 32 : value;
        void Update()
        {
            var weather = manager.Data.Weather;
            city.Text = weather.Temperature == null ? manager.Settings.City : weather.City;
            degrees.Text = weather.Temperature is double value ? Math.Round(Temp(value)) + "°" : "—°";
            condition.Text = weather.Loading ? "Обновление…" : weather.Error.Length > 0 ? "Нет связи · сохранённый прогноз" : weather.Condition + (weather.Temperature == null ? "" : $"\n↑{Temp(weather.High):0}°  ↓{Temp(weather.Low):0}°");
            root.ToolTip = weather.Updated == null ? "Нет данных" : weather.Source + " · " + weather.Updated.Value.ToLocalTime().ToString("g");
            forecast.Visibility = state.Size == "S" ? Visibility.Collapsed : Visibility.Visible;
            if (root.ActualWidth < 160)
            {
                current.ColumnDefinitions[1].Width = new GridLength(0);
                condition.Visibility = Visibility.Collapsed;
            }
            else
            {
                current.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
                condition.Visibility = Visibility.Visible;
            }
            var key = weather.ForecastFor(manager.Settings.Fahrenheit, state.Size == "L");
            if (key == forecastKey)
                return;
            forecastKey = key;
            forecast.Children.Clear();
            var points = state.Size == "L" ? weather.Days : weather.Hours;
            foreach (var point in points.Take(4))
            {
                var label = WidgetVisuals.Label(point.Time, 9, secondary: true);
                label.HorizontalAlignment = HorizontalAlignment.Center;
                var temperature = WidgetVisuals.Label(Math.Round(Temp(point.Temperature)) + "°", 12, true);
                temperature.HorizontalAlignment = HorizontalAlignment.Center;
                forecast.Children.Add(Ui.Stack(label, Ui.Gap(2), temperature));
            }
        }
        Bind(root, manager.Data, Update);
        root.SizeChanged += (_, _) => Update();
        return root;
    }
    static void SetColumn(UIElement element, int column) => Grid.SetColumn(element, column);
    static void SetRow(UIElement element, int row) => Grid.SetRow(element, row);
}
