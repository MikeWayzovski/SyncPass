using System.Text.Json.Serialization;

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

    public IReadOnlyList<string> Logs { get; set; } = [];
}

public sealed class SetupUserDto
{
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Email { get; set; }

    public string? Thumbnail { get; set; }
}

public sealed class SetupJobStatus
{
    public string ProjectId { get; set; } = string.Empty;

    public string? RemoteFolderId { get; set; }

    public string LocalFolderPath { get; set; } = string.Empty;

    public string Direction { get; set; } = "TwoWay";

    public int SyncIntervalSeconds { get; set; }

    public string State { get; set; } = "idle";
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

    public string? ParentId { get; set; }
}

public sealed class SaveSetupRequest
{
    public string ProjectId { get; set; } = string.Empty;

    public string RemoteFolderId { get; set; } = string.Empty;

    public string LocalFolderPath { get; set; } = string.Empty;

    public int SyncIntervalSeconds { get; set; } = 60;

    public string Direction { get; set; } = "TwoWay";
}

public sealed class LoginUrlResponse
{
    public string Url { get; set; } = string.Empty;
}
