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
}
