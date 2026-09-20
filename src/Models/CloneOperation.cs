using TrimbleConnector.Config;

namespace TrimbleConnector.Models;

public sealed class CloneOperation
{
    public string Id { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? ProjectId { get; set; }

    public string? Error { get; set; }

    public ConnectProject? Project { get; set; }

    public bool IsDone =>
        StatusEquals("DONE")
        || StatusEquals("COMPLETED")
        || StatusEquals("SUCCEEDED")
        || StatusEquals("SUCCESS")
        || (!string.IsNullOrWhiteSpace(ResolvedProjectId) && string.IsNullOrWhiteSpace(Status));

    public bool IsFailed =>
        StatusEquals("FAILED")
        || StatusEquals("ERROR")
        || StatusEquals("CANCELLED")
        || StatusEquals("CANCELED");

    public string? ResolvedProjectId =>
        FirstNonEmpty(ProjectId, Project?.Id);

    private bool StatusEquals(string expected) =>
        string.Equals(Status, expected, StringComparison.OrdinalIgnoreCase);

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}

public sealed class TriggerFilePayload
{
    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? TemplateProjectId { get; set; }

    public string? TemplateProjectName { get; set; }

    public string? ProjectId { get; set; }

    public string? RootId { get; set; }

    public string? Status { get; set; }

    public string? LocalFolderPath { get; set; }

    public List<FolderMapping>? FolderMappings { get; set; }
}
