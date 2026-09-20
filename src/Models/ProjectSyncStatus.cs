using System.Text.Json.Serialization;

namespace TrimbleConnector.Models;

public sealed class ProjectSyncStatus
{
    [JsonPropertyName("from")]
    public string? From { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }

    public string? EffectiveCursor => Cursor ?? From;
}
