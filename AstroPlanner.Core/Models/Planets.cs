namespace AstroPlanner.Core.Models;

public enum Planet
{
    Mercury,
    Venus,
    Mars,
    Jupiter,
    Saturn,
    Uranus,
    Neptune
}

/// <summary>
/// Un planeta visto desde la Tierra en un instante.
/// </summary>
/// <param name="Equatorial">Coordenadas aparentes del equinoccio de la fecha.</param>
/// <param name="DistanceAu">Distancia a la Tierra (Δ), en unidades astronómicas.</param>
/// <param name="SunDistanceAu">Distancia al Sol (r), en unidades astronómicas.</param>
/// <param name="ElongationDegrees">Separación angular respecto al Sol vista desde la Tierra.</param>
/// <param name="PhaseAngleDegrees">Ángulo Sol-planeta-Tierra.</param>
/// <param name="IlluminatedFraction">Fracción iluminada del disco (0-1).</param>
/// <param name="Magnitude">Magnitud visual aparente.</param>
/// <param name="ApparentDiameterArcsec">Diámetro ecuatorial aparente, en segundos de arco.</param>
public sealed record PlanetPosition(
    Planet Planet,
    EquatorialCoordinates Equatorial,
    double DistanceAu,
    double SunDistanceAu,
    double ElongationDegrees,
    double PhaseAngleDegrees,
    double IlluminatedFraction,
    double Magnitude,
    double ApparentDiameterArcsec);

/// <summary>Los planetas que se añaden al catálogo, con su nombre en español.</summary>
public static class SolarSystemCatalog
{
    public static IReadOnlyList<CelestialObject> Planets { get; } =
    [
        CelestialObject.CreatePlanet("Mercurio", "Mercurio", Planet.Mercury),
        CelestialObject.CreatePlanet("Venus", "Venus", Planet.Venus),
        CelestialObject.CreatePlanet("Marte", "Marte", Planet.Mars),
        CelestialObject.CreatePlanet("Júpiter", "Júpiter", Planet.Jupiter),
        CelestialObject.CreatePlanet("Saturno", "Saturno", Planet.Saturn),
        CelestialObject.CreatePlanet("Urano", "Urano", Planet.Uranus),
        CelestialObject.CreatePlanet("Neptuno", "Neptuno", Planet.Neptune)
    ];
}
