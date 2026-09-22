namespace TrimbleConnector.Models;

public sealed class FolderMetadata
{
    public string? ErpProjectId { get; set; }

    public string? Description { get; set; }

    public List<string> Tags { get; set; } = [];

    public DateTimeOffset? LastSyncTime { get; set; }

    public int TotalFilesSynced { get; set; }

    public double TotalSizeMb { get; set; }

    public string SyncStatus { get; set; } = FolderSyncStatus.Ok;
}

public readonly record struct FolderFileStats(int FileCount, DateTime? LastSyncedUtc);

public static class FolderSyncStatus
{
    public const string Ok = "OK";

    public const string Syncing = "SYNCING";

    public const string Error = "ERROR";

    public const string Paused = "PAUSED";
}

public sealed class ConnectTag
{
    public string Id { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string? Label { get; set; }

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(Name) ? Name
        : !string.IsNullOrWhiteSpace(Label) ? Label
        : Id;
}

public sealed class TagCreateRequest
{
    public string Name { get; set; } = string.Empty;

    public string? ProjectId { get; set; }
}

public sealed class TagAssignRequest
{
    public List<string> ObjectIds { get; set; } = [];

    public string ObjectType { get; set; } = "FOLDER";
}

public sealed class OverviewJobDto
{
    public string JobId { get; set; } = string.Empty;

    public string ParentJobId { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string LocalFolderPath { get; set; } = string.Empty;

    public string RemoteProjectName { get; set; } = string.Empty;

    public string RemoteFolderName { get; set; } = string.Empty;

    public string RemoteFolderPath { get; set; } = "/";

    public string? RemoteFolderId { get; set; }

    public string Direction { get; set; } = "LocalToCloud";

    public int SyncIntervalSeconds { get; set; }

    public bool Enabled { get; set; } = true;

    public string SyncStatus { get; set; } = FolderSyncStatus.Ok;

    public string? ErpProjectId { get; set; }

    public string? Description { get; set; }

    public List<string> Tags { get; set; } = [];

    public DateTimeOffset? LastSyncTime { get; set; }

    public int TotalFilesSynced { get; set; }

    public double TotalSizeMb { get; set; }

    public string? LastActivity { get; set; }
}

public sealed class OverviewStatsDto
{
    public int TotalActiveMappings { get; set; }

    public int TotalMappings { get; set; }

    public int TotalSyncedFiles { get; set; }

    public double DiskUsageMb { get; set; }

    public DateTimeOffset? LastActivity { get; set; }

    public string? LastActivitySummary { get; set; }
}

public sealed class OverviewJobActionRequest
{
    public string JobId { get; set; } = string.Empty;
}

public sealed class OverviewMetadataUpdateRequest
{
    public string JobId { get; set; } = string.Empty;

    public string? ErpProjectId { get; set; }

    public string? Description { get; set; }

    public List<string>? Tags { get; set; }

    public int? SyncIntervalSeconds { get; set; }

    public bool WriteConnectTags { get; set; }
}
