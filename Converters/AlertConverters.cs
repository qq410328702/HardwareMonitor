using HardwareMonitor.Services;
using System;
using System.Globalization;
using System.Windows.Data;

namespace HardwareMonitor.Converters;

public class MetricTypeDisplayConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        if (value is MetricType metric)
        {
            return metric switch
            {
                MetricType.CpuTemp => "CPU 温度",
                MetricType.GpuTemp => "GPU 温度",
                MetricType.CpuUsage => "CPU 使用率",
                MetricType.GpuUsage => "GPU 使用率",
                _ => metric.ToString()
            };
        }
        return value?.ToString() ?? "";
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

public class CompareDirectionDisplayConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        if (value is CompareDirection dir)
        {
            return dir switch
            {
                CompareDirection.Above => "高于",
                CompareDirection.Below => "低于",
                _ => dir.ToString()
            };
        }
        return value?.ToString() ?? "";
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

public class MetricTypeUnitConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        if (value is MetricType metric)
        {
            return metric is MetricType.CpuTemp or MetricType.GpuTemp ? "°C" : "%";
        }
        return "";
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}
