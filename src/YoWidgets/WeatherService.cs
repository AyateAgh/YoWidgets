using System.Text.Json;
namespace YoWidgets;

internal sealed class WeatherService
{
    public record Hour(string Time, double Temperature);
    sealed record Cache(string City, string Condition, double Temperature, double High, double Low, List<Hour> Hours, List<Hour> Days, DateTimeOffset Updated, string Source = "Open-Meteo");
    static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(15) };
    static string CachePath => Path.Combine(App.DataRoot, "weather-cache.json");
    int generation;
    public string City { get; private set; } = "Выберите город";
    public string Condition { get; private set; } = "Нет данных";
    public double? Temperature
    {
        get; private set;
    }
    public double High
    {
        get; private set;
    }
    public double Low
    {
        get; private set;
    }
    public string Error { get; private set; } = "";
    public bool Loading
    {
        get; private set;
    }
    public DateTimeOffset? Updated
    {
        get; private set;
    }
    public string Source { get; private set; } = "Open-Meteo";
    public List<Hour> Hours { get; private set; } = [];
    public List<Hour> Days { get; private set; } = [];
    public event Action? Changed;

    static WeatherService() => http.DefaultRequestHeaders.UserAgent.ParseAdd("YoWidgets/0.1");
    public WeatherService()
    {
        try
        {
            if (!File.Exists(CachePath) || new FileInfo(CachePath).Length > 256000)
                return;
            var c = JsonSerializer.Deserialize<Cache>(File.ReadAllText(CachePath));
            if (c == null || !double.IsFinite(c.Temperature))
                return;
            City = c.City;
            Condition = c.Condition;
            Temperature = c.Temperature;
            High = c.High;
            Low = c.Low;
            Hours = c.Hours ?? [];
            Days = c.Days ?? [];
            Updated = c.Updated;
            Source = c.Source;
        }
        catch { /* A bad optional cache never prevents startup. */ }
    }
    void SaveCache()
    {
        try
        {
            if (Temperature == null || Updated == null)
                return;
            File.WriteAllText(CachePath + ".tmp", JsonSerializer.Serialize(new Cache(City, Condition, Temperature.Value, High, Low, Hours, Days, Updated.Value, Source)));
            File.Move(CachePath + ".tmp", CachePath, true);
        }
        catch (Exception e) { App.Log("weather-cache", e); }
    }
    public string ForecastFor(bool fahrenheit, bool daily)
    {
        double Temp(double v) => fahrenheit ? v * 1.8 + 32 : v;
        return string.Join("     ", (daily ? Days : Hours).Take(4).Select(h => h.Time + "  " + Math.Round(Temp(h.Temperature)) + "°"));
    }
    public async Task RefreshAsync(string city, CancellationToken ct)
    {
        var request = Interlocked.Increment(ref generation);
        Loading = true;
        Error = "";
        Changed?.Invoke();
        try
        {
            using var geo = JsonDocument.Parse(await http.GetStringAsync("https://geocoding-api.open-meteo.com/v1/search?count=1&language=ru&name=" + Uri.EscapeDataString(city), ct));
            if (!geo.RootElement.TryGetProperty("results", out var list) || list.GetArrayLength() == 0)
                throw new InvalidOperationException("Город не найден. Уточните название.");
            var p = list[0];
            var lat = p.GetProperty("latitude").GetRawText();
            var lon = p.GetProperty("longitude").GetRawText();
            using var doc = JsonDocument.Parse(await http.GetStringAsync($"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,weather_code&daily=temperature_2m_max,temperature_2m_min&hourly=temperature_2m&forecast_days=5&timezone=auto", ct));
            if (request != generation || ct.IsCancellationRequested)
                return;
            var root = doc.RootElement;
            var current = root.GetProperty("current");
            var daily = root.GetProperty("daily");
            City = p.GetProperty("name").GetString() ?? city;
            Temperature = current.GetProperty("temperature_2m").GetDouble();
            High = daily.GetProperty("temperature_2m_max")[0].GetDouble();
            Low = daily.GetProperty("temperature_2m_min")[0].GetDouble();
            Condition = Describe(current.GetProperty("weather_code").GetInt32());
            var hourly = root.GetProperty("hourly");
            var now = current.GetProperty("time").GetString()!;
            var times = hourly.GetProperty("time").EnumerateArray().Select(x => x.GetString()!).ToArray();
            var index = Math.Max(0, Array.FindIndex(times, t => string.CompareOrdinal(t, now) >= 0));
            Hours = Enumerable.Range(index, Math.Min(4, times.Length - index)).Select(i => new Hour(times[i][11..16], hourly.GetProperty("temperature_2m")[i].GetDouble())).ToList();
            Days = Enumerable.Range(0, Math.Min(4, daily.GetProperty("time").GetArrayLength())).Select(i => new Hour(DateTime.Parse(daily.GetProperty("time")[i].GetString()!).ToString("ddd"), daily.GetProperty("temperature_2m_max")[i].GetDouble())).ToList();
            Source = "Open-Meteo";
            Updated = DateTimeOffset.Now;
            SaveCache();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
        catch (Exception e)
        {
            if (request != generation || ct.IsCancellationRequested)
                return;
            App.Log("weather-primary", e);
            if (e is HttpRequestException or TaskCanceledException)
            {
                try
                {
                    var raw = await http.GetStringAsync("https://wttr.in/" + Uri.EscapeDataString(city) + "?format=j1&lang=ru", ct);
                    var result = WeatherFallback.Parse(raw);
                    if (request != generation || ct.IsCancellationRequested)
                        return;
                    City = city;
                    Temperature = result.Temperature;
                    Condition = result.Condition;
                    High = result.High;
                    Low = result.Low;
                    Hours = result.Hours.Select(h => new Hour(h.Time, h.Temperature)).ToList();
                    Days = result.Days.Select(h => new Hour(h.Time, h.Temperature)).ToList();
                    Source = "wttr.in · резерв";
                    Updated = DateTimeOffset.Now;
                    SaveCache();
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                catch (Exception fallbackError) { if (request == generation) Error = "Нет связи с погодой. Сохранённые данные могут устареть."; App.Log("weather-fallback", fallbackError); }
            }
            else
                Error = e is InvalidOperationException ? e.Message : "Не удалось прочитать прогноз. Повторите позже.";
        }
        finally { if (request == generation) { Loading = false; Changed?.Invoke(); } }
    }
    static string Describe(int c) => c switch
    {
        0 => "Ясно",
        1 => "Преимущественно ясно",
        2 => "Переменная облачность",
        3 => "Облачно",
        45 or 48 => "Туман",
        >= 51 and <= 57 => "Морось",
        >= 61 and <= 67 => "Дождь",
        >= 71 and <= 77 => "Снег",
        >= 80 and <= 82 => "Ливень",
        >= 85 and <= 86 => "Снегопад",
        >= 95 => "Гроза",
        _ => "Погода"
    };
}
