namespace AstroPlanner.UI.Services;

/// <summary>
/// Registro de eventos y errores de la aplicación, para diagnosticar fallos
/// que el usuario no puede describir con detalle.
/// </summary>
public interface IAppLogger
{
    /// <summary>Dónde se guarda el registro, para indicárselo al usuario.</summary>
    string LogLocation { get; }

    void Info(string message);

    void Error(string message, Exception? exception = null);
}
