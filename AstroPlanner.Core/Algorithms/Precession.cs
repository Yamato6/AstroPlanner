using AstroPlanner.Core.Models;

namespace AstroPlanner.Core.Algorithms;

/// <summary>
/// Precesión de coordenadas ecuatoriales desde J2000.0 hasta la fecha de
/// observación (Meeus, "Astronomical Algorithms", cap. 21, método riguroso).
/// Sin la corrección, las coordenadas del catálogo se desvían ~0.36° en 2026.
/// No incluye nutación ni aberración (&lt; 30"), irrelevantes para planificar.
/// </summary>
public static class Precession
{
    public static EquatorialCoordinates FromJ2000(EquatorialCoordinates j2000, DateTime utcDateTime)
    {
        ArgumentNullException.ThrowIfNull(j2000);

        var t = (AstronomyMath.ToJulianDate(utcDateTime) - 2451545.0) / 36525.0;
        var t2 = t * t;
        var t3 = t2 * t;

        // Ángulos de precesión en segundos de arco → grados (Meeus 21.3).
        var zeta = (2306.2181 * t + 0.30188 * t2 + 0.017998 * t3) / 3600.0;
        var z = (2306.2181 * t + 1.09468 * t2 + 0.018203 * t3) / 3600.0;
        var theta = (2004.3109 * t - 0.42665 * t2 - 0.041833 * t3) / 3600.0;

        var alpha0 = AstronomyMath.DegreesToRadiansValue(j2000.RightAscensionHours * 15.0 + zeta);
        var delta0 = AstronomyMath.DegreesToRadiansValue(j2000.DeclinationDegrees);
        var thetaRad = AstronomyMath.DegreesToRadiansValue(theta);

        var a = Math.Cos(delta0) * Math.Sin(alpha0);
        var b = Math.Cos(thetaRad) * Math.Cos(delta0) * Math.Cos(alpha0) - Math.Sin(thetaRad) * Math.Sin(delta0);
        var c = Math.Sin(thetaRad) * Math.Cos(delta0) * Math.Cos(alpha0) + Math.Cos(thetaRad) * Math.Sin(delta0);

        var alpha = AstronomyMath.RadiansToDegreesValue(Math.Atan2(a, b)) + z;
        var delta = AstronomyMath.RadiansToDegreesValue(Math.Asin(Math.Clamp(c, -1.0, 1.0)));

        return new EquatorialCoordinates(AstronomyMath.NormalizeDegrees(alpha) / 15.0, delta);
    }
}
