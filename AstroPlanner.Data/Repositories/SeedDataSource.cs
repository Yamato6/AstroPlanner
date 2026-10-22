namespace AstroPlanner.Data.Repositories;

/// <summary>
/// Origen de los catálogos JSON. Por defecto se leen de los recursos
/// incrustados en el ensamblado, de modo que el .exe publicado funciona sin
/// la carpeta SeedData; una ruta explícita (pruebas, catálogos propios) tiene
/// prioridad.
/// </summary>
internal static class SeedDataSource
{
    /// <summary>Abre <paramref name="filePath"/> o, si es null, el recurso incrustado <paramref name="fileName"/>.</summary>
    public static Stream Open(string fileName, string? filePath)
    {
        if (filePath is not null)
        {
            return File.OpenRead(filePath);
        }

        var resourceName = $"SeedData/{fileName}";
        return typeof(SeedDataSource).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Embedded catalog '{resourceName}' was not found.", resourceName);
    }

    /// <summary>Descripción legible del origen, para mensajes de error.</summary>
    public static string Describe(string fileName, string? filePath) =>
        filePath ?? $"SeedData/{fileName} (incrustado)";
}
