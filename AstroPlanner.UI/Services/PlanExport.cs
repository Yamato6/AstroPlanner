using System.Globalization;
using AstroPlanner.UI.ViewModels;

namespace AstroPlanner.UI.Services;

/// <summary>Exporta "Mi plan" a CSV (ver <see cref="CsvBuilder"/>).</summary>
public static class PlanExport
{
    public static string BuildCsv(IEnumerable<PlanItemViewModel> items, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(culture);

        var csv = new CsvBuilder(culture).AddRow(["Orden", "ID", "Nombre", "Constelación", "Tipo", "Magnitud",
            "Culminación", "Altitud máxima (°)", "Distancia a la Luna (°)", "Luna estorba", "Notas"]);

        var order = 1;
        foreach (var item in items)
        {
            var row = item.Row;
            csv.AddRow([
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

        return csv.ToString();
    }
}
