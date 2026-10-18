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
}
