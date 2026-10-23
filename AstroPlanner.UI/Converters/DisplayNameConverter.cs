using System.Globalization;
using System.Windows.Data;
using AstroPlanner.Core.Models;
using AstroPlanner.UI.Services;

namespace AstroPlanner.UI.Converters;

/// <summary>Muestra en español el tipo de un objeto (ver <see cref="DisplayNames"/>).</summary>
public sealed class DisplayNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is CelestialObjectType type ? DisplayNames.Of(type) : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
