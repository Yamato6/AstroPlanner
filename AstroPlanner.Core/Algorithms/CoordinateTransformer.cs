using AstroPlanner.Core.Models;

namespace AstroPlanner.Core.Algorithms;

public static class CoordinateTransformer
{
    public static HorizontalCoordinates ToHorizontal(
        EquatorialCoordinates equatorial,
        ObserverLocation observer,
        DateTime utcDateTime)
    {
        ArgumentNullException.ThrowIfNull(equatorial);
        ArgumentNullException.ThrowIfNull(observer);

        var localSiderealTime = AstronomyMath.CalculateLocalSiderealTimeDegrees(
            utcDateTime,
            observer.LongitudeDegrees);
        var hourAngle = AstronomyMath.NormalizeDegrees(
            localSiderealTime - equatorial.RightAscensionHours * 15.0);

        var hourAngleRadians = AstronomyMath.DegreesToRadiansValue(hourAngle);
        var latitudeRadians = AstronomyMath.DegreesToRadiansValue(observer.LatitudeDegrees);
        var declinationRadians = AstronomyMath.DegreesToRadiansValue(equatorial.DeclinationDegrees);

        var sinAltitude = Math.Sin(latitudeRadians) * Math.Sin(declinationRadians)
            + Math.Cos(latitudeRadians) * Math.Cos(declinationRadians) * Math.Cos(hourAngleRadians);
        sinAltitude = Math.Clamp(sinAltitude, -1.0, 1.0);

        var altitudeRadians = Math.Asin(sinAltitude);
        var altitudeDegrees = AstronomyMath.RadiansToDegreesValue(altitudeRadians);
        var cosAltitude = Math.Cos(altitudeRadians);
        var cosLatitude = Math.Cos(latitudeRadians);

        if (Math.Abs(cosAltitude) < 1e-10 || Math.Abs(cosLatitude) < 1e-10)
        {
            return new HorizontalCoordinates(altitudeDegrees, 0);
        }

        var cosineAzimuth = (Math.Sin(declinationRadians) - Math.Sin(latitudeRadians) * sinAltitude)
            / (cosLatitude * cosAltitude);
        cosineAzimuth = Math.Clamp(cosineAzimuth, -1.0, 1.0);

        var azimuth = AstronomyMath.RadiansToDegreesValue(Math.Acos(cosineAzimuth));
        if (Math.Sin(hourAngleRadians) > 0)
        {
            azimuth = 360.0 - azimuth;
        }

        return new HorizontalCoordinates(altitudeDegrees, AstronomyMath.NormalizeDegrees(azimuth));
    }
}
