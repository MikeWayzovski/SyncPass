using System.Text.Json.Serialization;
using TrimbleConnector.Config;

namespace TrimbleConnector.Models;

public sealed class ConnectUser
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("hasImage")]
    public bool? HasImage { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; set; }

    [JsonPropertyName("thumbnailUrl")]
    public string? ThumbnailUrl { get; set; }

    [JsonPropertyName("picture")]
    public string? Picture { get; set; }

    public string? EffectiveThumbnail =>
        !string.IsNullOrWhiteSpace(Thumbnail) ? Thumbnail
        : !string.IsNullOrWhiteSpace(ThumbnailUrl) ? ThumbnailUrl
        : Picture;

    public string DisplayLabel
    {
        get
        {
            var full = $"{FirstName} {LastName}".Trim();
            if (!string.IsNullOrWhiteSpace(full))
            {
                return full;
            }

            if (!string.IsNullOrWhiteSpace(DisplayName))
            {
                return DisplayName;
            }

            return Email ?? "Trimble ID";
        }
    }
}

public sealed class SetupStatusResponse
{
    public bool Authenticated { get; set; }

    public bool Configured { get; set; }

    public string? UserName { get; set; }

    public string? UserFirstName { get; set; }

    public string? UserLastName { get; set; }

    public string? UserEmail { get; set; }

    public string? UserThumbnail { get; set; }

    public SetupUserDto? User { get; set; }

    public IReadOnlyList<SetupJobStatus> Jobs { get; set; } = [];

    public int ProjectCount { get; set; }

    public int SharedRuleCount { get; set; }

    public IReadOnlyList<string> Logs { get; set; } = [];

    public string Version { get; set; } = string.Empty;
}

public sealed class SetupUserDto
{
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Email { get; set; }

    public bool? HasImage { get; set; }

    public string? Thumbnail { get; set; }
}

public sealed class SetupJobStatus
{
    public string JobId { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string? RemoteFolderPath { get; set; }

    public string? RemoteFolderId { get; set; }

    public string LocalFolderPath { get; set; } = string.Empty;

    public string Direction { get; set; } = "TwoWay";

    public int SyncIntervalSeconds { get; set; }

    public string State { get; set; } = "idle";

    public DateTimeOffset? LastSyncedAtUtc { get; set; }
}

public sealed class ProvisionProjectRequest
{
    public string TemplateProjectId { get; set; } = string.Empty;

    public string TemplateProjectName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string LocalFolderPath { get; set; } = string.Empty;

    public List<FolderMapping>? FolderMappings { get; set; }
}

public sealed class SaveConfigRequest
{
    public ProjectProvisioningOptions? ProjectProvisioning { get; set; }

    public List<SharedSyncRule>? SharedSyncRules { get; set; }

    public List<SyncJobOptions>? SyncJobs { get; set; }
}

public sealed class SetupProjectDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? RootId { get; set; }

    public string? Location { get; set; }
}

public sealed class SetupFolderDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Path { get; set; } = "/";

    public string? ParentId { get; set; }
}

public sealed class SaveSetupRequest
{
    public string ProjectName { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string RemoteFolderPath { get; set; } = "/";

    public string RemoteFolderId { get; set; } = string.Empty;

    public string LocalFolderPath { get; set; } = string.Empty;

    public int SyncIntervalSeconds { get; set; } = 60;

    public string Direction { get; set; } = "TwoWay";

    public List<FolderMapping>? FolderMappings { get; set; }

    public bool Enabled { get; set; }
}

public sealed class LocalTreeResponse
{
    public bool Exists { get; set; }

    public string Path { get; set; } = string.Empty;

    public string? Error { get; set; }

    public IReadOnlyList<LocalFolderDto> Folders { get; set; } = [];
}

public sealed class LocalFolderDto
{
    public string Name { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public int FileCount { get; set; }
}

public sealed class BrowseResponse
{
    public string Path { get; set; } = string.Empty;

    public string? Parent { get; set; }

    public string? Error { get; set; }

    public IReadOnlyList<BrowseEntryDto> Folders { get; set; } = [];

    public IReadOnlyList<BrowseEntryDto> Roots { get; set; } = [];
}

public sealed class BrowseEntryDto
{
    public string Name { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;
}

public sealed class CreateFolderRequest
{
    public string ProjectName { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string ParentPath { get; set; } = "/";

    public string Name { get; set; } = string.Empty;
}

public sealed class InventoryRequest
{
    public string ProjectName { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string RemoteFolderPath { get; set; } = "/";

    public string LocalFolderPath { get; set; } = string.Empty;

    public List<FolderMapping>? FolderMappings { get; set; }
}

public sealed class InventoryResponse
{
    public string JobId { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;

    public int UploadCount { get; set; }

    public int DownloadCount { get; set; }

    public int SyncedCount { get; set; }

    public IReadOnlyList<InventoryItemDto> Items { get; set; } = [];
}

public sealed class InventoryItemDto
{
    public string RelativePath { get; set; } = string.Empty;

    public string Folder { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
}

public sealed class ActivateJobRequest
{
    public string JobId { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string LocalFolderPath { get; set; } = string.Empty;
}

public sealed class LoginUrlResponse
{
    public string Url { get; set; } = string.Empty;
}
