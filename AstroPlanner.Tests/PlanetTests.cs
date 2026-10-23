using AstroPlanner.Core.Algorithms;
using AstroPlanner.Core.Models;
using AstroPlanner.Core.Services;
using AstroPlanner.Data.Repositories;
using FluentAssertions;

namespace AstroPlanner.Tests;

/// <summary>
/// Planetas: ejemplo resuelto de Meeus (Venus, 33.a y 41.a) y oposiciones
/// y magnitudes publicadas para 2025-2026.
/// </summary>
public sealed class PlanetTests
{
    private static DateTime Utc(int year, int month, int day, int hour = 0) =>
        new(year, month, day, hour, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Venus_MatchesMeeusExample33a()
    {
        // 1992-12-20 0h TD: α = 21h04m41.454s = 316.17273°, δ = -18°53'16.84", Δ = 0.910947 UA.
        var venus = PlanetCalculator.GetPosition(Planet.Venus, Utc(1992, 12, 20));

        (venus.Equatorial.RightAscensionHours * 15.0).Should().BeApproximately(316.17273, 0.05);
        venus.Equatorial.DeclinationDegrees.Should().BeApproximately(-18.88801, 0.05);
        venus.DistanceAu.Should().BeApproximately(0.910947, 0.002);
        venus.SunDistanceAu.Should().BeApproximately(0.724604, 0.002);
    }

    [Fact]
    public void Venus_PhaseMatchesMeeusExample41a()
    {
        // Mismo instante: fracción iluminada k = 0.647, ángulo de fase ≈ 72.96°.
        var venus = PlanetCalculator.GetPosition(Planet.Venus, Utc(1992, 12, 20));

        venus.IlluminatedFraction.Should().BeApproximately(0.647, 0.01);
        venus.PhaseAngleDegrees.Should().BeApproximately(72.96, 0.5);
        venus.Magnitude.Should().BeInRange(-4.6, -3.8);
    }

    [Theory]
    [InlineData(Planet.Mars, 2025, 1, 16, -1.4, 0.3)]     // oposición de Marte
    [InlineData(Planet.Saturn, 2025, 9, 21, 0.6, 0.3)]    // oposición de Saturno (anillos casi de canto)
    [InlineData(Planet.Jupiter, 2026, 1, 10, -2.7, 0.3)]  // oposición de Júpiter
    public void Oppositions_AreOppositeTheSunWithPublishedMagnitude(
        Planet planet, int year, int month, int day, double magnitude, double tolerance)
    {
        var position = PlanetCalculator.GetPosition(planet, Utc(year, month, day));

        position.ElongationDegrees.Should().BeGreaterThan(175, "en oposición el planeta está frente al Sol");
        position.Magnitude.Should().BeApproximately(magnitude, tolerance);
        position.IlluminatedFraction.Should().BeGreaterThan(0.99);
    }

    [Fact]
    public void Jupiter_AtOppositionHasAbout46ArcsecondsDiameter()
    {
        var jupiter = PlanetCalculator.GetPosition(Planet.Jupiter, Utc(2026, 1, 10));

        jupiter.ApparentDiameterArcsec.Should().BeApproximately(46.5, 1.5);
    }

    [Fact]
    public void InnerPlanets_NeverStrayFarFromTheSun()
    {
        // Elongación máxima: Mercurio ~28°, Venus ~47°, a lo largo de dos años.
        for (var day = 0; day < 730; day += 5)
        {
            var when = Utc(2025, 1, 1).AddDays(day);
            PlanetCalculator.GetPosition(Planet.Mercury, when).ElongationDegrees.Should().BeLessThan(28.5);
            PlanetCalculator.GetPosition(Planet.Venus, when).ElongationDegrees.Should().BeLessThan(47.5);
        }
    }

    [Fact]
    public void AstronomyService_UsesThePlanetPositionOfTheDate()
    {
        var service = new AstronomyService();
        var mars = SolarSystemCatalog.Planets.Single(p => p.Planet == Planet.Mars);
        var observer = new ObserverLocation(19.4326, -99.1332);
        var when = Utc(2025, 1, 16, 6);

        var horizontal = service.GetHorizontal(mars, observer, when);
        var expected = CoordinateTransformer.ToHorizontal(
            PlanetCalculator.GetPosition(Planet.Mars, when).Equatorial, observer, when);

        horizontal.AzimuthDegrees.Should().BeApproximately(expected.AzimuthDegrees, 1e-9);
        horizontal.AltitudeDegrees.Should().BeApproximately(Refraction.ApparentAltitude(expected.AltitudeDegrees), 1e-9);
        service.GetApparentMagnitude(mars, when).Should().BeApproximately(-1.4, 0.3);
        service.GetPlanetPosition(TestCatalog.M42, when).Should().BeNull();
        service.GetApparentMagnitude(TestCatalog.M42, when).Should().Be(4.0);
    }

    [Fact]
    public async Task CatalogWithPlanets_AddsTheSevenPlanetsAfterTheMessierObjects()
    {
        var repository = new CatalogWithPlanetsRepository(new JsonCelestialRepository());

        var objects = await repository.GetAllAsync();

        objects.Should().HaveCount(117);
        objects.Where(o => o.IsPlanet).Select(o => o.CommonName)
            .Should().Equal("Mercurio", "Venus", "Marte", "Júpiter", "Saturno", "Urano", "Neptuno");
        objects.Where(o => o.IsPlanet).Should().OnlyContain(o =>
            o.Type == CelestialObjectType.Planet && o.Constellation == CelestialObject.SolarSystemConstellation);
    }
}
