using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

namespace HardwareMonitor.Services;

public class OperationResult
{
    public bool Success { get; }
    public string Message { get; }

    private OperationResult(bool success, string message)
    {
        Success = success;
        Message = message;
    }

    public static OperationResult Ok(string message = "") => new(true, message);
    public static OperationResult Failure(string message) => new(false, message);
}

public interface ITrayService : IDisposable
{
    void Initialize();
    void ShowTrayIcon();
    void HideTrayIcon();
    bool IsAutoStartEnabled { get; }
    OperationResult SetAutoStart(bool enable);
    void ShowBalloonTip(string title, string text, ToolTipIcon icon = ToolTipIcon.Warning);
    event EventHandler? ShowMainRequested;
    event EventHandler? ShowMiniRequested;
    event EventHandler? CheckUpdateRequested;
    event EventHandler? ExitRequested;
}

public class TrayService : ITrayService
{
    private const string RegistryRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "HardwareMonitor";
    private const string ExeName = "HardwareMonitor.exe";

    private NotifyIcon? _notifyIcon;
    private ToolStripMenuItem? _autoStartItem;
    private Icon? _trayIcon;
    private readonly ILogger? _logger;
    private bool _disposed;

    public event EventHandler? ShowMainRequested;
    public event EventHandler? ShowMiniRequested;
    public event EventHandler? CheckUpdateRequested;
    public event EventHandler? ExitRequested;

    public TrayService(ILogger? logger = null)
    {
        _logger = logger;
    }

    public void Initialize()
    {
        try
        {
            _trayIcon = LoadTrayIcon();
            _notifyIcon = new NotifyIcon
            {
                Text = "HardwareMonitor",
                Icon = _trayIcon,
                ContextMenuStrip = CreateContextMenu()
            };

            _notifyIcon.DoubleClick += (_, _) =>
                ShowMainRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger?.Error("Failed to create tray icon", ex);
        }
    }

    private Icon LoadTrayIcon()
    {
        try
        {
            var resource = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Resources/AppIcon.ico", UriKind.Absolute));

            if (resource?.Stream != null)
            {
                using var stream = resource.Stream;
                using var icon = new Icon(stream);
                return (Icon)icon.Clone();
            }

            _logger?.Warn("App icon resource not found, using system tray fallback icon.");
        }
        catch (Exception ex)
        {
            _logger?.Warn($"Failed to load app tray icon: {ex.Message}");
        }

        return (Icon)SystemIcons.Application.Clone();
    }

    public void ShowTrayIcon()
    {
        if (_notifyIcon != null)
            _notifyIcon.Visible = true;
    }

    public void HideTrayIcon()
    {
        if (_notifyIcon != null)
            _notifyIcon.Visible = false;
    }

    public void ShowBalloonTip(string title, string text, ToolTipIcon icon = ToolTipIcon.Warning)
    {
        try
        {
            _notifyIcon?.ShowBalloonTip(3000, title, text, icon);
        }
        catch (Exception ex)
        {
            _logger?.Error("Failed to show balloon tip", ex);
        }
    }

    public bool IsAutoStartEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, false);
                var command = key?.GetValue(AppName)?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(command))
                    return false;

                var exePath = TryExtractExecutablePath(command);
                return !string.IsNullOrWhiteSpace(exePath) &&
                    File.Exists(exePath) &&
                    !IsUnstablePath(exePath);
            }
            catch (Exception ex)
            {
                _logger?.Error("Failed to read auto-start registry key", ex);
                return false;
            }
        }
    }

    public OperationResult SetAutoStart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, true);
            if (key == null)
                return OperationResult.Failure("无法打开注册表 Run 键");

            if (enable)
            {
                var exePath = ResolveAutoStartExecutablePath();
                key.SetValue(AppName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(AppName, false);
            }

            return OperationResult.Ok();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger?.Error("权限不足，无法修改开机自启设置", ex);
            return OperationResult.Failure("权限不足，无法修改开机自启设置");
        }
        catch (Exception ex)
        {
            _logger?.Error("修改开机自启设置失败", ex);
            return OperationResult.Failure($"修改开机自启设置失败: {ex.Message}");
        }
    }

    private static string ResolveAutoStartExecutablePath()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath) || !File.Exists(processPath))
            throw new InvalidOperationException("无法确定当前程序路径");

        if (!IsUnstablePath(processPath))
            return processPath;

        return InstallCurrentAppToStableDirectory(processPath);
    }

    private static string InstallCurrentAppToStableDirectory(string processPath)
    {
        var sourceDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!Directory.Exists(sourceDirectory))
            throw new DirectoryNotFoundException($"当前程序目录不存在: {sourceDirectory}");

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var hardwareMonitorRoot = Path.Combine(localAppData, "HardwareMonitor");
        var targetDirectory = Path.Combine(hardwareMonitorRoot, "App");
        var stagingDirectory = Path.Combine(hardwareMonitorRoot, "App.staging");
        var backupDirectory = Path.Combine(hardwareMonitorRoot, "App.previous");

        Directory.CreateDirectory(hardwareMonitorRoot);
        RecreateDirectory(stagingDirectory);
        CopyDirectory(sourceDirectory, stagingDirectory, overwrite: true);

        var stagedExe = Path.Combine(stagingDirectory, ExeName);
        if (!File.Exists(stagedExe))
        {
            var fallbackExe = Path.Combine(stagingDirectory, Path.GetFileName(processPath));
            if (File.Exists(fallbackExe))
                stagedExe = fallbackExe;
            else
                throw new FileNotFoundException("稳定自启目录中未找到 HardwareMonitor.exe", stagedExe);
        }

        ReplaceDirectory(stagingDirectory, targetDirectory, backupDirectory);
        return Path.Combine(targetDirectory, Path.GetFileName(stagedExe));
    }

    private static bool IsUnstablePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return true;

        var fullPath = Path.GetFullPath(path);
        var tempPath = Path.GetFullPath(Path.GetTempPath());
        if (IsSubPathOf(fullPath, tempPath))
            return true;

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var stableAppPath = Path.Combine(localAppData, "HardwareMonitor", "App");
        if (IsSubPathOf(fullPath, stableAppPath))
            return false;

        var directory = Path.GetDirectoryName(fullPath) ?? "";
        var segments = directory
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToArray();

        return segments.Any(v =>
            string.Equals(v, "bin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(v, "obj", StringComparison.OrdinalIgnoreCase) ||
            v.StartsWith("HardwareMonitor-run-", StringComparison.OrdinalIgnoreCase) ||
            v.StartsWith("HardwareMonitor-build-", StringComparison.OrdinalIgnoreCase));
    }

    private static string TryExtractExecutablePath(string command)
    {
        command = command.Trim();
        if (command.Length == 0)
            return "";

        if (command[0] == '"')
        {
            var endQuote = command.IndexOf('"', 1);
            return endQuote > 1 ? command[1..endQuote] : "";
        }

        var exeIndex = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIndex >= 0)
            return command[..(exeIndex + 4)].Trim();

        var firstSpace = command.IndexOf(' ');
        return firstSpace > 0 ? command[..firstSpace].Trim() : command;
    }

    private static void RecreateDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, true);

        Directory.CreateDirectory(path);
    }

    private static void ReplaceDirectory(string sourceDirectory, string targetDirectory, string backupDirectory)
    {
        if (Directory.Exists(backupDirectory))
            Directory.Delete(backupDirectory, true);

        if (Directory.Exists(targetDirectory))
            Directory.Move(targetDirectory, backupDirectory);

        try
        {
            Directory.Move(sourceDirectory, targetDirectory);
            if (Directory.Exists(backupDirectory))
                Directory.Delete(backupDirectory, true);
        }
        catch
        {
            if (!Directory.Exists(targetDirectory) && Directory.Exists(backupDirectory))
                Directory.Move(backupDirectory, targetDirectory);
            throw;
        }
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory, bool overwrite)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relative));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, file);
            var destination = Path.Combine(targetDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite);
        }
    }

    private static bool IsSubPathOf(string path, string basePath)
    {
        var normalizedPath = Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedBase = Path.GetFullPath(basePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        return normalizedPath.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase);
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();

        var showMainItem = new ToolStripMenuItem("显示主窗口");
        showMainItem.Click += (_, _) =>
            ShowMainRequested?.Invoke(this, EventArgs.Empty);

        var showMiniItem = new ToolStripMenuItem("显示迷你窗口");
        showMiniItem.Click += (_, _) =>
            ShowMiniRequested?.Invoke(this, EventArgs.Empty);

        var checkUpdateItem = new ToolStripMenuItem("检查更新");
        checkUpdateItem.Click += (_, _) =>
            CheckUpdateRequested?.Invoke(this, EventArgs.Empty);

        _autoStartItem = new ToolStripMenuItem("开机自启")
        {
            CheckOnClick = true,
            Checked = IsAutoStartEnabled
        };
        _autoStartItem.Click += (_, _) =>
        {
            var result = SetAutoStart(_autoStartItem.Checked);
            if (!result.Success)
            {
                _autoStartItem.Checked = !_autoStartItem.Checked;
                MessageBox.Show(result.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };

        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += (_, _) =>
            ExitRequested?.Invoke(this, EventArgs.Empty);

        menu.Items.Add(showMainItem);
        menu.Items.Add(showMiniItem);
        menu.Items.Add(checkUpdateItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_autoStartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        return menu;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _trayIcon?.Dispose();
        _trayIcon = null;
    }
}
