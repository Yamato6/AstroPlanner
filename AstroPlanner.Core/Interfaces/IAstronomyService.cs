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

    /// <summary>
    /// Serie de altitud a lo largo de la noche que empieza en
    /// <paramref name="inicioNocheLocal"/> (hora local de <paramref name="zonaHoraria"/>),
    /// muestreada cada <paramref name="paso"/> en hora local.
    /// </summary>
    IReadOnlyList<(DateTime LocalTime, double AltitudeDegrees)> GetNightAltitudeSeries(
        CelestialObject objeto,
        ObserverLocation observador,
        DateTime inicioNocheLocal,
        TimeZoneInfo zonaHoraria,
        TimeSpan paso);

    /// <summary>
    /// Salida, cruce del umbral de altitud, culminación y puesta de
    /// <paramref name="objeto"/> durante la noche que empieza en
    /// <paramref name="inicioNocheLocal"/>.
    /// </summary>
    ObjectNightEvents GetNightEvents(
        CelestialObject objeto,
        ObserverLocation observador,
        DateTime inicioNocheLocal,
        TimeZoneInfo zonaHoraria,
        double altitudMinimaDegrees);
}
