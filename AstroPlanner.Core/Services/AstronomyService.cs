using AstroPlanner.Core.Algorithms;
using AstroPlanner.Core.Interfaces;
using AstroPlanner.Core.Models;

namespace AstroPlanner.Core.Services;

/// <summary>
/// Implementación por defecto de <see cref="IAstronomyService"/>.
/// Delega los cálculos a las clases estáticas de <c>Algorithms</c>.
/// Vive en Core porque no depende de UI y reutiliza lógica pura ya testeada.
/// </summary>
public sealed class AstronomyService : IAstronomyService
{
    public HorizontalCoordinates GetHorizontal(
        CelestialObject objeto,
        ObserverLocation observador,
        DateTime utcDateTime)
    {
        ArgumentNullException.ThrowIfNull(objeto);
        ArgumentNullException.ThrowIfNull(observador);

        return CoordinateTransformer.ToHorizontal(objeto.Coordinates, observador, utcDateTime);
    }

    public IEnumerable<(DateTime Time, double AltitudeDegrees)> GetAltitudeSeries(
        CelestialObject objeto,
        ObserverLocation observador,
        DateTime inicioUtc,
        DateTime finUtc,
        TimeSpan paso)
    {
        ArgumentNullException.ThrowIfNull(objeto);
        ArgumentNullException.ThrowIfNull(observador);

        if (paso <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(paso), "El paso temporal debe ser positivo.");
        }

        for (var t = inicioUtc; t <= finUtc; t += paso)
        {
            var horizontal = CoordinateTransformer.ToHorizontal(objeto.Coordinates, observador, t);
            yield return (t, horizontal.AltitudeDegrees);
        }
    }

    public IReadOnlyList<(DateTime LocalTime, double AltitudeDegrees)> GetNightAltitudeSeries(
        CelestialObject objeto,
        ObserverLocation observador,
        DateTime inicioNocheLocal,
        TimeZoneInfo zonaHoraria,
        TimeSpan paso)
    {
        ArgumentNullException.ThrowIfNull(objeto);
        ArgumentNullException.ThrowIfNull(observador);
        ArgumentNullException.ThrowIfNull(zonaHoraria);

        if (paso <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(paso), "El paso temporal debe ser positivo.");
        }

        var fin = inicioNocheLocal + ObservationNight.Duration;
        var points = new List<(DateTime LocalTime, double AltitudeDegrees)>();

        // El muestreo es en hora local para que el eje del gráfico sea regular;
        // cada punto se convierte a UTC por separado (respeta el horario de verano).
        for (var local = inicioNocheLocal; local <= fin; local += paso)
        {
            var utc = TimeZoneConversion.ToUtc(local, zonaHoraria);
            var horizontal = CoordinateTransformer.ToHorizontal(objeto.Coordinates, observador, utc);
            points.Add((local, horizontal.AltitudeDegrees));
        }

        return points;
    }

    public ObjectNightEvents GetNightEvents(
        CelestialObject objeto,
        ObserverLocation observador,
        DateTime inicioNocheLocal,
        TimeZoneInfo zonaHoraria,
        double altitudMinimaDegrees)
    {
        var samples = GetNightAltitudeSeries(
            objeto,
            observador,
            inicioNocheLocal,
            zonaHoraria,
            TimeSpan.FromMinutes(15));
        var maximum = samples.MaxBy(sample => sample.AltitudeDegrees);

        return new ObjectNightEvents(
            RiseLocal: ObservationNight.FindCrossing(samples, 0, rising: true),
            ThresholdCrossingLocal: ObservationNight.FindCrossing(samples, altitudMinimaDegrees, rising: true),
            CulminationLocal: maximum.LocalTime,
            SetLocal: ObservationNight.FindCrossing(samples, 0, rising: false),
            MaximumAltitudeDegrees: maximum.AltitudeDegrees);
    }
}
