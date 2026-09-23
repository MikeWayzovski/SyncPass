using System.Text.Json.Serialization;
using TrimbleConnector.Models;

namespace TrimbleConnector.Config;

public sealed class SyncJobOptions
{
    public string JobId { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string LocalProjectRoot { get; set; } = string.Empty;

    public string LocalFolderPath { get; set; } = string.Empty;

    public string RemoteFolderPath { get; set; } = string.Empty;

    public string RemoteFolderId { get; set; } = string.Empty;

    public List<FolderMapping> FolderMappings { get; set; } = [];

    public int SyncIntervalSeconds { get; set; } = 300;

    public SyncDirection Direction { get; set; } = SyncDirection.TwoWay;

    public bool Enabled { get; set; } = true;

    public string TargetFolderName { get; set; } = string.Empty;

    public bool AutoCreateRemoteFolder { get; set; } = true;

    public FolderMetadata Metadata { get; set; } = new();

    [JsonIgnore]
    public TimeSpan Interval => TimeSpan.FromSeconds(Math.Max(15, SyncIntervalSeconds));

    [JsonIgnore]
    public string? EffectiveRemoteFolderId =>
        string.IsNullOrWhiteSpace(RemoteFolderId) ? null : RemoteFolderId.Trim();

    [JsonIgnore]
    public string EffectiveRemotePath => RemotePath.Normalize(RemoteFolderPath);

    [JsonIgnore]
    public string EffectiveLocalRoot =>
        !string.IsNullOrWhiteSpace(LocalProjectRoot) ? LocalProjectRoot.Trim()
        : LocalFolderPath?.Trim() ?? string.Empty;

    [JsonIgnore]
    public bool HasProject =>
        !string.IsNullOrWhiteSpace(ProjectId) || !string.IsNullOrWhiteSpace(ProjectName);
}
