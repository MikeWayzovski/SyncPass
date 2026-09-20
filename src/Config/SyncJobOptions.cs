using TrimbleConnector.Models;

namespace TrimbleConnector.Config;

public sealed class SyncJobOptions
{
    public string ProjectId { get; set; } = string.Empty;

    public string LocalFolderPath { get; set; } = string.Empty;

    public string RemoteFolderPath { get; set; } = "/";

    public string RemoteFolderId { get; set; } = string.Empty;

    public int SyncIntervalSeconds { get; set; } = 300;

    public SyncDirection Direction { get; set; } = SyncDirection.TwoWay;

    public TimeSpan Interval => TimeSpan.FromSeconds(Math.Max(15, SyncIntervalSeconds));

    public string? EffectiveRemoteFolderId =>
        string.IsNullOrWhiteSpace(RemoteFolderId) ? null : RemoteFolderId.Trim();
}
