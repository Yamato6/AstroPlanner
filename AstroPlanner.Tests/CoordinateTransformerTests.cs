using AstroPlanner.Core.Algorithms;
using AstroPlanner.Core.Models;
using FluentAssertions;
using Xunit;

namespace AstroPlanner.Tests;

public class CoordinateTransformerTests
{
    [Fact]
    public void ToHorizontal_ObjectAtZenith_ReturnsAltitude90()
    {
        // En J2000.0 (2000-01-01 12:00 UTC) el GMST es 280.46061837°.
        // Con longitud 0, LST = GMST. Si colocamos la RA del objeto justo en
        // el LST y su Dec igual a la latitud, el ángulo horario H = 0 y el
        // objeto pasa por el zenit (altitud 90°).
        var instante = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var latitud = 40.0;
        var raGrados = AstronomyMath.CalculateGreenwichMeanSiderealTimeDegrees(instante);
        var raHoras = raGrados / 15.0;

        var equatorial = new EquatorialCoordinates(raHoras, latitud);
        var observador = new ObserverLocation(latitud, 0);

        var horizontal = CoordinateTransformer.ToHorizontal(equatorial, observador, instante);

        horizontal.AltitudeDegrees.Should().BeApproximately(90.0, 1e-4);
    }

    [Fact]
    public void ToHorizontal_ObjectOnMeridianSouth_ReturnsAzimuth180()
    {
        // Un objeto con Dec = 0 cruzando el meridiano (H = 0) para un observador
        // en latitud norte positiva se encuentra exactamente al sur (azimut 180°).
        var instante = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var latitud = 40.0;
        var raHoras = AstronomyMath.CalculateGreenwichMeanSiderealTimeDegrees(instante) / 15.0;

        var equatorial = new EquatorialCoordinates(raHoras, 0.0);
        var observador = new ObserverLocation(latitud, 0);

        var horizontal = CoordinateTransformer.ToHorizontal(equatorial, observador, instante);

        horizontal.AzimuthDegrees.Should().BeApproximately(180.0, 1e-4);
        // altitud en el tránsito = 90 - lat + dec = 90 - 40 + 0 = 50°.
        horizontal.AltitudeDegrees.Should().BeApproximately(50.0, 1e-4);
    }

    [Fact]
    public void ToHorizontal_ObjectAtSixHoursHourAngle_IsWest_Azimuth270()
    {
        // Con H = 90° (6h de ángulo horario) y lat = 0, Dec = 0, el objeto está
        // en el horizonte hacia el oeste (azimut 270°, altitud 0°).
        var latitud = 0.0;
        var dec = 0.0;
        // Queremos H = LST - RA = 90° => RA = LST - 90.
        var lstGrados = 180.0;
        var raHoras = (lstGrados - 90.0) / 15.0; // = 6h
        // Construimos una fecha tal que LST(lon=0) = 180° es complejo; en su lugar
        // usamos la longitud para ajustar. Con longitud -180 => LST = GMST - 180.
        // Buscamos un instante y usamos la fórmula directamente con RA fija.
        // Para simplificar, colocamos el objeto en el polo este: RA = LST - 90.
        // Usamos un observador en Greenwich y derivamos LST del instante.
        var instante = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var lstReal = AstronomyMath.CalculateLocalSiderealTimeDegrees(instante, 0);
        var raAjustada = AstronomyMath.NormalizeDegrees(lstReal - 90.0) / 15.0;

        var equatorial = new EquatorialCoordinates(raAjustada, dec);
        var observador = new ObserverLocation(latitud, 0);

        var horizontal = CoordinateTransformer.ToHorizontal(equatorial, observador, instante);

        // Con lat=0 y dec=0, H=90° => altitud 0°, azimut 270° (oeste).
        horizontal.AltitudeDegrees.Should().BeApproximately(0.0, 1e-4);
        horizontal.AzimuthDegrees.Should().BeApproximately(270.0, 1e-4);
    }

    [Fact]
    public void ToHorizontal_AzimuthStaysWithinRange_ForManyInstants()
    {
        // Barrido de 24h: el azimut debe permanecer siempre en [0, 360).
        var equatorial = new EquatorialCoordinates(5.59, -5.39); // M42, Orión
        var observador = new ObserverLocation(40.4, -3.7); // Madrid

        var inicio = new DateTime(2023, 10, 15, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 96; i++) // 24h cada 15 min
        {
            var t = inicio.AddMinutes(15 * i);
            var h = CoordinateTransformer.ToHorizontal(equatorial, observador, t);

            h.AltitudeDegrees.Should().BeInRange(-90.0, 90.0);
            h.AzimuthDegrees.Should().BeInRange(0.0, 360.0);
        }
    }
}
