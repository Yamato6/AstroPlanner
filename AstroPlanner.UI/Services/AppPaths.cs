using System.IO;

namespace AstroPlanner.UI.Services;

/// <summary>
/// Carpeta de datos del usuario: %LocalAppData%\AstroPlanner (configuración,
/// plan, bitácora y registros). El instalador nunca la borra.
/// </summary>
public static class AppPaths
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AstroPlanner");

    public static string DataFile(string fileName) => Path.Combine(DataDirectory, fileName);
}
