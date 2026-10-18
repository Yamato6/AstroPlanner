using System.Text.Json;
using System.Text.Json.Serialization;
using AstroPlanner.Core.Interfaces;
using AstroPlanner.Core.Models;

namespace AstroPlanner.Data.Repositories;

public sealed class JsonCelestialRepository : ICelestialRepository
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonCelestialRepository(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(AppContext.BaseDirectory, "SeedData", "messier_objects.json");
    }

    public async Task<IReadOnlyList<CelestialObject>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(_filePath);
        var objects = await JsonSerializer.DeserializeAsync<List<CelestialObject>>(
            stream,
            _jsonOptions,
            cancellationToken);

        if (objects is null || objects.Count == 0)
        {
            throw new InvalidDataException($"The celestial catalog '{_filePath}' is empty.");
        }

        return objects;
    }
}
