namespace AstroPlanner.Core.Algorithms;

/// <summary>
/// Refracción atmosférica estándar (10 °C, 1010 hPa). La atmósfera eleva
/// aparentemente los astros: ~34' en el horizonte, ~1' a 45°, 0 en el cénit.
/// Fórmula de Saemundsson (Meeus, cap. 16), de altitud verdadera a aparente.
/// </summary>
public static class Refraction
{
    /// <summary>
    /// Por debajo de este valor la fórmula pierde validez; el objeto está bajo
    /// el horizonte de todos modos y se devuelve la altitud sin corregir.
    /// </summary>
    private const double MinimumAltitudeDegrees = -2.0;

    /// <summary>Corrección (en grados, ≥ 0) que hay que sumar a la altitud verdadera.</summary>
    public static double CorrectionDegrees(double trueAltitudeDegrees)
    {
        if (double.IsNaN(trueAltitudeDegrees) || trueAltitudeDegrees < MinimumAltitudeDegrees)
        {
            return 0;
        }

        var argument = AstronomyMath.DegreesToRadiansValue(
            trueAltitudeDegrees + 10.3 / (trueAltitudeDegrees + 5.11));

        // R en minutos de arco; +0.0019279 hace R = 0 exactamente en el cénit.
        var minutes = 1.02 / Math.Tan(argument) + 0.0019279;
        return Math.Max(0, minutes / 60.0);
    }

    /// <summary>Altitud aparente (la que se observa) a partir de la verdadera.</summary>
    public static double ApparentAltitude(double trueAltitudeDegrees) =>
        trueAltitudeDegrees + CorrectionDegrees(trueAltitudeDegrees);
}
