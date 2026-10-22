namespace AstroPlanner.UI.Services;

/// <summary>
/// Persistencia del plan de observación ("Mi plan") entre sesiones.
/// Solo guarda los ids del catálogo; los objetos se reconstruyen al cargarlo.
/// </summary>
public interface IObservationPlanService
{
    /// <summary>Lee los ids guardados; si no hay plan o es inválido, devuelve una lista vacía.</summary>
    IReadOnlyList<string> Load();

    /// <summary>Guarda los ids de forma atómica (escribe temporal y renombra).</summary>
    void Save(IEnumerable<string> objectIds);
}
