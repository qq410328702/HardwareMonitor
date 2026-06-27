using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HardwareMonitor.Converters;

public class FloatFormatConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        if (value == null || value == DependencyProperty.UnsetValue || value == Binding.DoNothing)
            return "0";
        string fmt = p as string ?? "F1";
        return System.Convert.ToSingle(value).ToString(fmt);
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}
