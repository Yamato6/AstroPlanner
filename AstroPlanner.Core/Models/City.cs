namespace AstroPlanner.Core.Models;

public sealed record City(
    string Id,
    string Name,
    string State,
    string Country,
    double LatitudeDegrees,
    double LongitudeDegrees,
    double ElevationMeters,
    string TimeZoneId)
{
    public string DisplayName => $"{Name}, {State}";
}
