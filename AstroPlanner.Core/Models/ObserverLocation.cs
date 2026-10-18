namespace AstroPlanner.Core.Models;

public sealed record ObserverLocation
{
    public ObserverLocation(double latitudeDegrees, double longitudeDegrees, double elevationMeters = 0)
    {
        if (double.IsNaN(latitudeDegrees) || latitudeDegrees is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitudeDegrees), "Latitude must be between -90 and 90 degrees.");
        }

        if (double.IsNaN(longitudeDegrees) || longitudeDegrees is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitudeDegrees), "Longitude must be between -180 and 180 degrees.");
        }

        if (double.IsNaN(elevationMeters))
        {
            throw new ArgumentOutOfRangeException(nameof(elevationMeters), "Elevation must be a valid number.");
        }

        LatitudeDegrees = latitudeDegrees;
        LongitudeDegrees = longitudeDegrees;
        ElevationMeters = elevationMeters;
    }

    public double LatitudeDegrees { get; }
    public double LongitudeDegrees { get; }
    public double ElevationMeters { get; }
}
