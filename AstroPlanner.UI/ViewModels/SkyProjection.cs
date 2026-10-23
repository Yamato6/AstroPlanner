namespace AstroPlanner.UI.ViewModels;

/// <summary>
/// Proyección polar del cielo para la carta: el cénit en el centro y el
/// horizonte en un círculo de radio 90 (la distancia al centro es 90° - altitud).
/// Norte arriba y este a la izquierda, como se ve el cielo al mirar hacia arriba
/// (igual que en un planisferio).
/// </summary>
public static class SkyProjection
{
    public const double HorizonRadius = 90;

    public static (double X, double Y) ToXY(double altitudeDegrees, double azimuthDegrees)
    {
        var radius = HorizonRadius - altitudeDegrees;
        var azimuth = azimuthDegrees * Math.PI / 180.0;
        return (-radius * Math.Sin(azimuth), radius * Math.Cos(azimuth));
    }
}

/// <summary>Un objeto dibujado en la carta del cielo.</summary>
public sealed record SkyMapPoint(
    string Id,
    double AltitudeDegrees,
    double AzimuthDegrees,
    double Magnitude,
    bool IsVisible,
    bool IsInPlan,
    bool IsSelected,
    bool IsPlanet = false)
{
    public (double X, double Y) Position => SkyProjection.ToXY(AltitudeDegrees, AzimuthDegrees);
}
