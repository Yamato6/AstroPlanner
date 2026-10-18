using System.IO;
using System.Text.Json;

namespace AstroPlanner.UI.Services;

/// <summary>
/// Implementación basada en JSON de <see cref="ISettingsService"/>.
/// Archivo: %LocalAppData%\AstroPlanner\settings.json
/// </summary>
public sealed class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public JsonSettingsService(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AstroPlanner",
            "settings.json");
    }

    public ObserverSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new ObserverSettings();
            }

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<ObserverSettings>(json, Options) ?? new ObserverSettings();
        }
        catch (JsonException)
        {
            // Archivo corrupto: se ignora y se usan valores por defecto.
            return new ObserverSettings();
        }
        catch (IOException)
        {
            return new ObserverSettings();
        }
    }

    public void Save(ObserverSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, Options);
        var tempFile = _filePath + ".tmp";
        File.WriteAllText(tempFile, json);

        // Escritura atómica: evita dejar el archivo corrupto si la app se cierra
        // a mitad de la operación.
        File.Move(tempFile, _filePath, overwrite: true);
    }
}
