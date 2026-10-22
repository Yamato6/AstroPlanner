using System.Text.Json;
using System.Text.Json.Serialization;
using AstroPlanner.Core.Interfaces;
using AstroPlanner.Core.Models;

namespace AstroPlanner.Data.Repositories;

public sealed class JsonCelestialRepository : ICelestialRepository
{
    private const string FileName = "messier_objects.json";

    private readonly string? _filePath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <param name="filePath">Catálogo alternativo; null usa el incrustado en el ensamblado.</param>
    public JsonCelestialRepository(string? filePath = null)
    {
        _filePath = filePath;
    }

    public async Task<IReadOnlyList<CelestialObject>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var stream = SeedDataSource.Open(FileName, _filePath);
        var objects = await JsonSerializer.DeserializeAsync<List<CelestialObject>>(
            stream,
            _jsonOptions,
            cancellationToken);

        if (objects is null || objects.Count == 0)
        {
            throw new InvalidDataException(
                $"The celestial catalog '{SeedDataSource.Describe(FileName, _filePath)}' is empty.");
        }

        return objects;
    }
}
