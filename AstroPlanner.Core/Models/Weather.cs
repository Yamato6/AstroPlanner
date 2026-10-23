namespace AstroPlanner.Core.Models;

/// <summary>Pronóstico de una hora. Los porcentajes van de 0 a 100.</summary>
/// <param name="TimeUtc">Inicio de la hora, en UTC.</param>
public sealed record HourlyWeather(
    DateTime TimeUtc,
    int CloudCoverPercent,
    int? LowCloudPercent = null,
    int? MidCloudPercent = null,
    int? HighCloudPercent = null,
    int? PrecipitationProbabilityPercent = null,
    int? RelativeHumidityPercent = null);

/// <summary>Resumen de nubosidad de un intervalo (normalmente, la noche oscura).</summary>
/// <param name="AverageCloudCoverPercent">Nubosidad media del intervalo.</param>
/// <param name="ClearestHourUtc">Hora con menos nubes (la primera si hay empate).</param>
public sealed record CloudSummary(
    double AverageCloudCoverPercent,
    int MinimumCloudCoverPercent,
    int MaximumCloudCoverPercent,
    DateTime ClearestHourUtc,
    int ClearestHourCloudCoverPercent);

/// <summary>Pronóstico por horas para una ubicación.</summary>
public sealed class WeatherForecast
{
    public WeatherForecast(double latitude, double longitude, IReadOnlyList<HourlyWeather> hours, DateTime fetchedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(hours);
        Latitude = latitude;
        Longitude = longitude;
        Hours = hours.OrderBy(h => h.TimeUtc).ToList();
        FetchedAtUtc = fetchedAtUtc;
    }

    public double Latitude { get; }
    public double Longitude { get; }
    public IReadOnlyList<HourlyWeather> Hours { get; }
    public DateTime FetchedAtUtc { get; }

    /// <summary>La hora del pronóstico que contiene <paramref name="utc"/>, o null si queda fuera.</summary>
    public HourlyWeather? At(DateTime utc)
    {
        foreach (var hour in Hours)
        {
            if (utc >= hour.TimeUtc && utc < hour.TimeUtc.AddHours(1))
            {
                return hour;
            }
        }

        return null;
    }

    /// <summary>Horas que se solapan con [<paramref name="startUtc"/>, <paramref name="endUtc"/>).</summary>
    public IReadOnlyList<HourlyWeather> Between(DateTime startUtc, DateTime endUtc) =>
        Hours.Where(h => h.TimeUtc.AddHours(1) > startUtc && h.TimeUtc < endUtc).ToList();

    /// <summary>
    /// Resumen de nubosidad del intervalo, o null si el pronóstico no lo cubre
    /// (fechas pasadas o más allá del horizonte de pronóstico).
    /// </summary>
    public CloudSummary? Summarize(DateTime startUtc, DateTime endUtc)
    {
        var hours = Between(startUtc, endUtc);
        if (hours.Count == 0)
        {
            return null;
        }

        var clearest = hours.MinBy(h => h.CloudCoverPercent)!;
        return new CloudSummary(
            hours.Average(h => h.CloudCoverPercent),
            hours.Min(h => h.CloudCoverPercent),
            hours.Max(h => h.CloudCoverPercent),
            clearest.TimeUtc,
            clearest.CloudCoverPercent);
    }

    /// <summary>Descripción en palabras de un porcentaje de nubosidad.</summary>
    public static string Describe(double cloudCoverPercent) => cloudCoverPercent switch
    {
        < 15 => "Despejado",
        < 40 => "Poco nublado",
        < 70 => "Parcialmente nublado",
        < 90 => "Mayormente nublado",
        _ => "Cubierto"
    };
}

/// <summary>El pronóstico no se pudo obtener (sin conexión, error del servicio, respuesta inválida).</summary>
public sealed class WeatherForecastException(string message, Exception? innerException = null)
    : Exception(message, innerException);
