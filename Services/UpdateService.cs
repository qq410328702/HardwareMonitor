using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HardwareMonitor.Services;

public sealed class UpdateService
{
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/qq410328702/HardwareMonitor/releases/latest";
    private static readonly Regex ZipAssetRegex = new(@"^HardwareMonitor-v\d+\.\d+\.\d+-win-x64\.zip$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Sha256Regex = new(@"[a-fA-F0-9]{64}", RegexOptions.Compiled);
    private static readonly HttpClient Http = CreateHttpClient();

    private readonly ILogger? _logger;
    private readonly string _updatesRoot;

    public UpdateService(ILogger? logger = null)
    {
        _logger = logger;
        _updatesRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HardwareMonitor",
            "Updates");
    }

    public static bool TryApplyUpdateFromArgs(string[] args)
    {
        if (!args.Any(a => string.Equals(a, "--apply-update", StringComparison.OrdinalIgnoreCase)))
            return false;

        UpdateInstaller.Apply(args);
        return true;
    }

    public async Task<UpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await Http.GetAsync(LatestReleaseApiUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, cancellationToken: cancellationToken)
                ?? throw new UpdateException("无法解析 GitHub Release 信息");

            var latestVersion = ParseVersion(release.TagName);
            var currentVersion = GetCurrentVersion();
            var zipAsset = release.Assets.FirstOrDefault(a => ZipAssetRegex.IsMatch(a.Name));
            var shaAsset = zipAsset is null
                ? null
                : release.Assets.FirstOrDefault(a => string.Equals(a.Name, zipAsset.Name + ".sha256", StringComparison.OrdinalIgnoreCase));

            var canAutoInstall = zipAsset is not null && shaAsset is not null;
            var unavailableReason = canAutoInstall
                ? ""
                : zipAsset is null
                    ? "最新 Release 未找到 Windows x64 更新包"
                    : "最新 Release 缺少 SHA256 校验文件，不能自动安装";

            return new UpdateInfo
            {
                CurrentVersionText = FormatVersion(currentVersion),
                LatestVersionText = FormatVersion(latestVersion),
                TagName = release.TagName,
                ReleaseUrl = release.HtmlUrl,
                ReleaseNotes = release.Body ?? "",
                ZipAssetName = zipAsset?.Name ?? "",
                ZipDownloadUrl = zipAsset?.BrowserDownloadUrl ?? "",
                Sha256AssetName = shaAsset?.Name ?? "",
                Sha256DownloadUrl = shaAsset?.BrowserDownloadUrl ?? "",
                IsNewer = latestVersion > currentVersion,
                CanAutoInstall = canAutoInstall,
                AutoInstallUnavailableReason = unavailableReason
            };
        }
        catch (UpdateException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.Error("Failed to check for updates", ex);
            throw new UpdateException("检查更新失败，请稍后重试", ex);
        }
    }

    public async Task<UpdateDownloadResult> DownloadAndPrepareUpdateAsync(
        UpdateInfo info,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!info.IsNewer)
            throw new UpdateException("当前已经是最新版本");

        if (!info.CanAutoInstall)
            throw new UpdateException(info.AutoInstallUnavailableReason);

        try
        {
            var versionDirectory = Path.Combine(_updatesRoot, SanitizeFileName(info.TagName));
            var stagingDirectory = Path.Combine(versionDirectory, "staging");
            var zipPath = Path.Combine(versionDirectory, info.ZipAssetName);
            var shaPath = Path.Combine(versionDirectory, info.Sha256AssetName);
            var logPath = Path.Combine(_updatesRoot, "update.log");

            progress?.Report("正在准备更新目录...");
            RecreateDirectory(versionDirectory);
            Directory.CreateDirectory(stagingDirectory);
            Directory.CreateDirectory(_updatesRoot);

            progress?.Report("正在下载更新包...");
            await DownloadFileAsync(info.ZipDownloadUrl, zipPath, cancellationToken);

            progress?.Report("正在下载校验文件...");
            await DownloadFileAsync(info.Sha256DownloadUrl, shaPath, cancellationToken);

            progress?.Report("正在校验更新包...");
            ValidateSha256(zipPath, shaPath);

            progress?.Report("正在解压更新包...");
            ZipFile.ExtractToDirectory(zipPath, stagingDirectory, true);
            var stagedExe = Path.Combine(stagingDirectory, "HardwareMonitor.exe");
            if (!File.Exists(stagedExe))
                throw new UpdateException("更新包中未找到 HardwareMonitor.exe");

            progress?.Report("正在准备安装程序...");
            var updaterExe = PrepareUpdaterRuntime();
            var processPath = Environment.ProcessPath ?? throw new UpdateException("无法确定当前程序路径");
            var installDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var restartExe = Path.Combine(installDirectory, Path.GetFileName(processPath));

            return new UpdateDownloadResult
            {
                Info = info,
                VersionDirectory = versionDirectory,
                ZipPath = zipPath,
                Sha256Path = shaPath,
                StagingDirectory = stagingDirectory,
                UpdaterExecutablePath = updaterExe,
                InstallDirectory = installDirectory,
                RestartExecutablePath = restartExe,
                LogPath = logPath,
                MainProcessId = Environment.ProcessId
            };
        }
        catch (UpdateException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.Error("Failed to download and prepare update", ex);
            throw new UpdateException("准备更新失败，请稍后重试", ex);
        }
    }

    public void StartUpdater(UpdateDownloadResult result)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = result.UpdaterExecutablePath,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(result.UpdaterExecutablePath) ?? _updatesRoot
        };

        startInfo.ArgumentList.Add("--apply-update");
        startInfo.ArgumentList.Add("--source");
        startInfo.ArgumentList.Add(result.StagingDirectory);
        startInfo.ArgumentList.Add("--target");
        startInfo.ArgumentList.Add(result.InstallDirectory);
        startInfo.ArgumentList.Add("--restart");
        startInfo.ArgumentList.Add(result.RestartExecutablePath);
        startInfo.ArgumentList.Add("--wait-pid");
        startInfo.ArgumentList.Add(result.MainProcessId.ToString());
        startInfo.ArgumentList.Add("--log");
        startInfo.ArgumentList.Add(result.LogPath);
        startInfo.ArgumentList.Add("--version-dir");
        startInfo.ArgumentList.Add(result.VersionDirectory);

        Process.Start(startInfo);
    }

    private async Task DownloadFileAsync(string url, string path, CancellationToken cancellationToken)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = File.Create(path);
        await source.CopyToAsync(destination, cancellationToken);
    }

    private void ValidateSha256(string zipPath, string shaPath)
    {
        var checksumText = File.ReadAllText(shaPath);
        var match = Sha256Regex.Match(checksumText);
        if (!match.Success)
            throw new UpdateException("SHA256 校验文件格式无效");

        var expected = match.Value.ToLowerInvariant();
        using var stream = File.OpenRead(zipPath);
        var actual = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            throw new UpdateException("更新包 SHA256 校验失败");
    }

    private string PrepareUpdaterRuntime()
    {
        var sourceDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var processPath = Environment.ProcessPath ?? throw new UpdateException("无法确定当前程序路径");
        var updaterRuntime = Path.Combine(_updatesRoot, "UpdaterRuntime-" + DateTime.Now.ToString("yyyyMMddHHmmss"));
        Directory.CreateDirectory(updaterRuntime);

        CopyDirectory(sourceDirectory, updaterRuntime, overwrite: true);

        var updaterExe = Path.Combine(updaterRuntime, "HardwareMonitor.Updater.exe");
        File.Copy(processPath, updaterExe, true);
        return updaterExe;
    }

    private static void RecreateDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, true);
        Directory.CreateDirectory(path);
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
            File.Copy(file, Path.Combine(targetDirectory, relative), overwrite);
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("HardwareMonitor-Updater/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private static Version GetCurrentVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
        return new Version(version.Major, version.Minor, Math.Max(0, version.Build));
    }

    private static Version ParseVersion(string text)
    {
        text = (text ?? "").Trim();
        if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            text = text[1..];

        var core = text.Split('-', '+')[0];
        var parts = core.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var values = new int[3];
        for (int i = 0; i < values.Length && i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out values[i]))
                values[i] = 0;
        }

        return new Version(values[0], values[1], values[2]);
    }

    private static string FormatVersion(Version version)
    {
        return $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private static string SanitizeFileName(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '-');
        return string.IsNullOrWhiteSpace(value) ? "latest" : value;
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = "";

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = "";

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset> Assets { get; set; } = [];
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = "";
    }
}

internal static class UpdateInstaller
{
    public static void Apply(string[] args)
    {
        var source = GetArg(args, "--source");
        var target = GetArg(args, "--target");
        var restart = GetArg(args, "--restart");
        var logPath = GetArg(args, "--log");
        var versionDirectory = GetArg(args, "--version-dir");
        var waitPidText = GetArg(args, "--wait-pid");

        try
        {
            Log(logPath, "Updater started.");
            if (int.TryParse(waitPidText, out var waitPid) && waitPid > 0)
                WaitForProcessExit(waitPid, logPath);

            CopyDirectory(source, target, overwrite: true);
            Log(logPath, "Files copied.");

            if (!string.IsNullOrWhiteSpace(restart) && File.Exists(restart))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = restart,
                    WorkingDirectory = Path.GetDirectoryName(restart) ?? target,
                    UseShellExecute = true
                });
                Log(logPath, "Application restarted.");
            }

            CleanupVersionDirectory(versionDirectory, logPath);
        }
        catch (Exception ex)
        {
            Log(logPath, "Update failed: " + ex);
            try
            {
                System.Windows.MessageBox.Show(
                    "自动更新失败，原版本未被删除。\n\n" + ex.Message,
                    "更新失败",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
            catch
            {
            }
        }
    }

    private static string GetArg(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return "";
    }

    private static void WaitForProcessExit(int processId, string logPath)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (!process.WaitForExit(60000))
                Log(logPath, $"Process {processId} did not exit before timeout.");
        }
        catch (ArgumentException)
        {
        }
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory, bool overwrite)
    {
        if (string.IsNullOrWhiteSpace(sourceDirectory) || string.IsNullOrWhiteSpace(targetDirectory))
            throw new InvalidOperationException("Updater source or target path is empty.");

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relative));
        }

        Directory.CreateDirectory(targetDirectory);
        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, file);
            var destination = Path.Combine(targetDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite);
        }
    }

    private static void CleanupVersionDirectory(string versionDirectory, string logPath)
    {
        if (string.IsNullOrWhiteSpace(versionDirectory))
            return;

        try
        {
            Directory.Delete(versionDirectory, true);
            Log(logPath, "Version staging directory cleaned.");
        }
        catch (Exception ex)
        {
            Log(logPath, "Failed to clean staging directory: " + ex.Message);
        }
    }

    private static void Log(string logPath, string message)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(logPath))
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}
