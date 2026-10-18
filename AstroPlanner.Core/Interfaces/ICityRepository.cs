using AstroPlanner.Core.Models;

namespace AstroPlanner.Core.Interfaces;

public interface ICityRepository
{
    Task<IReadOnlyList<City>> GetAllAsync(CancellationToken cancellationToken = default);
}
