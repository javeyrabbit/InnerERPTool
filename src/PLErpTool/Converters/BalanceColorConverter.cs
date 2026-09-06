using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PLErpTool.Converters;

public sealed class BalanceColorConverter : IValueConverter
{
    private static readonly Brush Negative = new SolidColorBrush(Color.FromRgb(0xFF, 0x4D, 0x4F));
    private static readonly Brush Positive = new SolidColorBrush(Color.FromRgb(0x10, 0x7C, 0x41));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is decimal d && d < 0m ? Negative : Positive;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
