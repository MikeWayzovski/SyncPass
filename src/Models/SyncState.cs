namespace TrimbleConnector.Models;

public sealed class SyncState
{
    public string? Cursor { get; set; }

    public Dictionary<string, TrackedFile> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class TrackedFile
{
    public string RemoteId { get; set; } = string.Empty;

    public string? Hash { get; set; }

    public string? ParentId { get; set; }
}
