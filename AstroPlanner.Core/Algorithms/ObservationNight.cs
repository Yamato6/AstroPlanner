namespace AstroPlanner.Core.Algorithms;

/// <summary>
/// Ventana de observación nocturna (18:00 → 06:00 en hora local) y búsqueda
/// de cruces de altitud dentro de una serie de muestras.
/// </summary>
public static class ObservationNight
{
    public const int StartHour = 18;

    public static readonly TimeSpan Duration = TimeSpan.FromHours(12);

    /// <summary>
    /// Inicio (18:00 local) de la noche que contiene <paramref name="localTime"/>.
    /// Antes del mediodía se considera parte de la noche que empezó el día anterior,
    /// para que una hora como 02:00 caiga dentro de su propia noche.
    /// </summary>
    public static DateTime GetNightStartLocal(DateTime localTime) =>
        (localTime.Hour < 12 ? localTime.Date.AddDays(-1) : localTime.Date).AddHours(StartHour);

    /// <summary>
    /// Primer instante en que la altitud cruza <paramref name="altitudeDegrees"/>
    /// subiendo (<paramref name="rising"/> = true) o bajando, interpolando
    /// linealmente entre las dos muestras del cruce. Null si no hay cruce.
    /// </summary>
    public static DateTime? FindCrossing(
        IReadOnlyList<(DateTime Time, double AltitudeDegrees)> samples,
        double altitudeDegrees,
        bool rising)
    {
        ArgumentNullException.ThrowIfNull(samples);

        for (var index = 1; index < samples.Count; index++)
        {
            var previous = samples[index - 1];
            var current = samples[index];
            var previousDelta = previous.AltitudeDegrees - altitudeDegrees;
            var currentDelta = current.AltitudeDegrees - altitudeDegrees;

            if ((rising && previousDelta < 0 && currentDelta >= 0)
                || (!rising && previousDelta >= 0 && currentDelta < 0))
            {
                var fraction = previousDelta / (previousDelta - currentDelta);
                return previous.Time.AddMinutes((current.Time - previous.Time).TotalMinutes * fraction);
            }
        }

        return null;
    }
}
