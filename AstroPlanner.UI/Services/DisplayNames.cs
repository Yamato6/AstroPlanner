using AstroPlanner.Core.Models;

namespace AstroPlanner.UI.Services;

/// <summary>Opción de un filtro: <see cref="Value"/> es lo que se filtra y <see cref="Label"/>, lo que se muestra.</summary>
public sealed record FilterOption(string Value, string Label);

/// <summary>Nombres en español de los valores internos que se muestran al usuario.</summary>
public static class DisplayNames
{
    /// <summary>Valor del filtro de tipo que no filtra nada.</summary>
    public const string AllTypes = "All";

    public static string Of(CelestialObjectType type) => type switch
    {
        CelestialObjectType.Galaxy => "Galaxia",
        CelestialObjectType.OpenCluster => "Cúmulo abierto",
        CelestialObjectType.GlobularCluster => "Cúmulo globular",
        CelestialObjectType.PlanetaryNebula => "Nebulosa planetaria",
        CelestialObjectType.DiffuseNebula => "Nebulosa difusa",
        CelestialObjectType.SupernovaRemnant => "Resto de supernova",
        CelestialObjectType.Planet => "Planeta",
        _ => "Otro"
    };
}
