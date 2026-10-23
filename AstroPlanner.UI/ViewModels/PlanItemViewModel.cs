using CommunityToolkit.Mvvm.ComponentModel;

namespace AstroPlanner.UI.ViewModels;

/// <summary>
/// Fila de "Mi plan": el objeto del catálogo (con su posición actual), la nota
/// del usuario y la hora de culminación de la noche, que ordena el plan.
/// </summary>
public sealed partial class PlanItemViewModel : ObservableObject
{
    private readonly Action<PlanItemViewModel> _onNoteChanged;

    public PlanItemViewModel(CelestialObjectRowViewModel row, string note, Action<PlanItemViewModel> onNoteChanged)
    {
        Row = row;
        this.note = note;
        _onNoteChanged = onNoteChanged;
    }

    /// <summary>Fila del catálogo; comparte instancia con la tabla del planificador.</summary>
    public CelestialObjectRowViewModel Row { get; }

    public string Id => Row.Id;

    [ObservableProperty] private string note;

    /// <summary>Mejor momento: máxima altitud dentro de la noche (hora local).</summary>
    [ObservableProperty] private DateTime? culminationLocal;

    [ObservableProperty] private double maximumAltitudeDegrees;

    /// <summary>Nubosidad pronosticada (%) a la hora de culminación; null sin pronóstico.</summary>
    [ObservableProperty] private int? cloudCoverAtCulmination;

    partial void OnNoteChanged(string value) => _onNoteChanged(this);
}
