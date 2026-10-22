using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AstroPlanner.UI.ViewModels;

/// <summary>
/// Búsqueda en el catálogo por id, nombre o constelación, sin distinguir
/// mayúsculas ni acentos ("orion" encuentra "Nebulosa de Orión").
/// </summary>
public static partial class CatalogSearch
{
    [GeneratedRegex(@"^m?(\d+)$")]
    private static partial Regex IdQuery();

    public static bool Matches(CelestialObjectRowViewModel item, string? query) =>
        Matches(item.Id, item.CommonName, item.Constellation, query);

    public static bool Matches(string id, string commonName, string constellation, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var normalizedQuery = Normalize(query);

        // "M31", "m 31" o "31" buscan el id exacto (si no, "M1" encontraría M10-M19).
        var idMatch = IdQuery().Match(normalizedQuery.Replace(" ", ""));
        if (idMatch.Success)
        {
            return Normalize(id) == "m" + int.Parse(idMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        return Normalize(commonName).Contains(normalizedQuery, StringComparison.Ordinal)
            || Normalize(constellation).Contains(normalizedQuery, StringComparison.Ordinal);
    }

    /// <summary>Minúsculas, sin acentos y sin espacios sobrantes.</summary>
    public static string Normalize(string text)
    {
        var decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
