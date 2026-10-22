using System.IO;
using System.Text.Json;

namespace AstroPlanner.UI.Services;

/// <summary>
/// Implementación basada en JSON de <see cref="IObservationPlanService"/>.
/// Archivo: %LocalAppData%\AstroPlanner\plan.json
/// </summary>
public sealed class JsonObservationPlanService : IObservationPlanService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public JsonObservationPlanService(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AstroPlanner",
            "plan.json");
    }

    public IReadOnlyList<string> Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return [];
            }

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<string>>(json, Options) ?? [];
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

    public void Save(IEnumerable<string> objectIds)
    {
        ArgumentNullException.ThrowIfNull(objectIds);

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(objectIds.ToList(), Options);
        var tempFile = _filePath + ".tmp";
        File.WriteAllText(tempFile, json);

        // Escritura atómica: evita dejar el archivo corrupto si la app se cierra
        // a mitad de la operación.
        File.Move(tempFile, _filePath, overwrite: true);
    }
}
