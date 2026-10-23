namespace AstroPlanner.Core.Models;

public enum CelestialObjectType
{
    Galaxy,
    OpenCluster,
    GlobularCluster,
    PlanetaryNebula,
    DiffuseNebula,
    SupernovaRemnant,
    /// <summary>
    /// Para objetos del catálogo Messier que no encajan en las categorías
    /// estándar (ej. M40, un sistema estelar doble; M73, un agrupamiento
    /// de estrellas sin confirmación física de cúmulo).
    /// </summary>
    Unknown,
    /// <summary>Planeta del sistema solar: su posición y magnitud cambian con la fecha.</summary>
    Planet
}

public sealed class CelestialObject
{
    /// <summary>Constelación que se muestra para los planetas (cambia con la fecha).</summary>
    public const string SolarSystemConstellation = "Sistema solar";

    public CelestialObject(
        string id,
        string commonName,
        string constellation,
        CelestialObjectType type,
        double apparentMagnitude,
        EquatorialCoordinates coordinates)
    {
        Id = RequireText(id, nameof(id));
        CommonName = RequireText(commonName, nameof(commonName));
        Constellation = RequireText(constellation, nameof(constellation));
        Type = type;
        ApparentMagnitude = apparentMagnitude;
        Coordinates = coordinates ?? throw new ArgumentNullException(nameof(coordinates));
    }

    private CelestialObject(string id, string commonName, Planet planet)
        : this(id, commonName, SolarSystemConstellation, CelestialObjectType.Planet, double.NaN,
            new EquatorialCoordinates(0, 0))
    {
        Planet = planet;
    }

    /// <summary>
    /// Crea un planeta. Sus <see cref="Coordinates"/> y <see cref="ApparentMagnitude"/>
    /// no se usan: dependen de la fecha y las calcula <c>IAstronomyService</c>.
    /// </summary>
    public static CelestialObject CreatePlanet(string id, string commonName, Planet planet) =>
        new(id, commonName, planet);

    public string Id { get; }
    public string CommonName { get; }
    public string Constellation { get; }
    public CelestialObjectType Type { get; }

    /// <summary>Magnitud del catálogo (NaN en los planetas, cuya magnitud depende de la fecha).</summary>
    public double ApparentMagnitude { get; }

    /// <summary>Coordenadas J2000 del catálogo (sin uso en los planetas).</summary>
    public EquatorialCoordinates Coordinates { get; }

    /// <summary>El planeta, o null si es un objeto de cielo profundo con coordenadas fijas.</summary>
    public Planet? Planet { get; }

    public bool IsPlanet => Planet is not null;

    private static string RequireText(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value cannot be empty.", parameterName)
            : value.Trim();
}
