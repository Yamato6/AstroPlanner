using AstroPlanner.Core.Algorithms;
using AstroPlanner.Core.Interfaces;
using AstroPlanner.Core.Models;

namespace AstroPlanner.Core.Services;

/// <summary>
/// Implementación por defecto de <see cref="IAstronomyService"/>.
/// Delega los cálculos a las clases estáticas de <c>Algorithms</c>.
/// Vive en Core porque no depende de UI y reutiliza lógica pura ya testeada.
/// </summary>
public sealed class AstronomyService : IAstronomyService
{
    /// <summary>
    /// Altitud del centro del Sol en la puesta/salida: radio aparente (16')
    /// más refracción en el horizonte (34'), convención estándar.
    /// </summary>
    public const double SunriseSunsetAltitudeDegrees = -0.833;

    /// <summary>Sol a -18°: fin/inicio del crepúsculo astronómico.</summary>
    public const double AstronomicalTwilightAltitudeDegrees = -18.0;

    private static readonly TimeSpan SunSampleStep = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan EventSampleStep = TimeSpan.FromMinutes(10);

    public HorizontalCoordinates GetHorizontal(
        CelestialObject objeto,
        ObserverLocation observador,
        DateTime utcDateTime)
    {
        ArgumentNullException.ThrowIfNull(objeto);
        ArgumentNullException.ThrowIfNull(observador);

        // Catálogo J2000 → coordenadas de la fecha → horizonte → altitud aparente.
        var ofDate = Precession.FromJ2000(objeto.Coordinates, utcDateTime);
        var geometric = CoordinateTransformer.ToHorizontal(ofDate, observador, utcDateTime);
        return geometric with { AltitudeDegrees = Refraction.ApparentAltitude(geometric.AltitudeDegrees) };
    }

    public IEnumerable<(DateTime Time, double AltitudeDegrees)> GetAltitudeSeries(
        CelestialObject objeto,
        ObserverLocation observador,
        DateTime inicioUtc,
        DateTime finUtc,
        TimeSpan paso)
    {
        ArgumentNullException.ThrowIfNull(objeto);
        ArgumentNullException.ThrowIfNull(observador);

        if (paso <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(paso), "El paso temporal debe ser positivo.");
        }

        for (var t = inicioUtc; t <= finUtc; t += paso)
        {
            yield return (t, GetHorizontal(objeto, observador, t).AltitudeDegrees);
        }
    }

    public IReadOnlyList<(DateTime LocalTime, double AltitudeDegrees)> GetNightAltitudeSeries(
        CelestialObject objeto,
        ObserverLocation observador,
        DateTime inicioLocal,
        DateTime finLocal,
        TimeZoneInfo zonaHoraria,
        TimeSpan paso)
    {
        ArgumentNullException.ThrowIfNull(objeto);
        ArgumentNullException.ThrowIfNull(observador);

        return SampleLocal(inicioLocal, finLocal, zonaHoraria, paso,
            utc => GetHorizontal(objeto, observador, utc).AltitudeDegrees);
    }

    public ObjectNightEvents GetNightEvents(
        CelestialObject objeto,
        ObserverLocation observador,
        DateTime inicioLocal,
        DateTime finLocal,
        TimeZoneInfo zonaHoraria,
        double altitudMinimaDegrees)
    {
        var samples = GetNightAltitudeSeries(objeto, observador, inicioLocal, finLocal, zonaHoraria, EventSampleStep);
        var maximum = samples.MaxBy(sample => sample.AltitudeDegrees);

        return new ObjectNightEvents(
            RiseLocal: ObservationNight.FindCrossing(samples, 0, rising: true),
            ThresholdCrossingLocal: ObservationNight.FindCrossing(samples, altitudMinimaDegrees, rising: true),
            CulminationLocal: maximum.LocalTime,
            SetLocal: ObservationNight.FindCrossing(samples, 0, rising: false),
            MaximumAltitudeDegrees: maximum.AltitudeDegrees);
    }

    public double GetSunAltitudeDegrees(ObserverLocation observador, DateTime utcDateTime)
    {
        ArgumentNullException.ThrowIfNull(observador);

        // Altitud geométrica del centro: las convenciones de puesta (-0.833°) y
        // crepúsculo (-18°) ya incluyen la refracción.
        var sun = SolarSystemCalculator.GetSunPosition(utcDateTime);
        return CoordinateTransformer.ToHorizontal(sun.Equatorial, observador, utcDateTime).AltitudeDegrees;
    }

    public SkyCondition GetSkyCondition(ObserverLocation observador, DateTime utcDateTime)
    {
        var sunAltitude = GetSunAltitudeDegrees(observador, utcDateTime);
        return sunAltitude switch
        {
            > SunriseSunsetAltitudeDegrees => SkyCondition.Day,
            > AstronomicalTwilightAltitudeDegrees => SkyCondition.Twilight,
            _ => SkyCondition.Dark
        };
    }

    public NightInfo GetNightInfo(ObserverLocation observador, DateTime horaLocal, TimeZoneInfo zonaHoraria)
    {
        ArgumentNullException.ThrowIfNull(observador);

        // Se busca de mediodía a mediodía alrededor de la noche que contiene la hora.
        var nightDate = ObservationNight.GetNightStartLocal(horaLocal).Date;
        var noon = nightDate.AddHours(12);
        var samples = SampleLocal(noon, noon.AddDays(1), zonaHoraria, SunSampleStep,
            utc => GetSunAltitudeDegrees(observador, utc));

        var sunset = ObservationNight.FindCrossing(samples, SunriseSunsetAltitudeDegrees, rising: false);
        var sunrise = ObservationNight.FindCrossing(samples, SunriseSunsetAltitudeDegrees, rising: true);
        var dusk = ObservationNight.FindCrossing(samples, AstronomicalTwilightAltitudeDegrees, rising: false);
        var dawn = ObservationNight.FindCrossing(samples, AstronomicalTwilightAltitudeDegrees, rising: true);

        // Sin puesta o salida (día o noche polar) se usa la ventana fija 18:00 → 06:00.
        var hasSunWindow = sunset is not null && sunrise is not null && sunrise > sunset;
        var start = hasSunWindow ? sunset!.Value : nightDate.AddHours(ObservationNight.StartHour);
        var end = hasSunWindow ? sunrise!.Value : start + ObservationNight.Duration;

        return new NightInfo(
            WindowStartLocal: start,
            WindowEndLocal: end,
            SunsetLocal: hasSunWindow ? sunset : null,
            SunriseLocal: hasSunWindow ? sunrise : null,
            AstronomicalDuskLocal: dusk is not null && dawn is not null && dawn > dusk ? dusk : null,
            AstronomicalDawnLocal: dusk is not null && dawn is not null && dawn > dusk ? dawn : null);
    }

    public MoonState GetMoon(ObserverLocation observador, DateTime utcDateTime)
    {
        ArgumentNullException.ThrowIfNull(observador);

        var moon = SolarSystemCalculator.GetMoonPosition(utcDateTime);
        var sun = SolarSystemCalculator.GetSunPosition(utcDateTime);
        var geocentric = CoordinateTransformer.ToHorizontal(moon.Equatorial, observador, utcDateTime);

        // Paralaje: vista desde la superficie la Luna aparece hasta ~1° más baja.
        var topocentricAltitude = geocentric.AltitudeDegrees
            - moon.HorizontalParallaxDegrees * Math.Cos(geocentric.AltitudeDegrees * Math.PI / 180.0);

        return new MoonState(
            Horizontal: geocentric with { AltitudeDegrees = Refraction.ApparentAltitude(topocentricAltitude) },
            Equatorial: moon.Equatorial,
            IlluminatedFraction: SolarSystemCalculator.GetMoonIlluminatedFraction(moon, sun),
            IsWaxing: SolarSystemCalculator.IsMoonWaxing(moon, sun));
    }

    public double GetMoonSeparationDegrees(CelestialObject objeto, MoonState luna, DateTime utcDateTime)
    {
        ArgumentNullException.ThrowIfNull(objeto);
        ArgumentNullException.ThrowIfNull(luna);

        // Ambas en coordenadas de la fecha para que sean comparables.
        return AstronomyMath.AngularSeparationDegrees(
            Precession.FromJ2000(objeto.Coordinates, utcDateTime),
            luna.Equatorial);
    }

    public IReadOnlyList<(DateTime LocalTime, double AltitudeDegrees)> GetMoonAltitudeSeries(
        ObserverLocation observador,
        DateTime inicioLocal,
        DateTime finLocal,
        TimeZoneInfo zonaHoraria,
        TimeSpan paso)
    {
        ArgumentNullException.ThrowIfNull(observador);

        return SampleLocal(inicioLocal, finLocal, zonaHoraria, paso,
            utc => GetMoon(observador, utc).Horizontal.AltitudeDegrees);
    }

    /// <summary>
    /// Muestrea <paramref name="altitudeAt"/> en hora local de inicio a fin
    /// (ambos incluidos). El muestreo es en hora local para que el eje del
    /// gráfico sea regular; cada punto se convierte a UTC por separado, lo que
    /// respeta el horario de verano.
    /// </summary>
    private static List<(DateTime LocalTime, double AltitudeDegrees)> SampleLocal(
        DateTime startLocal,
        DateTime endLocal,
        TimeZoneInfo timeZone,
        TimeSpan step,
        Func<DateTime, double> altitudeAt)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        if (step <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(step), "El paso temporal debe ser positivo.");
        }

        if (endLocal < startLocal)
        {
            throw new ArgumentOutOfRangeException(nameof(endLocal), "El fin debe ser posterior al inicio.");
        }

        var points = new List<(DateTime LocalTime, double AltitudeDegrees)>();
        for (var local = startLocal; local < endLocal; local += step)
        {
            points.Add((local, altitudeAt(TimeZoneConversion.ToUtc(local, timeZone))));
        }

        points.Add((endLocal, altitudeAt(TimeZoneConversion.ToUtc(endLocal, timeZone))));
        return points;
    }
}
