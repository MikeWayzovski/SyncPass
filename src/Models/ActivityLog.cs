namespace TrimbleConnector.Models;

public sealed class ActivityLog
{
    public long Id { get; set; }

    public DateTime TimestampUtc { get; set; }

    public string ProjectId { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public string Details { get; set; } = string.Empty;
}
