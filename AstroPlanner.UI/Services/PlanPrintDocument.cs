using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using AstroPlanner.UI.ViewModels;

namespace AstroPlanner.UI.Services;

/// <summary>
/// Documento imprimible de "Mi plan": título, resumen de la noche y una tabla
/// en el orden de observación. Negro sobre blanco para que se lea bien en papel
/// (y con luz roja en el campo).
/// </summary>
public static class PlanPrintDocument
{
    private static readonly string[] Headers =
        ["#", "Culminación", "ID", "Nombre", "Constelación", "Mag.", "Alt. máx.", "Luna", "Notas"];

    private static readonly double[] ColumnWidths = [28, 74, 46, 150, 92, 40, 56, 44, 180];

    public static FlowDocument Create(IReadOnlyList<PlanItemViewModel> items, string summary)
    {
        ArgumentNullException.ThrowIfNull(items);

        var document = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11,
            PagePadding = new Thickness(48),
            ColumnWidth = double.PositiveInfinity,
            Background = Brushes.White,
            Foreground = Brushes.Black
        };

        document.Blocks.Add(new Paragraph(new Run("AstroPlanner — Mi plan"))
        {
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        });
        document.Blocks.Add(new Paragraph(new Run(summary))
        {
            Foreground = Brushes.DimGray,
            Margin = new Thickness(0, 0, 0, 16)
        });

        var table = new Table { CellSpacing = 0, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0, 0, 0, 1) };
        foreach (var width in ColumnWidths)
        {
            table.Columns.Add(new TableColumn { Width = new GridLength(width) });
        }

        var group = new TableRowGroup();
        group.Rows.Add(CreateRow(Headers, isHeader: true));

        var order = 1;
        foreach (var item in items)
        {
            var row = item.Row;
            group.Rows.Add(CreateRow(
            [
                order++.ToString(),
                item.CulminationLocal?.ToString("HH:mm") ?? "—",
                row.Id,
                row.CommonName,
                row.Constellation,
                row.ApparentMagnitude.ToString("0.0"),
                $"{item.MaximumAltitudeDegrees:0}°",
                row.MoonInterferes ? $"{row.MoonSeparationDegrees:0}° !" : $"{row.MoonSeparationDegrees:0}°",
                item.Note
            ], isHeader: false));
        }

        table.RowGroups.Add(group);
        document.Blocks.Add(table);

        if (items.Any(item => item.Row.MoonInterferes))
        {
            document.Blocks.Add(new Paragraph(new Run("! La Luna, brillante y cercana, dificulta la observación."))
            {
                FontSize = 9,
                Foreground = Brushes.DimGray,
                Margin = new Thickness(0, 8, 0, 0)
            });
        }

        return document;
    }

    private static TableRow CreateRow(IEnumerable<string> values, bool isHeader)
    {
        var row = new TableRow();
        foreach (var value in values)
        {
            row.Cells.Add(new TableCell(new Paragraph(new Run(value)) { Margin = new Thickness(0) })
            {
                Padding = new Thickness(4, 3, 4, 3),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 0, 1),
                FontWeight = isHeader ? FontWeights.SemiBold : FontWeights.Normal,
                Background = isHeader ? new SolidColorBrush(Color.FromRgb(235, 238, 243)) : Brushes.White
            });
        }

        return row;
    }
}
