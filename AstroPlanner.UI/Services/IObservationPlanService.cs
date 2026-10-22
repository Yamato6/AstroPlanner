namespace AstroPlanner.UI.Services;

/// <summary>Un objeto del plan de observación con la nota del usuario.</summary>
public sealed record ObservationPlanEntry(string Id, string Note = "");

/// <summary>
/// Persistencia del plan de observación ("Mi plan") entre sesiones.
/// Solo guarda ids del catálogo y notas; los objetos se reconstruyen al cargarlo.
/// </summary>
public interface IObservationPlanService
{
    /// <summary>Lee el plan guardado; si no hay plan o es inválido, devuelve una lista vacía.</summary>
    IReadOnlyList<ObservationPlanEntry> Load();

    /// <summary>Guarda el plan de forma atómica (escribe temporal y renombra).</summary>
    void Save(IEnumerable<ObservationPlanEntry> entries);
}
