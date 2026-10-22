using System.IO;

namespace AstroPlanner.UI.Services;

/// <summary>
/// Implementación de <see cref="IAppLogger"/> que escribe un archivo por día:
/// %LocalAppData%\AstroPlanner\logs\astroplanner-AAAAMMDD.log
/// </summary>
public sealed class FileAppLogger : IAppLogger
{
    private readonly string _directory;
    private readonly object _sync = new();

    public FileAppLogger(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AstroPlanner",
            "logs");
    }

    public string LogLocation => _directory;

    /// <summary>Archivo del día actual.</summary>
    public string CurrentFilePath => Path.Combine(_directory, $"astroplanner-{DateTime.Now:yyyyMMdd}.log");

    public void Info(string message) => Write("INFO", message, null);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
        if (exception is not null)
        {
            line += Environment.NewLine + exception;
        }

        try
        {
            lock (_sync)
            {
                Directory.CreateDirectory(_directory);
                File.AppendAllText(CurrentFilePath, line + Environment.NewLine);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Registrar nunca debe tumbar la aplicación.
        }
    }
}
