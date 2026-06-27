using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using HardwareMonitor.Services;

namespace HardwareMonitor.ViewModels;

public partial class MainViewModel
{
    private async Task StartAsync(CancellationToken ct)
    {
        try
        {
            await _hw.InitAsync();

            if (_hw.InitError is not null)
            {
                RunOnUI(() =>
                {
                    IsLoading = false;
                    IsError = true;
                    ErrorMessage = $"硬件初始化失败: {_hw.InitError}";
                });
                return;
            }

            RunOnUI(() => IsLoading = false);
            await PollAsync(ct);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            RunOnUI(() =>
            {
                IsLoading = false;
                IsError = true;
                ErrorMessage = $"监控异常: {ex.Message}";
            });
        }
    }

    private async Task PollAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var frame = await Task.Run(CaptureFrame, ct);
                var uiFrame = frame;
                RunOnUI(() =>
                {
                    ApplyFrame(uiFrame);
                });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
            }

            try
            {
                await Task.Delay(PollingIntervalMs, ct);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    private MonitoringFrame CaptureFrame()
    {
        var frame = new MonitoringFrame();

        try
        {
            frame.HardwareSnapshot = _hw.GetSnapshot();
        }
        catch (Exception)
        {
        }

        if (_diskService is not null)
        {
            try
            {
                frame.DiskSnapshots = _diskService.GetDiskSnapshots();
            }
            catch (Exception)
            {
            }
        }

        return frame;
    }

    private void ApplyFrame(MonitoringFrame frame)
    {
        if (frame.HardwareSnapshot is not null)
        {
            ApplySnapshot(frame.HardwareSnapshot);
        }

        if (frame.DiskSnapshots is not null)
            ApplyDiskSnapshots(frame.DiskSnapshots);
    }

    private static void RunOnUI(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null)
            dispatcher.BeginInvoke(action);
        else
            action();
    }

    private sealed class MonitoringFrame
    {
        public HardwareSnapshot? HardwareSnapshot { get; set; }
        public List<DiskSnapshot>? DiskSnapshots { get; set; }
    }
}
