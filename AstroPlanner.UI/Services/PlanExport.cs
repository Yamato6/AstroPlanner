using System.Globalization;
using System.Text;
using AstroPlanner.UI.ViewModels;

namespace AstroPlanner.UI.Services;

/// <summary>
/// Exporta "Mi plan" a CSV. Usa el separador de listas y el formato numérico
/// de la cultura indicada (la del sistema al exportar), para que Excel lo abra
/// en columnas sin pasos extra.
/// </summary>
public static class PlanExport
{
    public static string BuildCsv(IEnumerable<PlanItemViewModel> items, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(culture);

        var separator = culture.TextInfo.ListSeparator;
        var builder = new StringBuilder();

        void AppendRow(IEnumerable<string> fields) =>
            builder.AppendLine(string.Join(separator, fields.Select(field => Escape(field, separator))));

        AppendRow(["Orden", "ID", "Nombre", "Constelación", "Tipo", "Magnitud", "Culminación",
            "Altitud máxima (°)", "Distancia a la Luna (°)", "Luna estorba", "Notas"]);

        var order = 1;
        foreach (var item in items)
        {
            var row = item.Row;
            AppendRow([
                order++.ToString(culture),
                row.Id,
                row.CommonName,
                row.Constellation,
                row.Type.ToString(),
                row.ApparentMagnitude.ToString("0.0", culture),
                item.CulminationLocal?.ToString("HH:mm", culture) ?? "",
                item.MaximumAltitudeDegrees.ToString("0", culture),
                row.MoonSeparationDegrees.ToString("0", culture),
                row.MoonInterferes ? "Sí" : "No",
                item.Note
            ]);
        }

        return builder.ToString();
    }

    /// <summary>Entrecomilla los campos con separador, comillas o saltos de línea (RFC 4180).</summary>
    private static string Escape(string field, string separator)
    {
        if (field.Contains(separator, StringComparison.Ordinal)
            || field.Contains('"')
            || field.Contains('\n')
            || field.Contains('\r'))
        {
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }

        return field;
    }
}
