using System;

namespace HardwareMonitor.Services;

public sealed class UpdateInfo
{
    public string CurrentVersionText { get; init; } = "";
    public string LatestVersionText { get; init; } = "";
    public string TagName { get; init; } = "";
    public string ReleaseUrl { get; init; } = "";
    public string ReleaseNotes { get; init; } = "";
    public string ZipAssetName { get; init; } = "";
    public string ZipDownloadUrl { get; init; } = "";
    public string Sha256AssetName { get; init; } = "";
    public string Sha256DownloadUrl { get; init; } = "";
    public bool IsNewer { get; init; }
    public bool CanAutoInstall { get; init; }
    public string AutoInstallUnavailableReason { get; init; } = "";
}

public sealed class UpdateDownloadResult
{
    public required UpdateInfo Info { get; init; }
    public required string VersionDirectory { get; init; }
    public required string ZipPath { get; init; }
    public required string Sha256Path { get; init; }
    public required string StagingDirectory { get; init; }
    public required string UpdaterExecutablePath { get; init; }
    public required string InstallDirectory { get; init; }
    public required string RestartExecutablePath { get; init; }
    public required string LogPath { get; init; }
    public int MainProcessId { get; init; }
}

public sealed class UpdateException : Exception
{
    public UpdateException(string message) : base(message)
    {
    }

    public UpdateException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
