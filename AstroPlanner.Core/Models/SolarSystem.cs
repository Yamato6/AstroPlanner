namespace AstroPlanner.Core.Models;

/// <summary>Posición geocéntrica del Sol (equinoccio de la fecha).</summary>
public sealed record SunPosition(
    double EclipticLongitudeDegrees,
    EquatorialCoordinates Equatorial,
    double DistanceKm);

/// <summary>Posición geocéntrica de la Luna (equinoccio medio de la fecha).</summary>
public sealed record MoonPosition(
    double EclipticLongitudeDegrees,
    double EclipticLatitudeDegrees,
    EquatorialCoordinates Equatorial,
    double DistanceKm,
    double HorizontalParallaxDegrees);

/// <summary>
/// La Luna vista por un observador en un instante: posición en el cielo
/// (topocéntrica, con refracción) y fase.
/// </summary>
/// <param name="IlluminatedFraction">0 = Luna nueva, 1 = Luna llena.</param>
/// <param name="IsWaxing">True entre Luna nueva y llena (creciente).</param>
public sealed record MoonState(
    HorizontalCoordinates Horizontal,
    EquatorialCoordinates Equatorial,
    double IlluminatedFraction,
    bool IsWaxing)
{
    public bool IsAboveHorizon => Horizontal.AltitudeDegrees > 0;
}

/// <summary>Estado del cielo según la altura del Sol.</summary>
public enum SkyCondition
{
    /// <summary>Sol sobre el horizonte.</summary>
    Day,

    /// <summary>Sol entre 0° y -18°: el cielo aún no está totalmente oscuro.</summary>
    Twilight,

    /// <summary>Sol por debajo de -18°: noche astronómica.</summary>
    Dark
}

/// <summary>
/// Noche de observación en hora local: la ventana va de la puesta a la salida
/// del Sol; la noche oscura, del final al inicio del crepúsculo astronómico.
/// </summary>
/// <param name="WindowStartLocal">Puesta de Sol, o 18:00 si el Sol no se pone (día/noche polar).</param>
/// <param name="WindowEndLocal">Salida del Sol, o 06:00 si el Sol no sale.</param>
/// <param name="AstronomicalDuskLocal">Fin del crepúsculo vespertino (Sol a -18°); null si no oscurece del todo.</param>
/// <param name="AstronomicalDawnLocal">Inicio del crepúsculo matutino (Sol a -18°).</param>
public sealed record NightInfo(
    DateTime WindowStartLocal,
    DateTime WindowEndLocal,
    DateTime? SunsetLocal,
    DateTime? SunriseLocal,
    DateTime? AstronomicalDuskLocal,
    DateTime? AstronomicalDawnLocal)
{
    public bool HasAstronomicalDarkness => AstronomicalDuskLocal is not null && AstronomicalDawnLocal is not null;

    /// <summary>True si la ventana es la de reserva (18:00 → 06:00) por no haber puesta o salida del Sol.</summary>
    public bool IsFallbackWindow => SunsetLocal is null || SunriseLocal is null;
}
