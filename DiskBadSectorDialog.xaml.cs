using HardwareMonitor.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace HardwareMonitor;

public partial class DiskBadSectorDialog : Window
{
    private readonly DiskSnapshot _disk;
    private readonly DiskBadSectorScanService _scanService = new();
    private CancellationTokenSource? _scanCts;
    private bool _isScanning;

    public DiskBadSectorDialog(DiskSnapshot disk)
    {
        _disk = disk;
        InitializeComponent();
        DataContext = disk;
        Loaded += DiskBadSectorDialog_Loaded;
        Closing += DiskBadSectorDialog_Closing;
    }

    private void DiskBadSectorDialog_Loaded(object sender, RoutedEventArgs e)
    {
        var driveLetters = ParseDriveLetters(_disk.DriveLetters);
        DriveLetterCombo.ItemsSource = driveLetters;
        if (driveLetters.Count > 0)
        {
            DriveLetterCombo.SelectedIndex = 0;
            ScanStatusText.Text = "请选择快速扫描或完整扫描";
        }
        else
        {
            ScanStatusText.Text = "这块硬盘没有可扫描的盘符";
            ScanHintText.Text = "SMART 风险仍可查看；只读扫描需要已挂载盘符。";
        }

        SetScanControls(false);
    }

    private async void QuickScan_Click(object sender, RoutedEventArgs e)
    {
        await StartScanAsync(DiskBadSectorScanMode.Quick);
    }

    private async void FullScan_Click(object sender, RoutedEventArgs e)
    {
        var choice = MessageBox.Show(
            this,
            "完整扫描会顺序读取整个卷，可能持续很久并产生较高磁盘 IO。确认开始？",
            "完整只读扫描",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Information);

        if (choice == MessageBoxResult.OK)
            await StartScanAsync(DiskBadSectorScanMode.Full);
    }

    private void CancelScan_Click(object sender, RoutedEventArgs e)
    {
        _scanCts?.Cancel();
        ScanStatusText.Text = "正在取消扫描...";
        CancelScanButton.IsEnabled = false;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (_isScanning)
        {
            _scanCts?.Cancel();
            return;
        }

        Close();
    }

    private async Task StartScanAsync(DiskBadSectorScanMode mode)
    {
        if (_isScanning)
            return;

        if (DriveLetterCombo.SelectedItem is not string driveLetter)
        {
            ScanStatusText.Text = "请先选择可扫描的盘符";
            return;
        }

        _scanCts = new CancellationTokenSource();
        SetScanControls(true);
        ScanProgressBar.Value = 0;
        FailureCountText.Text = "0";
        FailuresList.ItemsSource = null;

        string modeText = mode == DiskBadSectorScanMode.Quick ? "快速扫描" : "完整扫描";
        ScanStatusText.Text = $"正在执行 {driveLetter} {modeText}";
        ScanProgressText.Text = "正在准备扫描...";

        var progress = new Progress<DiskBadSectorScanProgress>(UpdateProgress);

        try
        {
            var result = await _scanService.ScanAsync(
                driveLetter,
                mode,
                progress,
                _scanCts.Token);

            ShowResult(result);
        }
        catch (OperationCanceledException)
        {
            ScanStatusText.Text = "扫描已取消";
            ScanProgressText.Text = "没有执行修复或写入操作。";
        }
        catch (Exception ex)
        {
            ScanStatusText.Text = "扫描失败";
            ScanProgressText.Text = ex.Message;
        }
        finally
        {
            _scanCts.Dispose();
            _scanCts = null;
            SetScanControls(false);
        }
    }

    private void UpdateProgress(DiskBadSectorScanProgress progress)
    {
        ScanProgressBar.Value = progress.Percent;
        FailureCountText.Text = progress.FailureCount.ToString("N0");
        ScanStatusText.Text = progress.Status;
        ScanProgressText.Text =
            $"已扫描 {DiskBadSectorScanService.FormatBytes(progress.BytesRead)} / " +
            $"{DiskBadSectorScanService.FormatBytes(progress.PlannedBytes)}，" +
            $"卷容量 {DiskBadSectorScanService.FormatBytes(progress.VolumeBytes)}";
    }

    private void ShowResult(DiskBadSectorScanResult result)
    {
        ScanProgressBar.Value = result.IsCanceled ? ScanProgressBar.Value : 100;
        FailureCountText.Text = result.FailureCount.ToString("N0");
        FailuresList.ItemsSource = result.Failures;

        if (result.IsCanceled)
        {
            ScanStatusText.Text = "扫描已取消";
        }
        else if (result.FailureCount == 0)
        {
            ScanStatusText.Text = "扫描完成，未发现读取错误";
        }
        else
        {
            ScanStatusText.Text = $"扫描完成，发现 {result.FailureCount:N0} 个读取错误块";
        }

        ScanProgressText.Text =
            $"已扫描 {DiskBadSectorScanService.FormatBytes(result.BytesRead)} / " +
            $"{DiskBadSectorScanService.FormatBytes(result.PlannedBytes)}，" +
            $"耗时 {FormatDuration(result.Duration)}。";
    }

    private void SetScanControls(bool scanning)
    {
        _isScanning = scanning;
        bool hasDrive = DriveLetterCombo.Items.Count > 0;
        DriveLetterCombo.IsEnabled = !scanning && hasDrive;
        QuickScanButton.IsEnabled = !scanning && hasDrive;
        FullScanButton.IsEnabled = !scanning && hasDrive;
        CancelScanButton.IsEnabled = scanning;
    }

    private void DiskBadSectorDialog_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_isScanning)
            return;

        _scanCts?.Cancel();
        e.Cancel = true;
        ScanStatusText.Text = "正在取消扫描...";
        CancelScanButton.IsEnabled = false;
    }

    private static IReadOnlyList<string> ParseDriveLetters(string driveLetters)
    {
        if (string.IsNullOrWhiteSpace(driveLetters))
            return Array.Empty<string>();

        return Regex.Matches(driveLetters, @"[A-Za-z]:")
            .Select(m => m.Value.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return $"{duration.TotalHours:F1} 小时";
        if (duration.TotalMinutes >= 1)
            return $"{duration.TotalMinutes:F1} 分钟";
        return $"{duration.TotalSeconds:F1} 秒";
    }
}
