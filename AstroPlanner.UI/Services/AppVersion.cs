using System.Reflection;

namespace AstroPlanner.UI.Services;

/// <summary>
/// Versión semántica simplificada: "1.0.0" o "1.0.0-rc.1". Una pre-release es
/// anterior a la versión final del mismo número (1.0.0-rc.1 &lt; 1.0.0).
/// </summary>
public sealed record AppVersion(Version Number, string? PreRelease = null) : IComparable<AppVersion>
{
    public bool IsPreRelease => PreRelease is not null;

    /// <summary>Versión de este ejecutable (la que puso el CI a partir del tag).</summary>
    public static AppVersion Current { get; } =
        TryParse(typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion)
        ?? new AppVersion(Normalize(typeof(AppVersion).Assembly.GetName().Version ?? new Version(0, 0)));

    /// <summary>"v1.0.0-rc.1+abc123" → 1.0.0-rc.1; null si no es una versión (p. ej. "PreRelease").</summary>
    public static AppVersion? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var value = text.Trim().TrimStart('v', 'V');
        var plus = value.IndexOf('+');
        if (plus >= 0)
        {
            value = value[..plus];   // metadatos de compilación: no cuentan
        }

        var dash = value.IndexOf('-');
        var number = dash >= 0 ? value[..dash] : value;
        var preRelease = dash >= 0 ? value[(dash + 1)..] : null;

        return Version.TryParse(number, out var version)
            ? new AppVersion(Normalize(version), string.IsNullOrWhiteSpace(preRelease) ? null : preRelease)
            : null;
    }

    public static AppVersion Parse(string text) =>
        TryParse(text) ?? throw new FormatException($"'{text}' no es una versión.");

    public int CompareTo(AppVersion? other)
    {
        if (other is null) return 1;

        var byNumber = Number.CompareTo(other.Number);
        if (byNumber != 0) return byNumber;
        if (PreRelease is null || other.PreRelease is null)
        {
            return (PreRelease is null).CompareTo(other.PreRelease is null);
        }

        return ComparePreRelease(PreRelease, other.PreRelease);
    }

    public static bool operator >(AppVersion a, AppVersion b) => a.CompareTo(b) > 0;
    public static bool operator <(AppVersion a, AppVersion b) => a.CompareTo(b) < 0;

    public override string ToString() => PreRelease is null ? Number.ToString() : $"{Number}-{PreRelease}";

    /// <summary>Compara siempre con 3 componentes: 0.8 == 0.8.0 == 0.8.0.0.</summary>
    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));

    /// <summary>Como semver: identificadores separados por puntos; los numéricos se comparan como números (rc.2 &lt; rc.10).</summary>
    private static int ComparePreRelease(string a, string b)
    {
        var left = a.Split('.');
        var right = b.Split('.');
        for (var i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            var leftIsNumber = int.TryParse(left[i], out var leftNumber);
            var rightIsNumber = int.TryParse(right[i], out var rightNumber);
            var result = (leftIsNumber, rightIsNumber) switch
            {
                (true, true) => leftNumber.CompareTo(rightNumber),
                (true, false) => -1,
                (false, true) => 1,
                _ => string.CompareOrdinal(left[i], right[i])
            };
            if (result != 0) return result;
        }

        return left.Length.CompareTo(right.Length);
    }
}
