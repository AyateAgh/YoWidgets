using System.Globalization;
using System.Text.Json;
namespace YoWidgets.Core;

public record ForecastPoint(string Time, double Temperature);
public record WeatherReading(double Temperature, string Condition, double High, double Low, List<ForecastPoint> Hours, List<ForecastPoint> Days);
public static class WeatherFallback
{
    public static WeatherReading Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var current = root.GetProperty("current_condition")[0];
        var days = root.GetProperty("weather");
        var day = days[0];
        double Number(JsonElement o, string key)
        {
            var p = o.GetProperty(key);
            var value = p.ValueKind == JsonValueKind.String ? double.Parse(p.GetString()!, CultureInfo.InvariantCulture) : p.GetDouble();
            if (!double.IsFinite(value) || value < -150 || value > 150)
                throw new InvalidDataException("Invalid temperature");
            return value;
        }
        var condition = current.TryGetProperty("lang_ru", out var ru) ? ru[0].GetProperty("value").GetString() : current.GetProperty("weatherDesc")[0].GetProperty("value").GetString();
        var observed = current.TryGetProperty("localObsDateTime", out var observedText) && DateTime.TryParse(observedText.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var localTime) ? localTime : DateTime.MinValue;
        var hours = days.EnumerateArray().SelectMany(d =>
        {
            var date = DateTime.Parse(d.GetProperty("date").GetString()!, CultureInfo.InvariantCulture);
            return d.GetProperty("hourly").EnumerateArray().Select(h =>
            {
                var hour = int.Parse(h.GetProperty("time").GetString()!, CultureInfo.InvariantCulture) / 100;
                return (At: date.AddHours(hour), Temperature: Number(h, "tempC"));
            });
        }).Where(h => h.At >= observed).Take(4).Select(h => new ForecastPoint(h.At.ToString("HH:mm"), h.Temperature)).ToList();
        var forecast = days.EnumerateArray().Take(4).Select(d => new ForecastPoint(DateTime.Parse(d.GetProperty("date").GetString()!, CultureInfo.InvariantCulture).ToString("ddd", CultureInfo.GetCultureInfo("ru-RU")), Number(d, "maxtempC"))).ToList();
        return new(Number(current, "temp_C"), condition ?? "Погода", Number(day, "maxtempC"), Number(day, "mintempC"), hours, forecast);
    }
}
