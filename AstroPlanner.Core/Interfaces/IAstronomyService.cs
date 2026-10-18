using AstroPlanner.Core.Models;

namespace AstroPlanner.Core.Interfaces;

/// <summary>
/// Servicio astronómico de alto nivel para la capa de presentación.
/// Centraliza el cálculo de posición horizontal y de series temporales de
/// altitud, evitando que los ViewModels dependan directamente de las clases
/// estáticas de <c>Algorithms</c>.
/// </summary>
public interface IAstronomyService
{
    /// <summary>
    /// Convierte las coordenadas ecuatoriales de <paramref name="objeto"/>
    /// en coordenadas horizontales (altitud/azimut) vistas desde
    /// <paramref name="observador"/> en el instante UTC indicado.
    /// </summary>
    HorizontalCoordinates GetHorizontal(
        CelestialObject objeto,
        ObserverLocation observador,
        DateTime utcDateTime);

    /// <summary>
    /// Genera una serie temporal de altitud (en grados) para
    /// <paramref name="objeto"/> entre <paramref name="inicioUtc"/> y
    /// <paramref name="finUtc"/> cada <paramref name="paso"/>.
    /// Se usa para dibujar la curva de altitud vs. tiempo (tránsito nocturno).
    /// </summary>
    IEnumerable<(DateTime Time, double AltitudeDegrees)> GetAltitudeSeries(
        CelestialObject objeto,
        ObserverLocation observador,
        DateTime inicioUtc,
        DateTime finUtc,
        TimeSpan paso);
}
