using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AstroPlanner.UI.Converters;

/// <summary>
/// Como el BooleanToVisibilityConverter de WPF, pero con <see cref="Invert"/>
/// para ocultar un elemento cuando el valor es true.
/// </summary>
public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        (value is true) != Invert ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
