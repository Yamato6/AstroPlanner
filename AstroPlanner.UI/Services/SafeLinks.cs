using System.Diagnostics;
using System.IO;

namespace AstroPlanner.UI.Services;

/// <summary>
/// Apertura de enlaces y carpetas desde la interfaz. Solo se abren direcciones
/// https de los sitios que la app enlaza: un enlace manipulado (por ejemplo, en
/// la respuesta de GitHub) no puede lanzar un programa ni abrir otra web.
/// </summary>
public static class SafeLinks
{
    private static readonly string[] AllowedHosts = ["github.com", "open-meteo.com"];

    public static bool IsAllowed(Uri? uri) =>
        uri is { IsAbsoluteUri: true, Scheme: "https" }
        && AllowedHosts.Any(host => uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase)
                                    || uri.Host.EndsWith("." + host, StringComparison.OrdinalIgnoreCase));

    /// <summary>Abre el enlace en el navegador si está permitido; devuelve si se abrió.</summary>
    public static bool Open(Uri? uri)
    {
        if (!IsAllowed(uri))
        {
            return false;
        }

        Process.Start(new ProcessStartInfo(uri!.AbsoluteUri) { UseShellExecute = true });
        return true;
    }

    /// <summary>Abre una carpeta local en el Explorador (la crea si aún no existe).</summary>
    public static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }
}
