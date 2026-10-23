using AstroPlanner.Core.Models;

namespace AstroPlanner.Core.Interfaces;

/// <summary>
/// Servicio astronómico de alto nivel para la capa de presentación.
/// Centraliza el cálculo de posiciones (objetos, Sol, Luna), de la noche de
/// observación y de series temporales de altitud, evitando que los ViewModels
/// dependan directamente de las clases estáticas de <c>Algorithms</c>.
/// </summary>
public interface IAstronomyService
{
    /// <summary>
    /// Posición aparente (altitud/azimut) de <paramref name="objeto"/> vista desde
    /// <paramref name="observador"/> en el instante UTC indicado: aplica la
    /// precesión J2000 → fecha y la refracción atmosférica.
    /// </summary>
    HorizontalCoordinates GetHorizontal(
        CelestialObject objeto,
        ObserverLocation observador,
        DateTime utcDateTime);

    /// <summary>
    /// Genera una serie temporal de altitud (en grados) para
    /// <paramref name="objeto"/> entre <paramref name="inicioUtc"/> y
    /// <paramref name="finUtc"/> cada <paramref name="paso"/>.
    /// </summary>
    IEnumerable<(DateTime Time, double AltitudeDegrees)> GetAltitudeSeries(
        CelestialObject objeto,
        ObserverLocation observador,
        DateTime inicioUtc,
        DateTime finUtc,
        TimeSpan paso);

    /// <summary>
    /// Serie de altitud entre <paramref name="inicioLocal"/> y <paramref name="finLocal"/>
    /// (hora local de <paramref name="zonaHoraria"/>, ambos incluidos), muestreada
    /// cada <paramref name="paso"/> en hora local. Se usa para la curva nocturna.
    /// </summary>
    IReadOnlyList<(DateTime LocalTime, double AltitudeDegrees)> GetNightAltitudeSeries(
        CelestialObject objeto,
        ObserverLocation observador,
        DateTime inicioLocal,
        DateTime finLocal,
        TimeZoneInfo zonaHoraria,
        TimeSpan paso);

    /// <summary>
    /// Salida, cruce del umbral de altitud, culminación y puesta de
    /// <paramref name="objeto"/> entre <paramref name="inicioLocal"/> y
    /// <paramref name="finLocal"/> (normalmente, de la puesta a la salida del Sol).
    /// </summary>
    ObjectNightEvents GetNightEvents(
        CelestialObject objeto,
        ObserverLocation observador,
        DateTime inicioLocal,
        DateTime finLocal,
        TimeZoneInfo zonaHoraria,
        double altitudMinimaDegrees);

    /// <summary>
    /// Posición, distancia, fase, magnitud y tamaño aparente de un planeta;
    /// null si <paramref name="objeto"/> no es un planeta.
    /// </summary>
    PlanetPosition? GetPlanetPosition(CelestialObject objeto, DateTime utcDateTime);

    /// <summary>Magnitud aparente: la calculada para un planeta, la del catálogo para el resto.</summary>
    double GetApparentMagnitude(CelestialObject objeto, DateTime utcDateTime);

    /// <summary>Altitud geométrica del centro del Sol (grados).</summary>
    double GetSunAltitudeDegrees(ObserverLocation observador, DateTime utcDateTime);

    /// <summary>Día, crepúsculo o noche oscura según la altura del Sol.</summary>
    SkyCondition GetSkyCondition(ObserverLocation observador, DateTime utcDateTime);

    /// <summary>
    /// Puesta y salida del Sol y crepúsculo astronómico de la noche que contiene
    /// <paramref name="horaLocal"/> (antes del mediodía cuenta la noche anterior).
    /// </summary>
    NightInfo GetNightInfo(ObserverLocation observador, DateTime horaLocal, TimeZoneInfo zonaHoraria);

    /// <summary>Posición aparente y fase de la Luna para el observador.</summary>
    MoonState GetMoon(ObserverLocation observador, DateTime utcDateTime);

    /// <summary>Distancia angular (grados) entre <paramref name="objeto"/> y la Luna.</summary>
    double GetMoonSeparationDegrees(CelestialObject objeto, MoonState luna, DateTime utcDateTime);

    /// <summary>Serie de altitud de la Luna, como <see cref="GetNightAltitudeSeries"/>.</summary>
    IReadOnlyList<(DateTime LocalTime, double AltitudeDegrees)> GetMoonAltitudeSeries(
        ObserverLocation observador,
        DateTime inicioLocal,
        DateTime finLocal,
        TimeZoneInfo zonaHoraria,
        TimeSpan paso);
}
