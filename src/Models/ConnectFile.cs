using System.Text.Json.Serialization;

namespace TrimbleConnector.Models;

public sealed class ConnectProject
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("rootId")]
    public string? RootId { get; set; }

    [JsonPropertyName("rootFolderId")]
    public string? RootFolderId { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("region")]
    public string? Region { get; set; }

    public string? EffectiveRootId => RootId ?? RootFolderId;

    public string? EffectiveLocation => Location ?? Region;
}

public sealed class ConnectFolder
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("parentId")]
    public string? ParentId { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }
}

public sealed class ConnectFile
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("parentId")]
    public string? ParentId { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("hash")]
    public string? Hash { get; set; }

    [JsonPropertyName("size")]
    public long? Size { get; set; }

    [JsonPropertyName("versionId")]
    public string? VersionId { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

public sealed class DownloadUrlResponse
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; set; }

    public string? EffectiveUrl => Url ?? DownloadUrl;
}

public sealed class UploadInitRequest
{
    public string ProjectId { get; set; } = string.Empty;

    public string ParentId { get; set; } = string.Empty;

    [JsonPropertyName("parentType")]
    public string ParentType { get; set; } = "FOLDER";

    public string Name { get; set; } = string.Empty;

    public long Size { get; set; }

    public string? FileId { get; set; }
}

public sealed class UploadInitResponse
{
    [JsonPropertyName("uploadId")]
    public string? UploadId { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("uploadUrl")]
    public string? UploadUrl { get; set; }

    [JsonPropertyName("fileId")]
    public string? FileId { get; set; }

    public string? EffectiveUrl => Url ?? UploadUrl;
}

public sealed class UploadCommitRequest
{
    public string? UploadId { get; set; }

    public string? ProjectId { get; set; }

    public string? FileId { get; set; }
}
