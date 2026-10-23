using AstroPlanner.Core.Interfaces;
using AstroPlanner.Core.Models;

namespace AstroPlanner.Data.Repositories;

/// <summary>
/// Añade los planetas (<see cref="SolarSystemCatalog"/>) al catálogo de
/// cielo profundo de <paramref name="inner"/>.
/// </summary>
public sealed class CatalogWithPlanetsRepository(ICelestialRepository inner) : ICelestialRepository
{
    public async Task<IReadOnlyList<CelestialObject>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var deepSky = await inner.GetAllAsync(cancellationToken);
        return [.. deepSky, .. SolarSystemCatalog.Planets];
    }
}
