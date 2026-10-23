using System.Globalization;
using System.Windows.Data;

namespace AstroPlanner.UI.Converters;

/// <summary>Muestra <see cref="TrueText"/> o <see cref="FalseText"/> según un booleano.</summary>
public sealed class BooleanToTextConverter : IValueConverter
{
    public string TrueText { get; set; } = "";
    public string FalseText { get; set; } = "";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? TrueText : FalseText;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
