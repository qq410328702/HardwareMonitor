using HardwareMonitor.Services;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace HardwareMonitor.Converters;

public class DiskHealthStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        if (value is DiskHealthStatus status)
        {
            return status switch
            {
                DiskHealthStatus.Healthy => new SolidColorBrush(Color.FromRgb(0x3F, 0xB9, 0x50)),
                DiskHealthStatus.Warning => new SolidColorBrush(Color.FromRgb(0xD2, 0x99, 0x22)),
                DiskHealthStatus.Critical => new SolidColorBrush(Color.FromRgb(0xF8, 0x51, 0x49)),
                _ => new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E))
            };
        }
        return new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E));
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}
