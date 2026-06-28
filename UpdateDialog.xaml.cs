using HardwareMonitor.Services;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace HardwareMonitor;

public partial class UpdateDialog : Window
{
    private readonly UpdateService _updateService;
    private readonly bool _checkOnLoaded;
    private UpdateInfo? _info;
    private bool _isBusy;

    public UpdateDialog(UpdateService updateService, UpdateInfo? info, bool checkOnLoaded)
    {
        _updateService = updateService;
        _info = info;
        _checkOnLoaded = checkOnLoaded;

        InitializeComponent();
        Loaded += UpdateDialog_Loaded;

        if (_info is not null)
            ShowUpdateInfo(_info);
        else
            ShowChecking();
    }

    private async void UpdateDialog_Loaded(object sender, RoutedEventArgs e)
    {
        if (_checkOnLoaded)
            await CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            ShowChecking();
            var info = await _updateService.CheckForUpdatesAsync();
            _info = info;
            ShowUpdateInfo(info);
        }
        catch (Exception ex)
        {
            ShowError(ex is UpdateException ? ex.Message : "检查更新失败，请稍后重试");
        }
    }

    private void ShowChecking()
    {
        _isBusy = true;
        SetIcon(CheckingIcon);
        TitleText.Text = "正在检查更新";
        StatusText.Text = "正在连接 GitHub Releases...";
        CurrentVersionText.Text = "--";
        LatestVersionText.Text = "--";
        ReleaseNotesText.Text = "正在获取更新说明...";
        ProgressBar.Visibility = Visibility.Visible;
        ProgressBar.IsIndeterminate = true;
        InstallButton.IsEnabled = false;
        ReleaseButton.IsEnabled = false;
    }

    private void ShowUpdateInfo(UpdateInfo info)
    {
        _isBusy = false;
        ProgressBar.Visibility = Visibility.Collapsed;
        CurrentVersionText.Text = info.CurrentVersionText;
        LatestVersionText.Text = info.LatestVersionText;
        ReleaseButton.IsEnabled = !string.IsNullOrWhiteSpace(info.ReleaseUrl);
        ReleaseNotesText.Text = TrimReleaseNotes(info.ReleaseNotes);

        if (info.IsNewer)
        {
            SetIcon(UpdateAvailableIcon);
            TitleText.Text = "发现新版本";
            StatusText.Text = info.CanAutoInstall
                ? $"可以自动更新到 {info.TagName}"
                : info.AutoInstallUnavailableReason;
            InstallButton.IsEnabled = info.CanAutoInstall;
            return;
        }

        SetIcon(LatestIcon);
        TitleText.Text = "已经是最新版本";
        StatusText.Text = "当前版本已和 GitHub 最新正式版一致。";
        InstallButton.IsEnabled = false;
    }

    private void ShowError(string message)
    {
        _isBusy = false;
        SetIcon(ErrorIcon);
        ProgressBar.Visibility = Visibility.Collapsed;
        TitleText.Text = "检查更新失败";
        StatusText.Text = message;
        ReleaseNotesText.Text = "请确认网络连接可访问 GitHub，或稍后再试。";
        InstallButton.IsEnabled = false;
        ReleaseButton.IsEnabled = _info is not null && !string.IsNullOrWhiteSpace(_info.ReleaseUrl);
    }

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        if (_info is null || _isBusy)
            return;

        try
        {
            _isBusy = true;
            InstallButton.IsEnabled = false;
            ReleaseButton.IsEnabled = false;
            CloseButton.IsEnabled = false;
            SetIcon(UpdateAvailableIcon);
            ProgressBar.Visibility = Visibility.Visible;
            ProgressBar.IsIndeterminate = true;

            var progress = new Progress<string>(message => StatusText.Text = message);
            var result = await _updateService.DownloadAndPrepareUpdateAsync(_info, progress);

            StatusText.Text = "更新已准备完成，确认后将退出并安装。";
            var choice = MessageBox.Show(
                this,
                "更新包已下载并校验完成。点击“确定”后应用会退出、安装更新并重启。",
                "准备安装更新",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information);

            if (choice == MessageBoxResult.OK)
            {
                _updateService.StartUpdater(result);
                Application.Current.Shutdown();
                return;
            }

            CloseButton.IsEnabled = true;
            ShowUpdateInfo(_info);
        }
        catch (Exception ex)
        {
            CloseButton.IsEnabled = true;
            ShowError(ex is UpdateException ? ex.Message : "自动更新失败，请打开发布页手动下载");
        }
    }

    private void ReleaseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_info is null || string.IsNullOrWhiteSpace(_info.ReleaseUrl))
            return;

        Process.Start(new ProcessStartInfo
        {
            FileName = _info.ReleaseUrl,
            UseShellExecute = true
        });
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isBusy)
            Close();
    }

    private void SetIcon(UIElement visibleIcon)
    {
        foreach (var icon in new[] { CheckingIcon, UpdateAvailableIcon, LatestIcon, ErrorIcon })
            icon.Visibility = ReferenceEquals(icon, visibleIcon) ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string TrimReleaseNotes(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "此版本没有填写更新说明。";

        var lines = text
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(line => line.TrimEnd())
            .Take(18);

        return string.Join(Environment.NewLine, lines).Trim();
    }
}
