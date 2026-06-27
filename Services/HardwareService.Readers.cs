using LibreHardwareMonitor.Hardware;
using System;

namespace HardwareMonitor.Services;

public partial class HardwareService
{
    private static void ReadCpu(IHardware hw, HardwareSnapshot snap)
    {
        snap.CpuName = hw.Name;

        float powerPackage = 0, powerCores = 0, powerAny = 0;

        foreach (var sensor in hw.Sensors)
        {
            if (sensor.SensorType == SensorType.Temperature)
            {
                if (sensor.Name.Contains("Package") || sensor.Name.Contains("Tctl") || sensor.Name.Contains("Tdie"))
                    snap.CpuTemp = sensor.Value ?? 0;
                else if (snap.CpuTemp == 0)
                    snap.CpuTemp = sensor.Value ?? 0;
            }

            if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Total"))
                snap.CpuUsage = sensor.Value ?? 0;

            if (sensor.SensorType == SensorType.Power)
            {
                float v = sensor.Value ?? 0;
                if (v > 0)
                {
                    if (sensor.Name.Contains("Package"))
                        powerPackage = v;
                    else if (sensor.Name.Contains("Cores"))
                        powerCores = v;
                    else if (powerAny == 0)
                        powerAny = v;
                }
            }

            if (sensor.SensorType == SensorType.Clock && sensor.Name.Contains("Core #1"))
                snap.CpuClock = sensor.Value ?? 0;
        }

        snap.CpuPower = powerPackage > 0 ? powerPackage
                       : powerCores > 0 ? powerCores
                       : powerAny;
    }

    private static void ReadGpu(IHardware hw, HardwareSnapshot snap)
    {
        snap.GpuName = hw.Name;

        float powerBoard = 0, powerGpu = 0, powerAny = 0;

        foreach (var sensor in hw.Sensors)
        {
            if (sensor.SensorType == SensorType.Temperature)
            {
                if (sensor.Name.Contains("Core") || sensor.Name.Contains("GPU"))
                    snap.GpuTemp = sensor.Value ?? 0;
                else if (snap.GpuTemp == 0)
                    snap.GpuTemp = sensor.Value ?? 0;
            }

            if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Core"))
                snap.GpuUsage = sensor.Value ?? 0;

            if (sensor.SensorType == SensorType.Power)
            {
                float v = sensor.Value ?? 0;
                string name = sensor.Name;
                if (v > 0)
                {
                    if (name.Contains("Board") || name.Contains("TDP") || name.Contains("Total"))
                        powerBoard = Math.Max(powerBoard, v);
                    else if (name.Contains("GPU") || name.Contains("Core"))
                        powerGpu = Math.Max(powerGpu, v);
                    else
                        powerAny = Math.Max(powerAny, v);
                }
            }

            if (sensor.SensorType == SensorType.Clock && sensor.Name.Contains("Core"))
                snap.GpuClock = sensor.Value ?? 0;
            if (sensor.SensorType == SensorType.SmallData && sensor.Name.Contains("Memory Used"))
                snap.GpuMemUsed = sensor.Value ?? 0;
            if (sensor.SensorType == SensorType.SmallData && sensor.Name.Contains("Memory Total"))
                snap.GpuMemTotal = sensor.Value ?? 0;
        }

        snap.GpuPower = powerBoard > 0 ? powerBoard
                       : powerGpu > 0 ? powerGpu
                       : powerAny;
    }

    private static void ReadMemory(IHardware hw, HardwareSnapshot snap)
    {
        foreach (var sensor in hw.Sensors)
        {
            if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Memory"))
                snap.MemUsage = sensor.Value ?? 0;
            if (sensor.SensorType == SensorType.Data && sensor.Name.Contains("Used"))
                snap.MemUsed = sensor.Value ?? 0;
            if (sensor.SensorType == SensorType.Data && sensor.Name.Contains("Available"))
                snap.MemAvailable = sensor.Value ?? 0;
        }
    }
}
