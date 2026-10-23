using AstroPlanner.Core.Models;

namespace AstroPlanner.Core.Algorithms;

/// <summary>
/// Posiciones de los planetas a partir de los elementos orbitales keplerianos
/// de JPL ("Approximate Positions of the Planets", E. M. Standish, tabla 1,
/// válida 1800-2050). Error típico: ~1' en los planetas interiores, hasta
/// ~10' en Júpiter y Saturno; suficiente para planificar. Magnitudes y fase
/// según Meeus, "Astronomical Algorithms", cap. 41; diámetros, cap. 55.
/// </summary>
public static class PlanetCalculator
{
    /// <summary>Tiempo que tarda la luz en recorrer 1 UA, en días.</summary>
    private const double LightTimeDaysPerAu = 0.0057755183;

    /// <summary>Oblicuidad de la eclíptica J2000 (grados).</summary>
    private const double ObliquityJ2000 = 23.43928;

    /// <summary>
    /// Elementos en J2000 y su variación por siglo: semieje (UA), excentricidad,
    /// inclinación, longitud media, longitud del perihelio, longitud del nodo (grados).
    /// </summary>
    private sealed record OrbitalElements(
        double A, double E, double I, double L, double Perihelion, double Node,
        double ARate, double ERate, double IRate, double LRate, double PerihelionRate, double NodeRate);

    private static readonly OrbitalElements Mercury = new(
        0.38709927, 0.20563593, 7.00497902, 252.25032350, 77.45779628, 48.33076593,
        0.00000037, 0.00001906, -0.00594749, 149472.67411175, 0.16047689, -0.12534081);

    private static readonly OrbitalElements Venus = new(
        0.72333566, 0.00677672, 3.39467605, 181.97909950, 131.60246718, 76.67984255,
        0.00000390, -0.00004107, -0.00078890, 58517.81538729, 0.00268329, -0.27769418);

    private static readonly OrbitalElements EarthMoonBarycenter = new(
        1.00000261, 0.01671123, -0.00001531, 100.46457166, 102.93768193, 0.0,
        0.00000562, -0.00004392, -0.01294668, 35999.37244981, 0.32327364, 0.0);

    private static readonly OrbitalElements Mars = new(
        1.52371034, 0.09339410, 1.84969142, -4.55343205, -23.94362959, 49.55953891,
        0.00001847, 0.00007882, -0.00813131, 19140.30268499, 0.44441088, -0.29257343);

    private static readonly OrbitalElements Jupiter = new(
        5.20288700, 0.04838624, 1.30439695, 34.39644051, 14.72847983, 100.47390909,
        -0.00011607, -0.00013253, -0.00183714, 3034.74612775, 0.21252668, 0.20469106);

    private static readonly OrbitalElements Saturn = new(
        9.53667594, 0.05386179, 2.48599187, 49.95424423, 92.59887831, 113.66242448,
        -0.00125060, -0.00050991, 0.00193609, 1222.49362201, -0.41897216, -0.28867794);

    private static readonly OrbitalElements Uranus = new(
        19.18916464, 0.04725744, 0.77263783, 313.23810451, 170.95427630, 74.01692503,
        -0.00196176, -0.00004397, -0.00242939, 428.48202785, 0.40805281, 0.04240589);

    private static readonly OrbitalElements Neptune = new(
        30.06992276, 0.00859048, 1.77004347, -55.12002969, 44.96476227, 131.78422574,
        0.00026291, 0.00005105, 0.00035372, 218.45945325, -0.32241464, -0.00508664);

    private static OrbitalElements ElementsOf(Planet planet) => planet switch
    {
        Planet.Mercury => Mercury,
        Planet.Venus => Venus,
        Planet.Mars => Mars,
        Planet.Jupiter => Jupiter,
        Planet.Saturn => Saturn,
        Planet.Uranus => Uranus,
        Planet.Neptune => Neptune,
        _ => throw new ArgumentOutOfRangeException(nameof(planet))
    };

    /// <summary>Diámetro ecuatorial a 1 UA, en segundos de arco (Meeus, cap. 55).</summary>
    private static double DiameterAtOneAu(Planet planet) => planet switch
    {
        Planet.Mercury => 6.72,
        Planet.Venus => 16.82,
        Planet.Mars => 9.36,
        Planet.Jupiter => 196.88,
        Planet.Saturn => 165.46,
        Planet.Uranus => 70.04,
        Planet.Neptune => 67.00,
        _ => throw new ArgumentOutOfRangeException(nameof(planet))
    };

    private static double Sin(double degrees) => Math.Sin(AstronomyMath.DegreesToRadiansValue(degrees));
    private static double Cos(double degrees) => Math.Cos(AstronomyMath.DegreesToRadiansValue(degrees));

    public static PlanetPosition GetPosition(Planet planet, DateTime utcDateTime)
    {
        var jd = AstronomyMath.ToJulianDate(utcDateTime);
        var earth = Heliocentric(EarthMoonBarycenter, jd);

        // Corrección por tiempo de luz: se ve el planeta donde estaba cuando salió la luz.
        var position = Heliocentric(ElementsOf(planet), jd);
        for (var iteration = 0; iteration < 2; iteration++)
        {
            var delta = Length(Subtract(position, earth));
            position = Heliocentric(ElementsOf(planet), jd - delta * LightTimeDaysPerAu);
        }

        var geocentric = Subtract(position, earth);
        var distance = Length(geocentric);
        var sunDistance = Length(position);
        var earthSunDistance = Length(earth);

        // Eclíptica J2000 → ecuatorial J2000 → equinoccio de la fecha.
        var epsilon = AstronomyMath.DegreesToRadiansValue(ObliquityJ2000);
        var x = geocentric.X;
        var y = geocentric.Y * Math.Cos(epsilon) - geocentric.Z * Math.Sin(epsilon);
        var z = geocentric.Y * Math.Sin(epsilon) + geocentric.Z * Math.Cos(epsilon);
        var j2000 = new EquatorialCoordinates(
            AstronomyMath.NormalizeDegrees(AstronomyMath.RadiansToDegreesValue(Math.Atan2(y, x))) / 15.0,
            AstronomyMath.RadiansToDegreesValue(Math.Asin(Math.Clamp(z / distance, -1.0, 1.0))));
        var ofDate = Precession.FromJ2000(j2000, utcDateTime);

        var phaseAngle = AngleFromSides(sunDistance, distance, earthSunDistance);
        var elongation = AngleFromSides(earthSunDistance, distance, sunDistance);
        var magnitude = Magnitude(planet, sunDistance, distance, phaseAngle, geocentric, jd);

        return new PlanetPosition(
            planet,
            ofDate,
            distance,
            sunDistance,
            elongation,
            phaseAngle,
            (1 + Cos(phaseAngle)) / 2,
            magnitude,
            DiameterAtOneAu(planet) / distance);
    }

    /// <summary>
    /// Magnitud visual (Meeus 41, fórmulas del Astronomical Almanac). En Saturno
    /// se incluye el brillo de los anillos según su inclinación (Meeus 45).
    /// </summary>
    private static double Magnitude(
        Planet planet, double r, double delta, double i, (double X, double Y, double Z) geocentric, double jd)
    {
        var distanceTerm = 5 * Math.Log10(r * delta);
        return planet switch
        {
            Planet.Mercury => -0.42 + distanceTerm + 0.0380 * i - 0.000273 * i * i + 0.000002 * i * i * i,
            Planet.Venus => -4.40 + distanceTerm + 0.0009 * i + 0.000239 * i * i - 0.00000065 * i * i * i,
            Planet.Mars => -1.52 + distanceTerm + 0.016 * i,
            Planet.Jupiter => -9.40 + distanceTerm + 0.005 * i,
            Planet.Saturn => SaturnMagnitude(distanceTerm, geocentric, jd),
            Planet.Uranus => -7.19 + distanceTerm,
            Planet.Neptune => -6.87 + distanceTerm,
            _ => throw new ArgumentOutOfRangeException(nameof(planet))
        };
    }

    private static double SaturnMagnitude(double distanceTerm, (double X, double Y, double Z) geocentric, double jd)
    {
        var sinB = Math.Sin(AstronomyMath.DegreesToRadiansValue(SaturnRingTiltDegrees(geocentric, jd)));
        return -8.88 + distanceTerm - 2.60 * Math.Abs(sinB) + 1.25 * sinB * sinB;
    }

    /// <summary>
    /// Inclinación de los anillos de Saturno vista desde la Tierra (B, Meeus 45):
    /// cerca de 0° los anillos se ven de canto y Saturno es más débil.
    /// </summary>
    internal static double SaturnRingTiltDegrees((double X, double Y, double Z) geocentric, double jd)
    {
        var t = (jd - 2451545.0) / 36525.0;
        var inclination = 28.075216 - 0.012998 * t + 0.000004 * t * t;
        var node = 169.508470 + 1.394681 * t + 0.000412 * t * t;

        // Longitud y latitud eclípticas geocéntricas; la longitud se lleva al
        // equinoccio de la fecha sumando la precesión general (~1.397° por siglo).
        var lambda = AstronomyMath.RadiansToDegreesValue(Math.Atan2(geocentric.Y, geocentric.X)) + 1.3969713 * t;
        var beta = AstronomyMath.RadiansToDegreesValue(
            Math.Atan2(geocentric.Z, Math.Sqrt(geocentric.X * geocentric.X + geocentric.Y * geocentric.Y)));

        var sinB = Sin(inclination) * Cos(beta) * Sin(lambda - node) - Cos(inclination) * Sin(beta);
        return AstronomyMath.RadiansToDegreesValue(Math.Asin(Math.Clamp(sinB, -1.0, 1.0)));
    }

    /// <summary>Posición heliocéntrica en la eclíptica J2000 (UA).</summary>
    private static (double X, double Y, double Z) Heliocentric(OrbitalElements el, double jd)
    {
        var t = (jd - 2451545.0) / 36525.0;
        var a = el.A + el.ARate * t;
        var e = el.E + el.ERate * t;
        var inclination = el.I + el.IRate * t;
        var meanLongitude = el.L + el.LRate * t;
        var perihelion = el.Perihelion + el.PerihelionRate * t;
        var node = el.Node + el.NodeRate * t;

        var argumentOfPerihelion = perihelion - node;
        var meanAnomaly = AstronomyMath.NormalizeDegrees(meanLongitude - perihelion);
        if (meanAnomaly > 180)
        {
            meanAnomaly -= 360;
        }

        // Ecuación de Kepler por Newton-Raphson (en radianes).
        var m = AstronomyMath.DegreesToRadiansValue(meanAnomaly);
        var eccentricAnomaly = m + e * Math.Sin(m);
        for (var iteration = 0; iteration < 20; iteration++)
        {
            var step = (eccentricAnomaly - e * Math.Sin(eccentricAnomaly) - m) / (1 - e * Math.Cos(eccentricAnomaly));
            eccentricAnomaly -= step;
            if (Math.Abs(step) < 1e-12)
            {
                break;
            }
        }

        // Coordenadas en el plano de la órbita y rotación a la eclíptica.
        var xOrbit = a * (Math.Cos(eccentricAnomaly) - e);
        var yOrbit = a * Math.Sqrt(1 - e * e) * Math.Sin(eccentricAnomaly);

        var cw = Cos(argumentOfPerihelion);
        var sw = Sin(argumentOfPerihelion);
        var cn = Cos(node);
        var sn = Sin(node);
        var ci = Cos(inclination);
        var si = Sin(inclination);

        return (
            (cw * cn - sw * sn * ci) * xOrbit + (-sw * cn - cw * sn * ci) * yOrbit,
            (cw * sn + sw * cn * ci) * xOrbit + (-sw * sn + cw * cn * ci) * yOrbit,
            sw * si * xOrbit + cw * si * yOrbit);
    }

    private static (double X, double Y, double Z) Subtract((double X, double Y, double Z) a, (double X, double Y, double Z) b) =>
        (a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    private static double Length((double X, double Y, double Z) v) => Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);

    /// <summary>Ángulo (grados) opuesto al lado <paramref name="opposite"/>, por la ley del coseno.</summary>
    private static double AngleFromSides(double adjacent1, double adjacent2, double opposite)
    {
        var cosine = (adjacent1 * adjacent1 + adjacent2 * adjacent2 - opposite * opposite) / (2 * adjacent1 * adjacent2);
        return AstronomyMath.RadiansToDegreesValue(Math.Acos(Math.Clamp(cosine, -1.0, 1.0)));
    }
}
