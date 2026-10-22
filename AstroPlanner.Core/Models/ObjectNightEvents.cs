namespace AstroPlanner.Core.Models;

/// <summary>
/// Eventos de un objeto durante una noche de observación, en hora local.
/// Cada hora es null si el evento no ocurre dentro de la ventana nocturna
/// (p. ej. un objeto circumpolar no sale ni se pone).
/// </summary>
public sealed record ObjectNightEvents(
    DateTime? RiseLocal,
    DateTime? ThresholdCrossingLocal,
    DateTime CulminationLocal,
    DateTime? SetLocal,
    double MaximumAltitudeDegrees);
