using System.Text.Json.Serialization;

namespace TrimbleConnector.Models;

public sealed class SyncObjectPage
{
    [JsonPropertyName("items")]
    public List<SyncObject> Items { get; set; } = [];

    [JsonPropertyName("data")]
    public List<SyncObject>? Data { get; set; }

    [JsonPropertyName("next")]
    public string? Next { get; set; }

    [JsonPropertyName("skipToken")]
    public string? SkipToken { get; set; }

    public IEnumerable<SyncObject> AllItems =>
        Items.Count > 0 ? Items : Data ?? Enumerable.Empty<SyncObject>();

    public string? NextCursor => Next ?? SkipToken;
}

public sealed class SyncObject
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("parentId")]
    public string? ParentId { get; set; }

    [JsonPropertyName("hash")]
    public string? Hash { get; set; }

    [JsonPropertyName("size")]
    public long? Size { get; set; }

    [JsonPropertyName("versionId")]
    public string? VersionId { get; set; }

    [JsonPropertyName("modifiedOn")]
    public DateTimeOffset? ModifiedOn { get; set; }

    [JsonPropertyName("deleted")]
    public bool Deleted { get; set; }

    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; }

    [JsonPropertyName("changeType")]
    public string? ChangeType { get; set; }

    public bool IsFile =>
        string.Equals(Type, "FILE", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Type, "FS_FILE", StringComparison.OrdinalIgnoreCase);

    public bool IsFolder =>
        string.Equals(Type, "FOLDER", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Type, "FS_FOLDER", StringComparison.OrdinalIgnoreCase);

    public bool IsRemoved =>
        Deleted
        || IsDeleted
        || string.Equals(ChangeType, "deleted", StringComparison.OrdinalIgnoreCase);
}
