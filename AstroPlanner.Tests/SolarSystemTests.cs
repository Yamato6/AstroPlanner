using AstroPlanner.Core.Algorithms;
using AstroPlanner.Core.Models;
using AstroPlanner.Core.Services;
using FluentAssertions;

namespace AstroPlanner.Tests;

/// <summary>
/// Pruebas de referencia contra los ejemplos resueltos de Meeus,
/// "Astronomical Algorithms" (2.ª ed.), y efemérides conocidas.
/// </summary>
public sealed class SolarSystemTests
{
    private static DateTime FromJulianDate(double jd) =>
        new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc).AddDays(jd - 2451545.0);

    [Fact]
    public void Precession_MatchesMeeusExample21b()
    {
        // θ Persei hasta 2028-11-13.19 (Meeus 21.b). La entrada ya incluye el
        // movimiento propio: α0 = 2h44m12.975s = 41.054063°, δ0 = +49°13'39.90".
        var j2000 = new EquatorialCoordinates(41.054063 / 15.0, 49.227750);

        var ofDate = Precession.FromJ2000(j2000, FromJulianDate(2462088.69));

        (ofDate.RightAscensionHours * 15.0).Should().BeApproximately(41.547214, 0.001);
        ofDate.DeclinationDegrees.Should().BeApproximately(49.348483, 0.001);
    }

    [Fact]
    public void Precession_AtJ2000_IsIdentity()
    {
        var j2000 = new EquatorialCoordinates(5.59, -5.39);

        var ofDate = Precession.FromJ2000(j2000, new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        ofDate.RightAscensionHours.Should().BeApproximately(5.59, 1e-9);
        ofDate.DeclinationDegrees.Should().BeApproximately(-5.39, 1e-9);
    }

    [Theory]
    [InlineData(-0.5667, 0.57)]   // en el horizonte aparente la refracción es ~34'
    [InlineData(45, 0.0167)]       // ~1' a 45°
    [InlineData(90, 0.0)]          // nada en el cénit
    public void Refraction_MatchesStandardValues(double trueAltitude, double expectedCorrection)
    {
        Refraction.CorrectionDegrees(trueAltitude).Should().BeApproximately(expectedCorrection, 0.005);
    }

    [Fact]
    public void Refraction_WellBelowHorizon_IsNotApplied()
    {
        Refraction.ApparentAltitude(-10).Should().Be(-10);
    }

    [Fact]
    public void SunPosition_MatchesMeeusExample25a()
    {
        // 1992-10-13 0h TD: α = 198.38083°, δ = -7.78507° (aparentes).
        var sun = SolarSystemCalculator.GetSunPosition(new DateTime(1992, 10, 13, 0, 0, 0, DateTimeKind.Utc));

        (sun.Equatorial.RightAscensionHours * 15.0).Should().BeApproximately(198.38083, 0.02);
        sun.Equatorial.DeclinationDegrees.Should().BeApproximately(-7.78507, 0.02);
        (sun.DistanceKm / 149_597_870.7).Should().BeApproximately(0.99766, 0.0005);
    }

    [Fact]
    public void MoonPosition_MatchesMeeusExample47a()
    {
        // 1992-04-12 0h TD: α = 134.688470°, δ = 13.768368°, distancia 368409.7 km.
        // Las fórmulas de baja precisión tienen un error de ~0.3°.
        var moon = SolarSystemCalculator.GetMoonPosition(new DateTime(1992, 4, 12, 0, 0, 0, DateTimeKind.Utc));

        (moon.Equatorial.RightAscensionHours * 15.0).Should().BeApproximately(134.688470, 0.5);
        moon.Equatorial.DeclinationDegrees.Should().BeApproximately(13.768368, 0.5);
        moon.DistanceKm.Should().BeApproximately(368409.7, 2500);
    }

    [Fact]
    public void MoonIllumination_MatchesMeeusExample48a()
    {
        // 1992-04-12 0h TD: fracción iluminada k = 0.6786, Luna creciente.
        var utc = new DateTime(1992, 4, 12, 0, 0, 0, DateTimeKind.Utc);
        var moon = SolarSystemCalculator.GetMoonPosition(utc);
        var sun = SolarSystemCalculator.GetSunPosition(utc);

        SolarSystemCalculator.GetMoonIlluminatedFraction(moon, sun).Should().BeApproximately(0.6786, 0.02);
        SolarSystemCalculator.IsMoonWaxing(moon, sun).Should().BeTrue();
    }

    [Theory]
    [InlineData(2026, 1, 3, 10, 3, 1.0)]   // Luna llena del 3 de enero de 2026 (10:03 UT)
    [InlineData(2026, 1, 18, 19, 52, 0.0)] // Luna nueva del 18 de enero de 2026 (19:52 UT)
    public void MoonIllumination_FullAndNewMoon2026(int year, int month, int day, int hour, int minute, double expected)
    {
        var utc = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);

        var fraction = SolarSystemCalculator.GetMoonIlluminatedFraction(
            SolarSystemCalculator.GetMoonPosition(utc),
            SolarSystemCalculator.GetSunPosition(utc));

        fraction.Should().BeApproximately(expected, 0.02);
    }

    [Fact]
    public void AngularSeparation_MatchesMeeusExample17a()
    {
        // Arturo y Espiga: 32.7930°.
        var arcturus = new EquatorialCoordinates(213.9154 / 15.0, 19.1825);
        var spica = new EquatorialCoordinates(201.2983 / 15.0, -11.1614);

        AstronomyMath.AngularSeparationDegrees(arcturus, spica).Should().BeApproximately(32.7930, 0.001);
    }

    [Fact]
    public void NightInfo_MexicoCitySummerSolstice_MatchesPublishedSunsetAndSunrise()
    {
        // CDMX (UTC-6 todo el año): 21/6/2026 puesta ≈ 19:17, 22/6 salida ≈ 05:59.
        var service = new AstronomyService();
        var cdmx = new ObserverLocation(19.4326, -99.1332, 2240);
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time (Mexico)");

        var night = service.GetNightInfo(cdmx, new DateTime(2026, 6, 21, 22, 0, 0), zone);

        night.SunsetLocal.Should().BeCloseTo(new DateTime(2026, 6, 21, 19, 17, 0), TimeSpan.FromMinutes(3));
        night.SunriseLocal.Should().BeCloseTo(new DateTime(2026, 6, 22, 5, 59, 0), TimeSpan.FromMinutes(3));
        night.HasAstronomicalDarkness.Should().BeTrue();
        night.AstronomicalDuskLocal.Should().BeAfter(night.SunsetLocal!.Value);
        night.AstronomicalDawnLocal.Should().BeBefore(night.SunriseLocal!.Value);
        night.WindowStartLocal.Should().Be(night.SunsetLocal!.Value);
        night.IsFallbackWindow.Should().BeFalse();
    }

    [Fact]
    public void NightInfo_HighLatitudeMidsummer_HasNoAstronomicalDarkness()
    {
        // Latitud 60°N en el solsticio: el Sol baja solo a ~-6.5°, nunca a -18°.
        var service = new AstronomyService();
        var observer = new ObserverLocation(60, 10.75);
        var zone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

        var night = service.GetNightInfo(observer, new DateTime(2026, 6, 21, 23, 0, 0), zone);

        night.SunsetLocal.Should().NotBeNull();
        night.HasAstronomicalDarkness.Should().BeFalse();
    }

    [Fact]
    public void NightInfo_PolarDay_FallsBackToFixedWindow()
    {
        // Latitud 78°N en junio: el Sol no se pone.
        var service = new AstronomyService();
        var observer = new ObserverLocation(78, 15);
        var zone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

        var night = service.GetNightInfo(observer, new DateTime(2026, 6, 21, 23, 0, 0), zone);

        night.IsFallbackWindow.Should().BeTrue();
        night.WindowStartLocal.Should().Be(new DateTime(2026, 6, 21, 18, 0, 0));
        night.WindowEndLocal.Should().Be(new DateTime(2026, 6, 22, 6, 0, 0));
    }

    [Fact]
    public void SkyCondition_FollowsSunAltitude()
    {
        var service = new AstronomyService();
        var cdmx = new ObserverLocation(19.4326, -99.1332);

        // Horas UTC del 21/6/2026 (CDMX = UTC-6).
        service.GetSkyCondition(cdmx, new DateTime(2026, 6, 21, 18, 0, 0, DateTimeKind.Utc)).Should().Be(SkyCondition.Day);
        service.GetSkyCondition(cdmx, new DateTime(2026, 6, 22, 1, 40, 0, DateTimeKind.Utc)).Should().Be(SkyCondition.Twilight);
        service.GetSkyCondition(cdmx, new DateTime(2026, 6, 22, 6, 0, 0, DateTimeKind.Utc)).Should().Be(SkyCondition.Dark);
    }

    [Fact]
    public void Moon_TopocentricAltitudeIsLowerThanGeocentric()
    {
        var service = new AstronomyService();
        var observer = new ObserverLocation(19.4326, -99.1332);
        var utc = new DateTime(2026, 1, 3, 6, 0, 0, DateTimeKind.Utc);

        var moon = service.GetMoon(observer, utc);
        var geocentric = CoordinateTransformer.ToHorizontal(
            SolarSystemCalculator.GetMoonPosition(utc).Equatorial, observer, utc);

        // Paralaje (~1° cerca del horizonte) menos refracción: siempre más baja salvo en el horizonte.
        if (geocentric.AltitudeDegrees > 10)
        {
            moon.Horizontal.AltitudeDegrees.Should().BeLessThan(geocentric.AltitudeDegrees);
        }

        moon.IlluminatedFraction.Should().BeGreaterThan(0.95, "es casi Luna llena");
    }
}
