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
    Unknown
}

public sealed class CelestialObject
{
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

    public string Id { get; }
    public string CommonName { get; }
    public string Constellation { get; }
    public CelestialObjectType Type { get; }
    public double ApparentMagnitude { get; }
    public EquatorialCoordinates Coordinates { get; }

    private static string RequireText(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value cannot be empty.", parameterName)
            : value.Trim();
}
