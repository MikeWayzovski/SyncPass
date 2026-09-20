namespace TrimbleConnector.Models;

public sealed class FileState
{
    public string RelativePath { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string? LocalHash { get; set; }

    public DateTime LocalLastWriteTimeUtc { get; set; }

    public string? TrimbleFileId { get; set; }

    public string? TrimbleVersionId { get; set; }

    public DateTime LastSyncedAtUtc { get; set; }
}
