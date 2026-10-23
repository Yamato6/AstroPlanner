using System.IO;
using System.Text.Json;

namespace AstroPlanner.UI.Services;

/// <summary>
/// Implementación basada en JSON de <see cref="IObservationPlanService"/>.
/// Archivo: %LocalAppData%\AstroPlanner\plan.json
/// Formato actual: [{"id":"M31","note":"..."}]. También lee el formato de la
/// 0.3-0.5, una lista de ids (["M31","M42"]), para no perder planes existentes.
/// </summary>
public sealed class JsonObservationPlanService : IObservationPlanService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _filePath;

    public JsonObservationPlanService(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AstroPlanner",
            "plan.json");
    }

    public IReadOnlyList<ObservationPlanEntry> Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return [];
            }

            using var document = JsonDocument.Parse(File.ReadAllText(_filePath));
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var entries = new List<ObservationPlanEntry>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                var entry = element.ValueKind switch
                {
                    // Formato anterior: solo el id.
                    JsonValueKind.String => new ObservationPlanEntry(element.GetString()!),
                    JsonValueKind.Object => element.Deserialize<ObservationPlanEntry>(Options),
                    _ => null
                };

                if (!string.IsNullOrWhiteSpace(entry?.Id))
                {
                    entries.Add(entry with { Note = entry.Note ?? "" });
                }
            }

            return entries;
        }
        catch (JsonException)
        {
            // Archivo corrupto: se empieza con un plan vacío.
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    public void Save(IEnumerable<ObservationPlanEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        AtomicFile.WriteAllText(_filePath, JsonSerializer.Serialize(entries.ToList(), Options));
    }
}
