using System.Text.Json;
using AstroPlanner.Core.Interfaces;
using AstroPlanner.Core.Models;

namespace AstroPlanner.Data.Repositories;

public sealed class JsonCityRepository : ICityRepository
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public JsonCityRepository(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(AppContext.BaseDirectory, "SeedData", "cities.json");
    }

    public async Task<IReadOnlyList<City>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(_filePath);
        var cities = await JsonSerializer.DeserializeAsync<List<City>>(
            stream,
            _jsonOptions,
            cancellationToken);

        if (cities is null || cities.Count == 0)
        {
            throw new InvalidDataException($"The city catalog '{_filePath}' is empty.");
        }

        return cities;
    }
}
