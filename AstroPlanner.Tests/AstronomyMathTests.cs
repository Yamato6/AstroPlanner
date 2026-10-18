using AstroPlanner.Core.Algorithms;
using FluentAssertions;
using Xunit;

namespace AstroPlanner.Tests;

public class AstronomyMathTests
{
    [Fact]
    public void ToJulianDate_J2000Epoch_Returns2451545()
    {
        // 1 de enero de 2000 a las 12:00:00 UTC es exactamente el epoch J2000.0.
        var j2000 = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var jd = AstronomyMath.ToJulianDate(j2000);

        jd.Should().BeApproximately(2451545.0, 1e-6);
    }

    [Fact]
    public void ToJulianDate_KnownDate_MatchesReferenceValue()
    {
        // Fecha juliana de referencia: 2023-10-15 00:00:00 UTC -> JD 2460232.5
        var fecha = new DateTime(2023, 10, 15, 0, 0, 0, DateTimeKind.Utc);

        var jd = AstronomyMath.ToJulianDate(fecha);

        jd.Should().BeApproximately(2460232.5, 1e-6);
    }

    [Fact]
    public void ToJulianDate_LocalTime_IsConvertedToUtc()
    {
        // Una DateTime marcada como Local debe convertirse a UTC antes del cálculo.
        var utc = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var local = utc.ToLocalTime();

        var jdUtc = AstronomyMath.ToJulianDate(utc);
        var jdLocal = AstronomyMath.ToJulianDate(local);

        jdLocal.Should().BeApproximately(jdUtc, 1e-6);
    }

    [Fact]
    public void CalculateGreenwichMeanSiderealTime_J2000_ReturnsExpected()
    {
        // GMST en el epoch J2000.0 debe ser 280.46061837 grados (constante de la fórmula).
        var j2000 = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var gmst = AstronomyMath.CalculateGreenwichMeanSiderealTimeDegrees(j2000);

        gmst.Should().BeApproximately(280.46061837, 1e-6);
    }

    [Fact]
    public void CalculateLocalSiderealTime_GreenwichOffset_IsZero()
    {
        // Con longitud 0 (Greenwich), LST == GMST.
        var fecha = new DateTime(2023, 10, 15, 22, 0, 0, DateTimeKind.Utc);

        var gmst = AstronomyMath.CalculateGreenwichMeanSiderealTimeDegrees(fecha);
        var lst = AstronomyMath.CalculateLocalSiderealTimeDegrees(fecha, 0);

        lst.Should().BeApproximately(gmst, 1e-9);
    }

    [Fact]
    public void CalculateLocalSiderealTime_PositiveLongitude_AddsToGmst()
    {
        var fecha = new DateTime(2023, 10, 15, 22, 0, 0, DateTimeKind.Utc);

        var gmst = AstronomyMath.CalculateGreenwichMeanSiderealTimeDegrees(fecha);
        var lst = AstronomyMath.CalculateLocalSiderealTimeDegrees(fecha, 15);

        // 15° de longitud este => LST = GMST + 15, normalizado a [0,360).
        var esperado = (gmst + 15.0) % 360.0;
        lst.Should().BeApproximately(esperado, 1e-9);
    }

    [Fact]
    public void NormalizeDegrees_NegativeInput_ReturnsPositiveEquivalent()
    {
        AstronomyMath.NormalizeDegrees(-10).Should().BeApproximately(350, 1e-9);
        AstronomyMath.NormalizeDegrees(-360).Should().BeApproximately(0, 1e-9);
    }

    [Fact]
    public void NormalizeDegrees_LargeInput_ReturnsWithinRange()
    {
        AstronomyMath.NormalizeDegrees(725).Should().BeApproximately(5, 1e-9);
        AstronomyMath.NormalizeDegrees(360).Should().BeApproximately(0, 1e-9);
    }
}
