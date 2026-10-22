using AstroPlanner.Core.Algorithms;
using FluentAssertions;

namespace AstroPlanner.Tests;

public sealed class ObservationNightTests
{
    [Theory]
    [InlineData(2026, 3, 5, 2, 0, 2026, 3, 4)]   // madrugada → noche que empezó el día anterior
    [InlineData(2026, 3, 5, 11, 59, 2026, 3, 4)] // justo antes del mediodía
    [InlineData(2026, 3, 5, 12, 0, 2026, 3, 5)]  // desde el mediodía → noche del mismo día
    [InlineData(2026, 3, 5, 20, 0, 2026, 3, 5)]
    [InlineData(2026, 1, 1, 1, 30, 2025, 12, 31)] // cruza el cambio de año
    public void GetNightStartLocal_ReturnsSixPmOfTheNightContainingTheTime(
        int year, int month, int day, int hour, int minute,
        int expectedYear, int expectedMonth, int expectedDay)
    {
        var start = ObservationNight.GetNightStartLocal(new DateTime(year, month, day, hour, minute, 0));

        start.Should().Be(new DateTime(expectedYear, expectedMonth, expectedDay, 18, 0, 0));
    }

    [Fact]
    public void FindCrossing_Rising_InterpolatesBetweenSamples()
    {
        var t0 = new DateTime(2026, 3, 4, 20, 0, 0);
        var samples = new List<(DateTime, double)>
        {
            (t0, -10),
            (t0.AddMinutes(15), 10),
            (t0.AddMinutes(30), 20)
        };

        var crossing = ObservationNight.FindCrossing(samples, 0, rising: true);

        crossing.Should().Be(t0.AddMinutes(7.5));
    }

    [Fact]
    public void FindCrossing_Setting_FindsDownwardCrossingOfThreshold()
    {
        var t0 = new DateTime(2026, 3, 4, 20, 0, 0);
        var samples = new List<(DateTime, double)>
        {
            (t0, 40),
            (t0.AddMinutes(15), 30),
            (t0.AddMinutes(30), 10)
        };

        var crossing = ObservationNight.FindCrossing(samples, 20, rising: false);

        crossing.Should().Be(t0.AddMinutes(22.5));
    }

    [Fact]
    public void FindCrossing_NoCrossing_ReturnsNull()
    {
        var t0 = new DateTime(2026, 3, 4, 20, 0, 0);
        var samples = new List<(DateTime, double)> { (t0, 30), (t0.AddMinutes(15), 35) };

        ObservationNight.FindCrossing(samples, 0, rising: true).Should().BeNull();
        ObservationNight.FindCrossing(samples, 0, rising: false).Should().BeNull();
    }
}
