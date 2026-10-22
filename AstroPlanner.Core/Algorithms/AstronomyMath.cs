using AstroPlanner.Core.Models;

namespace AstroPlanner.Core.Algorithms;

public static class AstronomyMath
{
    private const double DegreesToRadians = Math.PI / 180.0;
    private const double RadiansToDegrees = 180.0 / Math.PI;

    public static double ToJulianDate(DateTime dateTime)
    {
        var utc = dateTime.Kind == DateTimeKind.Local
            ? dateTime.ToUniversalTime()
            : DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);

        var year = utc.Year;
        var month = utc.Month;
        var day = utc.Day + (utc.Hour + utc.Minute / 60.0 + utc.Second / 3600.0 + utc.Millisecond / 3_600_000.0) / 24.0;

        if (month <= 2)
        {
            year--;
            month += 12;
        }

        var a = year / 100;
        var b = 2 - a + a / 4;

        return Math.Floor(365.25 * (year + 4716))
            + Math.Floor(30.6001 * (month + 1))
            + day + b - 1524.5;
    }

    public static double CalculateGreenwichMeanSiderealTimeDegrees(DateTime utcDateTime)
    {
        var daysSinceJ2000 = ToJulianDate(utcDateTime) - 2451545.0;
        return NormalizeDegrees(280.46061837 + 360.98564736629 * daysSinceJ2000);
    }

    public static double CalculateLocalSiderealTimeDegrees(DateTime utcDateTime, double longitudeDegrees)
    {
        if (double.IsNaN(longitudeDegrees) || longitudeDegrees is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitudeDegrees));
        }

        return NormalizeDegrees(CalculateGreenwichMeanSiderealTimeDegrees(utcDateTime) + longitudeDegrees);
    }

    public static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360.0;
        return normalized < 0 ? normalized + 360.0 : normalized;
    }

    /// <summary>Distancia angular (grados) entre dos puntos del cielo (Meeus 17.1).</summary>
    public static double AngularSeparationDegrees(EquatorialCoordinates a, EquatorialCoordinates b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        var deltaA = DegreesToRadiansValue(a.DeclinationDegrees);
        var deltaB = DegreesToRadiansValue(b.DeclinationDegrees);
        var deltaAlpha = DegreesToRadiansValue((a.RightAscensionHours - b.RightAscensionHours) * 15.0);

        var cosine = Math.Sin(deltaA) * Math.Sin(deltaB)
            + Math.Cos(deltaA) * Math.Cos(deltaB) * Math.Cos(deltaAlpha);
        return RadiansToDegreesValue(Math.Acos(Math.Clamp(cosine, -1.0, 1.0)));
    }

    internal static double DegreesToRadiansValue(double degrees) => degrees * DegreesToRadians;
    internal static double RadiansToDegreesValue(double radians) => radians * RadiansToDegrees;
}
