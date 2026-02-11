using System;
using System.Drawing;
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
    event EventHandler? ExitRequested;
}

public class TrayService : ITrayService
{
    private const string RegistryRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "HardwareMonitor";

    private NotifyIcon? _notifyIcon;
    private ToolStripMenuItem? _autoStartItem;
    private readonly ILogger? _logger;
    private bool _disposed;

    public event EventHandler? ShowMainRequested;
    public event EventHandler? ShowMiniRequested;
    public event EventHandler? ExitRequested;

    public TrayService(ILogger? logger = null)
    {
        _logger = logger;
    }

    public void Initialize()
    {
        try
        {
            _notifyIcon = new NotifyIcon
            {
                Text = "HardwareMonitor",
                Icon = SystemIcons.Application,
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
                return key?.GetValue(AppName) != null;
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
                var exePath = Environment.ProcessPath ?? "";
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

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();

        var showMainItem = new ToolStripMenuItem("显示主窗口");
        showMainItem.Click += (_, _) =>
            ShowMainRequested?.Invoke(this, EventArgs.Empty);

        var showMiniItem = new ToolStripMenuItem("显示迷你窗口");
        showMiniItem.Click += (_, _) =>
            ShowMiniRequested?.Invoke(this, EventArgs.Empty);

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
    }
}
