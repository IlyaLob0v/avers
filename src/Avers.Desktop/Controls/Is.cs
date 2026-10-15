using System.Globalization;
using Avalonia.Data.Converters;

namespace Avers.Desktop.Controls;

/// <summary>
/// Сравнение привязанного значения с параметром:
/// <c>IsVisible="{Binding Module, Converter={x:Static ctl:Is.Equal}, ConverterParameter={x:Static d:AppModule.Abon}}"</c>.
/// </summary>
public sealed class Is : IValueConverter
{
    public static readonly Is Equal = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => Equals(value, parameter);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
