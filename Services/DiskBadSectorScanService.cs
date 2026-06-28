using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace HardwareMonitor.Services;

internal enum DiskBadSectorScanMode
{
    Quick,
    Full
}

internal sealed class DiskBadSectorFailure
{
    public long Offset { get; init; }
    public int Length { get; init; }
    public string ErrorCode { get; init; } = "";
    public string Message { get; init; } = "";

    public string OffsetDisplay => DiskBadSectorScanService.FormatBytes(Offset);
    public string LengthDisplay => DiskBadSectorScanService.FormatBytes(Length);
}

internal sealed class DiskBadSectorScanProgress
{
    public long BytesRead { get; init; }
    public long PlannedBytes { get; init; }
    public long VolumeBytes { get; init; }
    public long CurrentOffset { get; init; }
    public int FailureCount { get; init; }
    public string Status { get; init; } = "";
    public double Percent => PlannedBytes > 0
        ? Math.Clamp(BytesRead * 100d / PlannedBytes, 0d, 100d)
        : 0d;
}

internal sealed class DiskBadSectorScanResult
{
    public string DriveLetter { get; init; } = "";
    public DiskBadSectorScanMode Mode { get; init; }
    public long VolumeBytes { get; init; }
    public long PlannedBytes { get; init; }
    public long BytesRead { get; init; }
    public TimeSpan Duration { get; init; }
    public bool IsCanceled { get; init; }
    public int FailureCount { get; init; }
    public IReadOnlyList<DiskBadSectorFailure> Failures { get; init; } = Array.Empty<DiskBadSectorFailure>();
}

internal sealed class DiskBadSectorScanService
{
    private const int QuickSampleCount = 96;
    private const int QuickBlockSize = 1024 * 1024;
    private const int FullBlockSize = 4 * 1024 * 1024;
    private const int DefaultSectorSize = 4096;
    private const uint GenericRead = 0x80000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x00000080;
    private const uint FileFlagSequentialScan = 0x08000000;
    private const uint IoctlDiskGetLengthInfo = 0x0007405C;

    private static readonly SemaphoreSlim ScanLock = new(1, 1);

    public async Task<DiskBadSectorScanResult> ScanAsync(
        string driveLetter,
        DiskBadSectorScanMode mode,
        IProgress<DiskBadSectorScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!await ScanLock.WaitAsync(0, cancellationToken))
            throw new InvalidOperationException("已有坏道扫描正在进行，请等待当前扫描结束。");

        try
        {
            return await Task.Run(
                () => ScanCore(driveLetter, mode, progress, cancellationToken),
                cancellationToken);
        }
        finally
        {
            ScanLock.Release();
        }
    }

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = Math.Max(0, bytes);
        int unit = 0;

        while (value >= 1024d && unit < units.Length - 1)
        {
            value /= 1024d;
            unit++;
        }

        return unit == 0 ? $"{value:F0} {units[unit]}" : $"{value:F1} {units[unit]}";
    }

    private static DiskBadSectorScanResult ScanCore(
        string driveLetter,
        DiskBadSectorScanMode mode,
        IProgress<DiskBadSectorScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        string normalizedDrive = NormalizeDriveLetter(driveLetter);
        var failures = new List<DiskBadSectorFailure>();
        var stopwatch = Stopwatch.StartNew();
        long bytesRead = 0;
        long plannedBytes = 0;
        long volumeBytes = 0;
        bool canceled = false;

        progress?.Report(new DiskBadSectorScanProgress
        {
            Status = $"正在打开 {normalizedDrive} 卷"
        });

        using var handle = OpenVolume(normalizedDrive);
        volumeBytes = GetVolumeLength(handle, normalizedDrive);
        if (volumeBytes <= 0)
            throw new IOException($"无法获取 {normalizedDrive} 卷容量。");

        using var stream = new FileStream(handle, FileAccess.Read, FullBlockSize, false);
        var ranges = mode == DiskBadSectorScanMode.Quick
            ? BuildQuickRanges(volumeBytes).ToList()
            : null;
        plannedBytes = ranges?.Sum(r => (long)r.Length) ?? volumeBytes;
        var buffer = new byte[mode == DiskBadSectorScanMode.Quick ? QuickBlockSize : FullBlockSize];

        try
        {
            foreach (var range in EnumerateRanges(mode, volumeBytes, ranges))
            {
                cancellationToken.ThrowIfCancellationRequested();

                ReadRange(stream, range, buffer, cancellationToken, out var failure);
                bytesRead += range.Length;

                if (failure is not null)
                    failures.Add(failure);

                progress?.Report(new DiskBadSectorScanProgress
                {
                    BytesRead = bytesRead,
                    PlannedBytes = plannedBytes,
                    VolumeBytes = volumeBytes,
                    CurrentOffset = range.Offset,
                    FailureCount = failures.Count,
                    Status = failure is null ? "正在只读扫描" : "发现读取错误，已跳过该块"
                });
            }
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }

        stopwatch.Stop();
        return new DiskBadSectorScanResult
        {
            DriveLetter = normalizedDrive,
            Mode = mode,
            VolumeBytes = volumeBytes,
            PlannedBytes = plannedBytes,
            BytesRead = bytesRead,
            Duration = stopwatch.Elapsed,
            IsCanceled = canceled,
            FailureCount = failures.Count,
            Failures = failures
        };
    }

    private static int ReadRange(
        FileStream stream,
        ScanRange range,
        byte[] buffer,
        CancellationToken cancellationToken,
        out DiskBadSectorFailure? failure)
    {
        failure = null;
        int totalRead = 0;

        try
        {
            stream.Seek(range.Offset, SeekOrigin.Begin);
            int remaining = range.Length;

            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int readLength = Math.Min(remaining, buffer.Length);
                int read = stream.Read(buffer, 0, readLength);
                if (read <= 0)
                    break;

                totalRead += read;
                remaining -= read;
            }
        }
        catch (IOException ex)
        {
            failure = CreateFailure(range, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            failure = CreateFailure(range, ex);
        }

        return totalRead;
    }

    private static DiskBadSectorFailure CreateFailure(ScanRange range, Exception ex) => new()
    {
        Offset = range.Offset,
        Length = range.Length,
        ErrorCode = $"0x{ex.HResult:X8}",
        Message = ex is Win32Exception win32 ? win32.Message : ex.Message
    };

    private static IEnumerable<ScanRange> EnumerateRanges(
        DiskBadSectorScanMode mode,
        long volumeBytes,
        IReadOnlyList<ScanRange>? quickRanges)
    {
        if (mode == DiskBadSectorScanMode.Quick)
            return quickRanges ?? Array.Empty<ScanRange>();

        return EnumerateFullRanges(volumeBytes);
    }

    private static IEnumerable<ScanRange> EnumerateFullRanges(long volumeBytes)
    {
        for (long offset = 0; offset < volumeBytes; offset += FullBlockSize)
        {
            int length = (int)Math.Min(FullBlockSize, volumeBytes - offset);
            if (length > 0)
                yield return new ScanRange(offset, length);
        }
    }

    private static IEnumerable<ScanRange> BuildQuickRanges(long volumeBytes)
    {
        if (volumeBytes <= 0)
            yield break;

        int blockLength = (int)Math.Min(QuickBlockSize, volumeBytes);
        long maxOffset = Math.Max(0, volumeBytes - blockLength);
        var offsets = new SortedSet<long> { 0 };

        if (maxOffset > 0)
            offsets.Add(AlignDown(maxOffset, DefaultSectorSize));

        int middleSamples = Math.Max(0, QuickSampleCount - offsets.Count);
        for (int i = 1; i <= middleSamples; i++)
        {
            double ratio = i / (middleSamples + 1d);
            long offset = AlignDown((long)(maxOffset * ratio), DefaultSectorSize);
            offsets.Add(Math.Clamp(offset, 0, maxOffset));
        }

        foreach (long offset in offsets)
        {
            int length = (int)Math.Min(blockLength, volumeBytes - offset);
            if (length > 0)
                yield return new ScanRange(offset, length);
        }
    }

    private static long AlignDown(long value, long alignment) =>
        alignment <= 0 ? value : value / alignment * alignment;

    private static SafeFileHandle OpenVolume(string driveLetter)
    {
        string path = @"\\.\" + driveLetter;
        var handle = CreateFile(
            path,
            GenericRead,
            FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero,
            OpenExisting,
            FileAttributeNormal | FileFlagSequentialScan,
            IntPtr.Zero);

        if (handle.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"无法打开 {driveLetter} 卷，请确认以管理员权限运行。");

        return handle;
    }

    private static long GetVolumeLength(SafeFileHandle handle, string driveLetter)
    {
        if (DeviceIoControl(
                handle,
                IoctlDiskGetLengthInfo,
                IntPtr.Zero,
                0,
                out GetLengthInformation lengthInfo,
                Marshal.SizeOf<GetLengthInformation>(),
                out _,
                IntPtr.Zero) &&
            lengthInfo.Length > 0)
        {
            return lengthInfo.Length;
        }

        try
        {
            return new DriveInfo(driveLetter + @"\").TotalSize;
        }
        catch
        {
            return 0;
        }
    }

    private static string NormalizeDriveLetter(string driveLetter)
    {
        string value = driveLetter.Trim();
        if (value.Length == 1 && char.IsLetter(value[0]))
            return $"{char.ToUpperInvariant(value[0])}:";

        if (value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':')
            return $"{char.ToUpperInvariant(value[0])}:";

        throw new ArgumentException("请选择有效盘符，例如 C:。", nameof(driveLetter));
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        int nInBufferSize,
        out GetLengthInformation lpOutBuffer,
        int nOutBufferSize,
        out int lpBytesReturned,
        IntPtr lpOverlapped);

    [StructLayout(LayoutKind.Sequential)]
    private struct GetLengthInformation
    {
        public long Length;
    }

    private readonly record struct ScanRange(long Offset, int Length);
}
