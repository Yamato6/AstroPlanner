using System.Globalization;
using System.Text;

namespace AstroPlanner.UI.Services;

/// <summary>
/// Construye CSV con el separador de listas de la cultura indicada (la del
/// sistema al exportar), para que Excel lo abra en columnas sin pasos extra.
/// </summary>
internal sealed class CsvBuilder(CultureInfo culture)
{
    private readonly StringBuilder _builder = new();
    private readonly string _separator = culture.TextInfo.ListSeparator;

    public CultureInfo Culture { get; } = culture;

    public CsvBuilder AddRow(IEnumerable<string> fields)
    {
        _builder.AppendLine(string.Join(_separator, fields.Select(Escape)));
        return this;
    }

    public override string ToString() => _builder.ToString();

    /// <summary>Entrecomilla los campos con separador, comillas o saltos de línea (RFC 4180).</summary>
    private string Escape(string field)
    {
        if (field.Contains(_separator, StringComparison.Ordinal)
            || field.Contains('"')
            || field.Contains('\n')
            || field.Contains('\r'))
        {
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }

        return field;
    }
}
