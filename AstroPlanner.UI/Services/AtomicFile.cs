using System.IO;

namespace AstroPlanner.UI.Services;

/// <summary>
/// Escritura atómica de archivos de datos del usuario: se escribe un temporal
/// y se renombra, para no dejar el archivo corrupto si la app se cierra a
/// mitad de la operación.
/// </summary>
internal static class AtomicFile
{
    public static void WriteAllText(string filePath, string contents)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempFile = filePath + ".tmp";
        File.WriteAllText(tempFile, contents);
        File.Move(tempFile, filePath, overwrite: true);
    }
}
