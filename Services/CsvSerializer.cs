using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HardwareMonitor.Services;

public static class CsvSerializer
{
    private const string Header = "Timestamp,CpuTemp,CpuUsage,GpuTemp,GpuUsage,MemUsage,TotalPower";

    public static string Serialize(IEnumerable<SnapshotRecord> records)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Header);

        foreach (var r in records)
        {
            sb.Append(r.Timestamp.ToString("o", CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(r.CpuTemp.ToString("G9", CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(r.CpuUsage.ToString("G9", CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(r.GpuTemp.ToString("G9", CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(r.GpuUsage.ToString("G9", CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(r.MemUsage.ToString("G9", CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.AppendLine(r.TotalPower.ToString("G9", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    public static List<SnapshotRecord> Deserialize(string csv)
    {
        var records = new List<SnapshotRecord>();
        if (string.IsNullOrWhiteSpace(csv))
            return records;

        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        // Skip header line, process data lines
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = line.Split(',');
            if (fields.Length != 7)
                continue;

            records.Add(new SnapshotRecord
            {
                Timestamp = DateTime.Parse(fields[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                CpuTemp = float.Parse(fields[1], CultureInfo.InvariantCulture),
                CpuUsage = float.Parse(fields[2], CultureInfo.InvariantCulture),
                GpuTemp = float.Parse(fields[3], CultureInfo.InvariantCulture),
                GpuUsage = float.Parse(fields[4], CultureInfo.InvariantCulture),
                MemUsage = float.Parse(fields[5], CultureInfo.InvariantCulture),
                TotalPower = float.Parse(fields[6], CultureInfo.InvariantCulture)
            });
        }

        return records;
    }
}
