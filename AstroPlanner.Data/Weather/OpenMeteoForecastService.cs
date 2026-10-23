using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using AstroPlanner.Core.Interfaces;
using AstroPlanner.Core.Models;

namespace AstroPlanner.Data.Weather;

/// <summary>
/// Pronóstico de nubes de Open-Meteo (https://open-meteo.com): gratuito y sin
/// clave. Las coordenadas se envían redondeadas a 2 decimales (~1 km), que es
/// más que suficiente para un pronóstico y evita mandar la ubicación exacta.
/// </summary>
public sealed class OpenMeteoForecastService(HttpClient http, TimeProvider? timeProvider = null) : IWeatherForecastService
{
    public const string BaseUrl = "https://api.open-meteo.com/v1/forecast";

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    public static string BuildUrl(double latitude, double longitude) =>
        FormattableString.Invariant($"{BaseUrl}?latitude={Math.Round(latitude, 2):0.00}&longitude={Math.Round(longitude, 2):0.00}")
        + "&hourly=cloud_cover,cloud_cover_low,cloud_cover_mid,cloud_cover_high,precipitation_probability,relative_humidity_2m"
        + "&timezone=GMT&forecast_days=7";

    public async Task<WeatherForecast> GetForecastAsync(
        double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        OpenMeteoResponse? response;
        try
        {
            using var message = await http.GetAsync(BuildUrl(latitude, longitude), cancellationToken);
            if (!message.IsSuccessStatusCode)
            {
                throw new WeatherForecastException($"El servicio del tiempo respondió {(int)message.StatusCode}.");
            }

            await using var stream = await message.Content.ReadAsStreamAsync(cancellationToken);
            response = await JsonSerializer.DeserializeAsync<OpenMeteoResponse>(stream, cancellationToken: cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new WeatherForecastException("Sin conexión con el servicio del tiempo.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new WeatherForecastException("El servicio del tiempo no respondió a tiempo.", ex);
        }
        catch (JsonException ex)
        {
            throw new WeatherForecastException("Respuesta del servicio del tiempo no válida.", ex);
        }

        return Parse(response, latitude, longitude, _time.GetUtcNow().UtcDateTime);
    }

    private static WeatherForecast Parse(OpenMeteoResponse? response, double latitude, double longitude, DateTime fetchedAtUtc)
    {
        var hourly = response?.Hourly;
        if (hourly?.Time is not { Count: > 0 } times || hourly.CloudCover is not { } cloud || cloud.Count != times.Count)
        {
            throw new WeatherForecastException("Respuesta del servicio del tiempo incompleta.");
        }

        var hours = new List<HourlyWeather>(times.Count);
        for (var i = 0; i < times.Count; i++)
        {
            if (cloud[i] is not { } cover
                || !DateTime.TryParseExact(times[i], "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var time))
            {
                continue; // horas sin dato: se omiten
            }

            hours.Add(new HourlyWeather(
                DateTime.SpecifyKind(time, DateTimeKind.Utc),
                cover,
                ValueAt(hourly.CloudCoverLow, i),
                ValueAt(hourly.CloudCoverMid, i),
                ValueAt(hourly.CloudCoverHigh, i),
                ValueAt(hourly.PrecipitationProbability, i),
                ValueAt(hourly.RelativeHumidity, i)));
        }

        return new WeatherForecast(latitude, longitude, hours, fetchedAtUtc);
    }

    private static int? ValueAt(List<int?>? values, int index) =>
        values is not null && index < values.Count ? values[index] : null;

    private sealed class OpenMeteoResponse
    {
        [JsonPropertyName("hourly")] public HourlyData? Hourly { get; set; }
    }

    private sealed class HourlyData
    {
        [JsonPropertyName("time")] public List<string>? Time { get; set; }
        [JsonPropertyName("cloud_cover")] public List<int?>? CloudCover { get; set; }
        [JsonPropertyName("cloud_cover_low")] public List<int?>? CloudCoverLow { get; set; }
        [JsonPropertyName("cloud_cover_mid")] public List<int?>? CloudCoverMid { get; set; }
        [JsonPropertyName("cloud_cover_high")] public List<int?>? CloudCoverHigh { get; set; }
        [JsonPropertyName("precipitation_probability")] public List<int?>? PrecipitationProbability { get; set; }
        [JsonPropertyName("relative_humidity_2m")] public List<int?>? RelativeHumidity { get; set; }
    }
}
