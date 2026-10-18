namespace AstroPlanner.Core.Models;

public sealed record EquatorialCoordinates
{
    public EquatorialCoordinates(double rightAscensionHours, double declinationDegrees)
    {
        if (double.IsNaN(rightAscensionHours) || rightAscensionHours is < 0 or >= 24)
        {
            throw new ArgumentOutOfRangeException(nameof(rightAscensionHours), "Right ascension must be between 0 (inclusive) and 24 hours (exclusive).");
        }

        if (double.IsNaN(declinationDegrees) || declinationDegrees is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(declinationDegrees), "Declination must be between -90 and 90 degrees.");
        }

        RightAscensionHours = rightAscensionHours;
        DeclinationDegrees = declinationDegrees;
    }

    public double RightAscensionHours { get; }
    public double DeclinationDegrees { get; }
}

public sealed record HorizontalCoordinates(double AltitudeDegrees, double AzimuthDegrees);
