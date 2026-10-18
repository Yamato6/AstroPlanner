using AstroPlanner.Core.Models;

namespace AstroPlanner.UI.Services;

/// <summary>
/// Configuración persistida del observador.
/// Se guarda/lee como JSON en %LocalAppData%\AstroPlanner\settings.json.
/// </summary>
public sealed class ObserverSettings
{
    public double LatitudeDegrees { get; set; } = 40.4168;
    public double LongitudeDegrees { get; set; } = -3.7038;
    public double ElevationMeters { get; set; } = 667;

    /// <summary>
    /// Id de zona horaria del sistema (ej. "Romance Standard Time").
    /// Se usa para convertir la fecha/hora local elegida por el usuario a UTC
    /// antes de los cálculos astronómicos.
    /// </summary>
    public string TimeZoneId { get; set; } = TimeZoneInfo.Local.Id;

    /// <summary>Umbral de altitud mínimo (grados) para considerar un objeto visible.</summary>
    public double MinimumAltitude { get; set; } = 20;

    public ObserverLocation ToObserverLocation() =>
        new(LatitudeDegrees, LongitudeDegrees, ElevationMeters);

    public TimeZoneInfo GetTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            // Si el id guardado ya no existe (cambio de SO), se usa la local.
            return TimeZoneInfo.Local;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Local;
        }
    }
}
