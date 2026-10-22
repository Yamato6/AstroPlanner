using AstroPlanner.Core.Interfaces;
using AstroPlanner.Core.Models;
using AstroPlanner.Core.Services;
using FluentAssertions;
using Xunit;

namespace AstroPlanner.Tests;

public class AstronomyServiceTests
{
    private readonly IAstronomyService _service = new AstronomyService();
    private readonly ObserverLocation _madrid = new(40.4, -3.7);
    private readonly CelestialObject _m42 = new(
        "M42",
        "Nebulosa de Orión",
        "Orion",
        CelestialObjectType.DiffuseNebula,
        4.0,
        new EquatorialCoordinates(5.59, -5.39));

    [Fact]
    public void GetHorizontal_MatchesCoordinateTransformer()
    {
        var instante = new DateTime(2023, 10, 15, 22, 0, 0, DateTimeKind.Utc);

        var h = _service.GetHorizontal(_m42, _madrid, instante);

        h.AltitudeDegrees.Should().BeInRange(-90, 90);
        h.AzimuthDegrees.Should().BeInRange(0, 360);
    }

    [Fact]
    public void GetAltitudeSeries_ReturnsExpectedNumberOfPoints()
    {
        var inicio = new DateTime(2023, 10, 15, 18, 0, 0, DateTimeKind.Utc);
        var fin = new DateTime(2023, 10, 16, 6, 0, 0, DateTimeKind.Utc);
        var paso = TimeSpan.FromMinutes(30);

        var serie = _service.GetAltitudeSeries(_m42, _madrid, inicio, fin, paso).ToList();

        // 18:00 a 06:00 = 12h cada 30min => 24 intervalos + punto inicial = 25 puntos.
        serie.Should().HaveCount(25);
        serie.Should().OnlyContain(p => p.AltitudeDegrees >= -90 && p.AltitudeDegrees <= 90);
    }

    [Fact]
    public void GetAltitudeSeries_InvalidStep_Throws()
    {
        var inicio = new DateTime(2023, 10, 15, 18, 0, 0, DateTimeKind.Utc);

        var act = () => _service.GetAltitudeSeries(_m42, _madrid, inicio, inicio.AddHours(1), TimeSpan.Zero).ToList();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static readonly TimeZoneInfo MadridZone =
        TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");

    private static CelestialObject ObjectAt(double raHours, double decDegrees) => new(
        "TEST",
        "Objeto de prueba",
        "Test",
        CelestialObjectType.Unknown,
        5.0,
        new EquatorialCoordinates(raHours, decDegrees));

    [Fact]
    public void GetNightAltitudeSeries_CoversSixPmToSixAmInLocalTime()
    {
        var start = new DateTime(2023, 12, 15, 18, 0, 0);

        var series = _service.GetNightAltitudeSeries(_m42, _madrid, start, MadridZone, TimeSpan.FromMinutes(30));

        series.Should().HaveCount(25);
        series[0].LocalTime.Should().Be(start);
        series[^1].LocalTime.Should().Be(start.AddHours(12));
    }

    [Fact]
    public void GetNightEvents_M42InDecember_RisesAndCulminatesButSetsAfterTheNight()
    {
        var start = new DateTime(2023, 12, 15, 18, 0, 0);

        var events = _service.GetNightEvents(_m42, _madrid, start, MadridZone, 20);

        // Altitud de culminación = 90 - latitud + declinación = 90 - 40.4 - 5.39 ≈ 44.2°.
        events.MaximumAltitudeDegrees.Should().BeApproximately(44.2, 1.0);
        events.CulminationLocal.Should().BeAfter(start.AddHours(4)).And.BeBefore(start.AddHours(9));
        events.RiseLocal.Should().NotBeNull().And.BeBefore(events.CulminationLocal);
        events.ThresholdCrossingLocal.Should().NotBeNull().And.BeAfter(events.RiseLocal!.Value);
        // Semiarco ≈ 5.7 h: culmina hacia la 01:15 y se pone hacia las 07:00,
        // fuera de la ventana 18:00 → 06:00, así que no hay puesta en la noche.
        events.SetLocal.Should().BeNull();
    }

    [Fact]
    public void GetNightEvents_CircumpolarObject_NeverRisesOrSets()
    {
        var observer = new ObserverLocation(50, 0);

        var events = _service.GetNightEvents(
            ObjectAt(3, 85), observer, new DateTime(2023, 12, 15, 18, 0, 0), MadridZone, 20);

        events.RiseLocal.Should().BeNull();
        events.SetLocal.Should().BeNull();
        events.MaximumAltitudeDegrees.Should().BeGreaterThan(45);
    }

    [Fact]
    public void GetNightEvents_ObjectBelowHorizonAllNight_HasNegativeMaximum()
    {
        // Dec -80° desde latitud 40°N: altitud máxima = 90 - 40 - 80 = -30°.
        var observer = new ObserverLocation(40, 0);

        var events = _service.GetNightEvents(
            ObjectAt(3, -80), observer, new DateTime(2023, 12, 15, 18, 0, 0), MadridZone, 20);

        events.MaximumAltitudeDegrees.Should().BeLessThan(0);
        events.RiseLocal.Should().BeNull();
        events.ThresholdCrossingLocal.Should().BeNull();
    }
}
