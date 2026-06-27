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

public class BytesToStringConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        if (value == null || value == DependencyProperty.UnsetValue || value == Binding.DoNothing)
            return "0 B";
        long bytes = System.Convert.ToInt64(value);
        if (bytes < 1024L) return $"{bytes} B";
        if (bytes < 1024L * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}
