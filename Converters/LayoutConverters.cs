using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HardwareMonitor.Converters;

public class RatioToWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2
            && values[0] is float ratio
            && values[1] is double parentWidth
            && parentWidth > 0)
        {
            return parentWidth * Math.Clamp(ratio / 100f, 0f, 1f);
        }
        return 0.0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
