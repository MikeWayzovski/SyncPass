using TrimbleConnector.Models;

namespace TrimbleConnector.Config;

public sealed class ConnectorSyncConfig
{
    public ProjectProvisioningOptions ProjectProvisioning { get; set; } = new();

    public List<SharedSyncRule> SharedSyncRules { get; set; } = [];

    public List<SyncJobOptions> SyncJobs { get; set; } = [];

    public WizardPreferences WizardPreferences { get; set; } = new();
}

public sealed class WizardPreferences
{
    public string DefaultSyncDirection { get; set; } = "LocalToCloud";

    public string DefaultRemoteParentPath { get; set; } = "/";
}

public sealed class ProjectProvisioningOptions
{
    public bool AutoProvisionOnFolderTrigger { get; set; }

    public string TriggerFileName { get; set; } = "_trimble_sync.json";

    public string DefaultTemplateProjectId { get; set; } = string.Empty;

    public string DefaultTemplateProjectName { get; set; } = string.Empty;

    public string DefaultRegion { get; set; } = "europe";

    public string? WatchRoot { get; set; }
}

public sealed class SharedSyncRule
{
    public string Name { get; set; } = string.Empty;

    public string LocalFolderPath { get; set; } = string.Empty;

    public List<SyncTarget> SyncTargets { get; set; } = [];

    public SyncDirection Direction { get; set; } = SyncDirection.LocalToCloud;

    public int SyncIntervalSeconds { get; set; } = 300;
}

public sealed class SyncTarget
{
    public string ProjectName { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string RemoteFolderPath { get; set; } = string.Empty;

    public string RemoteFolderId { get; set; } = string.Empty;
}

public sealed class FolderMapping
{
    public string LocalSubPath { get; set; } = string.Empty;

    public string RemoteFolderPath { get; set; } = string.Empty;

    public string RemoteFolderId { get; set; } = string.Empty;

    public SyncDirection Direction { get; set; } = SyncDirection.TwoWay;

    public bool HasRemoteTarget =>
        !string.IsNullOrWhiteSpace(RemoteFolderPath)
        || !string.IsNullOrWhiteSpace(RemoteFolderId);
}
