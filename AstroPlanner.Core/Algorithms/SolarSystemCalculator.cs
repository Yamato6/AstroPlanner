using AstroPlanner.Core.Models;

namespace AstroPlanner.Core.Algorithms;

/// <summary>
/// Posiciones del Sol y la Luna con fórmulas de baja precisión, suficientes
/// para planificar: Sol ~0.01° (Meeus, cap. 25), Luna ~0.3° (Astronomical
/// Almanac, "low precision formulae"). Se usa UT en lugar de TT (ΔT ≈ 70 s),
/// lo que añade un error despreciable a esta escala.
/// </summary>
public static class SolarSystemCalculator
{
    private const double AstronomicalUnitKm = 149_597_870.7;
    private const double EarthRadiusKm = 6378.14;

    private static double Sin(double degrees) => Math.Sin(AstronomyMath.DegreesToRadiansValue(degrees));
    private static double Cos(double degrees) => Math.Cos(AstronomyMath.DegreesToRadiansValue(degrees));

    private static double CenturiesSinceJ2000(DateTime utcDateTime) =>
        (AstronomyMath.ToJulianDate(utcDateTime) - 2451545.0) / 36525.0;

    /// <summary>Oblicuidad media de la eclíptica (grados).</summary>
    private static double MeanObliquity(double t) => 23.4392911 - 0.0130042 * t;

    public static SunPosition GetSunPosition(DateTime utcDateTime)
    {
        var t = CenturiesSinceJ2000(utcDateTime);

        var meanLongitude = 280.46646 + 36000.76983 * t + 0.0003032 * t * t;
        var meanAnomaly = 357.52911 + 35999.05029 * t - 0.0001537 * t * t;
        var eccentricity = 0.016708634 - 0.000042037 * t;

        var center = (1.914602 - 0.004817 * t - 0.000014 * t * t) * Sin(meanAnomaly)
            + (0.019993 - 0.000101 * t) * Sin(2 * meanAnomaly)
            + 0.000289 * Sin(3 * meanAnomaly);

        var trueLongitude = meanLongitude + center;
        var trueAnomaly = meanAnomaly + center;
        var distanceAu = 1.000001018 * (1 - eccentricity * eccentricity) / (1 + eccentricity * Cos(trueAnomaly));

        // Longitud aparente: corrige nutación y aberración (Meeus 25.8).
        var omega = 125.04 - 1934.136 * t;
        var apparentLongitude = AstronomyMath.NormalizeDegrees(trueLongitude - 0.00569 - 0.00478 * Sin(omega));
        var obliquity = MeanObliquity(t) + 0.00256 * Cos(omega);

        var equatorial = EclipticToEquatorial(apparentLongitude, 0, obliquity);
        return new SunPosition(apparentLongitude, equatorial, distanceAu * AstronomicalUnitKm);
    }

    public static MoonPosition GetMoonPosition(DateTime utcDateTime)
    {
        var t = CenturiesSinceJ2000(utcDateTime);

        var longitude = 218.32 + 481267.881 * t
            + 6.29 * Sin(135.0 + 477198.87 * t)
            - 1.27 * Sin(259.3 - 413335.36 * t)
            + 0.66 * Sin(235.7 + 890534.22 * t)
            + 0.21 * Sin(269.9 + 954397.74 * t)
            - 0.19 * Sin(357.5 + 35999.05 * t)
            - 0.11 * Sin(186.5 + 966404.03 * t);

        var latitude = 5.13 * Sin(93.3 + 483202.02 * t)
            + 0.28 * Sin(228.2 + 960400.89 * t)
            - 0.28 * Sin(318.3 + 6003.15 * t)
            - 0.17 * Sin(217.6 - 407332.21 * t);

        var parallax = 0.9508
            + 0.0518 * Cos(135.0 + 477198.87 * t)
            + 0.0095 * Cos(259.3 - 413335.36 * t)
            + 0.0078 * Cos(235.7 + 890534.22 * t)
            + 0.0028 * Cos(269.9 + 954397.74 * t);

        longitude = AstronomyMath.NormalizeDegrees(longitude);
        var equatorial = EclipticToEquatorial(longitude, latitude, MeanObliquity(t));
        var distanceKm = EarthRadiusKm / Sin(parallax);

        return new MoonPosition(longitude, latitude, equatorial, distanceKm, parallax);
    }

    /// <summary>
    /// Fracción iluminada del disco lunar (0 = nueva, 1 = llena) a partir del
    /// ángulo de fase (Meeus, cap. 48).
    /// </summary>
    public static double GetMoonIlluminatedFraction(MoonPosition moon, SunPosition sun)
    {
        ArgumentNullException.ThrowIfNull(moon);
        ArgumentNullException.ThrowIfNull(sun);

        // Elongación geocéntrica Luna-Sol.
        var cosElongation = Cos(moon.EclipticLatitudeDegrees)
            * Cos(moon.EclipticLongitudeDegrees - sun.EclipticLongitudeDegrees);
        var elongation = Math.Acos(Math.Clamp(cosElongation, -1.0, 1.0));

        var phaseAngle = Math.Atan2(
            sun.DistanceKm * Math.Sin(elongation),
            moon.DistanceKm - sun.DistanceKm * Math.Cos(elongation));

        return (1 + Math.Cos(phaseAngle)) / 2;
    }

    /// <summary>True si la Luna está creciendo (entre Luna nueva y llena).</summary>
    public static bool IsMoonWaxing(MoonPosition moon, SunPosition sun) =>
        AstronomyMath.NormalizeDegrees(moon.EclipticLongitudeDegrees - sun.EclipticLongitudeDegrees) < 180;

    private static EquatorialCoordinates EclipticToEquatorial(
        double longitudeDegrees,
        double latitudeDegrees,
        double obliquityDegrees)
    {
        var lambda = AstronomyMath.DegreesToRadiansValue(longitudeDegrees);
        var beta = AstronomyMath.DegreesToRadiansValue(latitudeDegrees);
        var epsilon = AstronomyMath.DegreesToRadiansValue(obliquityDegrees);

        var alpha = Math.Atan2(
            Math.Sin(lambda) * Math.Cos(epsilon) - Math.Tan(beta) * Math.Sin(epsilon),
            Math.Cos(lambda));
        var delta = Math.Asin(Math.Clamp(
            Math.Sin(beta) * Math.Cos(epsilon) + Math.Cos(beta) * Math.Sin(epsilon) * Math.Sin(lambda),
            -1.0,
            1.0));

        return new EquatorialCoordinates(
            AstronomyMath.NormalizeDegrees(AstronomyMath.RadiansToDegreesValue(alpha)) / 15.0,
            AstronomyMath.RadiansToDegreesValue(delta));
    }
}
