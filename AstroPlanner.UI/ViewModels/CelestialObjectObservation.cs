using AstroPlanner.Core.Models;

namespace AstroPlanner.UI.ViewModels;

public sealed class CelestialObjectObservation
{
    public required CelestialObject Object { get; init; }
    public required HorizontalCoordinates Coordinates { get; init; }
    public bool IsVisible { get; init; }
    public string VisibilityStatus => Coordinates.AltitudeDegrees < 0
        ? "No visible"
        : IsVisible ? "Visible" : "Por debajo del umbral";
    public DateTime? RiseTimeLocal { get; init; }
    public DateTime? VisibilityStartTimeLocal { get; init; }
    public DateTime? BestObservationTimeLocal { get; init; }
    public DateTime? SetTimeLocal { get; init; }
    public double MaximumAltitudeDegrees { get; init; }

    /// <summary>Magnitud aparente en el instante elegido (en los planetas cambia con la fecha).</summary>
    public double Magnitude { get; init; }

    /// <summary>Distancia, fase y tamaño aparente si el objeto es un planeta; null si no.</summary>
    public PlanetPosition? Planet { get; init; }

    public bool IsPlanet => Planet is not null;

    /// <summary>Nubosidad pronosticada (%) a la hora de culminación; null sin pronóstico.</summary>
    public int? CloudCoverAtCulmination { get; init; }

    /// <summary>Distancia angular a la Luna en el instante elegido (grados).</summary>
    public double? MoonSeparationDegrees { get; init; }

    /// <summary>Aviso cuando la Luna dificulta la observación; null si no molesta.</summary>
    public string? MoonWarning { get; init; }

    public bool HasMoonWarning => MoonWarning is not null;
}
