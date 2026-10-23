using System.IO;
using System.Text.Json;

namespace AstroPlanner.UI.Services;

/// <summary>
/// Una observación registrada en la bitácora. Seeing, transparencia y
/// valoración van de 1 (malo) a 5 (excelente).
/// </summary>
public sealed record ObservationLogEntry(
    Guid Id,
    string ObjectId,
    string ObjectName,
    DateTime ObservedAtLocal,
    string Location,
    string Equipment,
    int Seeing,
    int Transparency,
    int Rating,
    string Notes)
{
    public string RatingStars => new string('★', Math.Clamp(Rating, 0, 5)) + new string('☆', 5 - Math.Clamp(Rating, 0, 5));
}

/// <summary>Persistencia de la bitácora de observaciones.</summary>
public interface IObservationLogService
{
    /// <summary>Lee la bitácora; si no existe o es inválida, devuelve una lista vacía.</summary>
    IReadOnlyList<ObservationLogEntry> Load();

    /// <summary>Guarda la bitácora de forma atómica.</summary>
    void Save(IEnumerable<ObservationLogEntry> entries);
}

/// <summary>
/// Implementación JSON de <see cref="IObservationLogService"/>.
/// Archivo: %LocalAppData%\AstroPlanner\logbook.json
/// </summary>
public sealed class JsonObservationLogService : IObservationLogService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _filePath;

    public JsonObservationLogService(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AstroPlanner",
            "logbook.json");
    }

    public IReadOnlyList<ObservationLogEntry> Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return [];
            }

            var entries = JsonSerializer.Deserialize<List<ObservationLogEntry>>(File.ReadAllText(_filePath), Options);
            return entries?.Where(entry => !string.IsNullOrWhiteSpace(entry.ObjectId)).ToList() ?? [];
        }
        catch (JsonException)
        {
            // Archivo corrupto: se aparta (no se sobrescribe al guardar la
            // siguiente observación) y se empieza con una bitácora vacía.
            PreserveCorruptFile();
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    public void Save(IEnumerable<ObservationLogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        AtomicFile.WriteAllText(_filePath, JsonSerializer.Serialize(entries.ToList(), Options));
    }

    /// <summary>Ruta con la que se apartó el último archivo corrupto, si lo hubo.</summary>
    public string? PreservedCorruptFilePath { get; private set; }

    private void PreserveCorruptFile()
    {
        try
        {
            var preserved = Path.Combine(
                Path.GetDirectoryName(_filePath) ?? "",
                $"{Path.GetFileNameWithoutExtension(_filePath)}.corrupto-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            File.Move(_filePath, preserved);
            PreservedCorruptFilePath = preserved;
        }
        catch (IOException)
        {
            // Si no se puede apartar, se deja como está.
        }
    }
}
