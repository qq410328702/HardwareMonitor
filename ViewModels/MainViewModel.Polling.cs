using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

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
                var s = await Task.Run(() => _hw.GetSnapshot(), ct);
                RunOnUI(() =>
                {
                    ApplySnapshot(s);
                    EvaluateAlerts(s);
                });

                if (_dataStorageService is not null)
                {
                    try
                    {
                        await _dataStorageService.SaveSnapshotAsync(s);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
            }

            if (_diskService is not null)
            {
                try
                {
                    var disks = await Task.Run(() => _diskService.GetDiskSnapshots(), ct);
                    RunOnUI(() => ApplyDiskSnapshots(disks));
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception)
                {
                }
            }

            if (_networkService is not null)
            {
                try
                {
                    var nets = await Task.Run(() => _networkService.GetNetworkSnapshots(), ct);
                    RunOnUI(() => ApplyNetworkSnapshots(nets));
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception)
                {
                }
            }

            if (_processService is not null)
            {
                try
                {
                    var sortMode = _processSortMode;
                    var procs = await Task.Run(() => _processService.GetTopProcesses(10, sortMode), ct);
                    RunOnUI(() => ApplyTopProcesses(procs));
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception)
                {
                }
            }

            try
            {
                await Task.Delay(PollingIntervalMs, ct);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    private static void RunOnUI(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null)
            dispatcher.BeginInvoke(action);
        else
            action();
    }
}
