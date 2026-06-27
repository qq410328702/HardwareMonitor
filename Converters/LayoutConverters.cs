using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace HardwareMonitor.Converters;

public class MemoryBarWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type t, object p, CultureInfo c)
    {
        if (values.Length < 3) return 0.0;
        foreach (var v in values)
            if (v == null || v == DependencyProperty.UnsetValue || v == Binding.DoNothing)
                return 0.0;
        float used = System.Convert.ToSingle(values[0]);
        float total = System.Convert.ToSingle(values[1]);
        double maxWidth = System.Convert.ToDouble(values[2]);
        if (total <= 0) return 0.0;
        return maxWidth * Math.Clamp(used / total, 0, 1);
    }

    public object[] ConvertBack(object v, Type[] t, object p, CultureInfo c) => throw new NotImplementedException();
}

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

public class EnumEqualConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        if (value == null || p == null)
            return new SolidColorBrush(Colors.Transparent);
        bool isMatch = value.ToString() == p.ToString();
        return isMatch
            ? new SolidColorBrush(Color.FromArgb(0x40, 0x58, 0xA6, 0xFF))
            : new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

public class CardIdToDisplayNameConverter : IValueConverter
{
    private static readonly Dictionary<string, string> DisplayNames = new()
    {
        ["cpu"] = "CPU 温度",
        ["gpu"] = "GPU 温度",
        ["memory"] = "内存使用率",
        ["disk"] = "磁盘监控",
        ["network"] = "网络监控",
        ["process"] = "进程监控",
        ["charts"] = "图表趋势",
        ["history"] = "历史数据",
        ["alert"] = "告警规则"
    };

    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        if (value is string cardId && DisplayNames.TryGetValue(cardId, out var name))
            return name;
        return value?.ToString() ?? "";
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}
