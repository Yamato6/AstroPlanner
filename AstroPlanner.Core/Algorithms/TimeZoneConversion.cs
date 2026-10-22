namespace AstroPlanner.Core.Algorithms;

/// <summary>
/// Conversión entre la hora "de pared" del observador y UTC, que es lo que
/// requieren los cálculos astronómicos.
/// </summary>
public static class TimeZoneConversion
{
    /// <summary>
    /// Busca una zona horaria del sistema por id; si no existe o es inválida,
    /// devuelve la zona local.
    /// </summary>
    public static TimeZoneInfo FindOrLocal(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Local;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Local;
        }
    }

    /// <summary>
    /// Convierte una fecha/hora "de pared" en <paramref name="timeZone"/> a UTC.
    /// </summary>
    public static DateTime ToUtc(DateTime localWallClock, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        var wallClock = DateTime.SpecifyKind(localWallClock, DateTimeKind.Unspecified);

        // Una hora inexistente ocurre durante el cambio a horario de verano.
        // Se desplaza a la primera hora válida para mantener el cálculo usable.
        if (timeZone.IsInvalidTime(wallClock))
        {
            wallClock = wallClock.AddHours(1);
        }

        // Para una hora ambigua (fin del horario de verano), ConvertTimeToUtc
        // aplica de forma determinista el desplazamiento estándar de la zona.
        return TimeZoneInfo.ConvertTimeToUtc(wallClock, timeZone);
    }
}
