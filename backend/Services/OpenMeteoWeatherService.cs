using System.Globalization;
using System.Text.Json;

namespace Virexaone.FMS.Backend.Services;

public sealed record WeatherReportDto(
    double Latitude,
    double Longitude,
    double TemperatureC,
    double HumidityPercent,
    double PrecipitationMm,
    double RainMm,
    double ShowersMm,
    double WindSpeedKmh,
    int WeatherCode,
    bool IsRaining,
    string ModelTimeUtc,
    string FetchedAtUtc,
    string Source
);

public sealed class OpenMeteoWeatherService
{
    private readonly HttpClient _client;
    private readonly string? _apiKey;
    private readonly bool _allowFreeEvaluation;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly Dictionary<string, (DateTime fetchedAt, WeatherReportDto report)> _cache = new();

    public OpenMeteoWeatherService(HttpClient client, IConfiguration configuration, IHostEnvironment environment)
    {
        _client = client;
        _client.Timeout = TimeSpan.FromSeconds(10);
        _apiKey = configuration["OpenMeteo:ApiKey"];
        _allowFreeEvaluation = environment.IsDevelopment() ||
            configuration.GetValue<bool>("OpenMeteo:AllowFreeForEvaluation");
    }

    public async Task<WeatherReportDto?> GetCurrentAsync(double latitude, double longitude)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) && !_allowFreeEvaluation) return null;
        double gridLat = Math.Round(latitude, 3);
        double gridLon = Math.Round(longitude, 3);
        string key = $"{gridLat:F3},{gridLon:F3}";
        await _refreshGate.WaitAsync();
        try
        {
            if (_cache.TryGetValue(key, out var cached) && DateTime.UtcNow - cached.fetchedAt < TimeSpan.FromMinutes(5))
                return cached.report;

            string host = string.IsNullOrWhiteSpace(_apiKey)
                ? "https://api.open-meteo.com"
                : "https://customer-api.open-meteo.com";
            string url = host + "/v1/forecast?latitude=" +
                gridLat.ToString("F3", CultureInfo.InvariantCulture) + "&longitude=" +
                gridLon.ToString("F3", CultureInfo.InvariantCulture) +
                "&current=temperature_2m,relative_humidity_2m,precipitation,rain,showers,weather_code,wind_speed_10m" +
                "&timezone=GMT&forecast_days=1" +
                (string.IsNullOrWhiteSpace(_apiKey) ? "" : "&apikey=" + Uri.EscapeDataString(_apiKey));
            try
            {
                using var response = await _client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
                var current = json.RootElement.GetProperty("current");
                double rain = current.GetProperty("rain").GetDouble();
                double showers = current.GetProperty("showers").GetDouble();
                int code = current.GetProperty("weather_code").GetInt32();
                bool raining = rain > 0.05 || showers > 0.05 || (code >= 51 && code <= 67) ||
                    (code >= 80 && code <= 82) || code >= 95;
                var report = new WeatherReportDto(
                    gridLat, gridLon,
                    current.GetProperty("temperature_2m").GetDouble(),
                    current.GetProperty("relative_humidity_2m").GetDouble(),
                    current.GetProperty("precipitation").GetDouble(),
                    rain, showers,
                    current.GetProperty("wind_speed_10m").GetDouble(),
                    code, raining,
                    current.GetProperty("time").GetString() + "Z",
                    DateTime.UtcNow.ToString("o"),
                    "Open-Meteo model current"
                );
                if (_cache.Count > 32) _cache.Clear();
                _cache[key] = (DateTime.UtcNow, report);
                return report;
            }
            catch (Exception)
            {
                Console.WriteLine("[OpenMeteoWeatherService] Weather request failed.");
                return null;
            }
        }
        finally
        {
            _refreshGate.Release();
        }
    }
}
