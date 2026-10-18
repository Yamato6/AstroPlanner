using AstroPlanner.Core.Models;

namespace AstroPlanner.Core.Interfaces;

public interface ICelestialRepository
{
    Task<IReadOnlyList<CelestialObject>> GetAllAsync(CancellationToken cancellationToken = default);
}
