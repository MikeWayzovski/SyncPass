namespace TrimbleConnector.Models;

public enum LocalChangeKind
{
    Created,
    Changed,
    Deleted,
    Renamed
}

public sealed record LocalFileChange(
    string FullPath,
    LocalChangeKind Kind,
    string? OldFullPath = null);
