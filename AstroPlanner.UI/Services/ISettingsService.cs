namespace AstroPlanner.UI.Services;

/// <summary>
/// Persistencia de la configuración del observador.
/// Interfaz en UI/Services porque es una preocupación de presentación
/// (dónde y cómo guardar), no de dominio.
/// </summary>
public interface ISettingsService
{
    /// <summary>Lee la configuración guardada; si no existe, devuelve valores por defecto.</summary>
    ObserverSettings Load();

    /// <summary>Guarda la configuración de forma atómica (escribe temporal y renombra).</summary>
    void Save(ObserverSettings settings);
}
