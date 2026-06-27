using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace HardwareMonitor.Converters;

public class TempToColorConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        if (value == null || value == DependencyProperty.UnsetValue || value == Binding.DoNothing)
            return new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E));
        float temp = System.Convert.ToSingle(value);
        if (temp < 50) return new SolidColorBrush(Color.FromRgb(0x3F, 0xB9, 0x50));
        if (temp < 70) return new SolidColorBrush(Color.FromRgb(0xD2, 0x99, 0x22));
        if (temp < 85) return new SolidColorBrush(Color.FromRgb(0xF0, 0x72, 0x3C));
        return new SolidColorBrush(Color.FromRgb(0xF8, 0x51, 0x49));
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

public class TempToWpfColorConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        if (value == null || value == DependencyProperty.UnsetValue || value == Binding.DoNothing)
            return Color.FromRgb(0x8B, 0x94, 0x9E);
        float temp = System.Convert.ToSingle(value);
        if (temp < 50) return Color.FromRgb(0x3F, 0xB9, 0x50);
        if (temp < 70) return Color.FromRgb(0xD2, 0x99, 0x22);
        if (temp < 85) return Color.FromRgb(0xF0, 0x72, 0x3C);
        return Color.FromRgb(0xF8, 0x51, 0x49);
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

public class UsageToColorConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        if (value == null || value == DependencyProperty.UnsetValue || value == Binding.DoNothing)
            return Color.FromRgb(0x8B, 0x94, 0x9E);
        float usage = System.Convert.ToSingle(value);
        if (usage < 50) return Color.FromRgb(0x58, 0xA6, 0xFF);
        if (usage < 80) return Color.FromRgb(0xD2, 0x99, 0x22);
        return Color.FromRgb(0xF8, 0x51, 0x49);
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}
