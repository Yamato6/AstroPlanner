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
}
