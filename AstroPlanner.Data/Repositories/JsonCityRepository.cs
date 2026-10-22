using System.Text.Json;
using AstroPlanner.Core.Interfaces;
using AstroPlanner.Core.Models;

namespace AstroPlanner.Data.Repositories;

public sealed class JsonCityRepository : ICityRepository
{
    private const string FileName = "cities.json";

    private readonly string? _filePath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <param name="filePath">Catálogo alternativo; null usa el incrustado en el ensamblado.</param>
    public JsonCityRepository(string? filePath = null)
    {
        _filePath = filePath;
    }

    public async Task<IReadOnlyList<City>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var stream = SeedDataSource.Open(FileName, _filePath);
        var cities = await JsonSerializer.DeserializeAsync<List<City>>(
            stream,
            _jsonOptions,
            cancellationToken);

        if (cities is null || cities.Count == 0)
        {
            throw new InvalidDataException(
                $"The city catalog '{SeedDataSource.Describe(FileName, _filePath)}' is empty.");
        }

        return cities;
    }
}
