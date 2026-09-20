using System.Net;
using System.Text.Json;
using TrimbleConnector.Config;
using TrimbleConnector.Models;

namespace TrimbleConnector.Services;

public sealed class ProjectProvisioningService
{
    public const string DefaultTriggerFileName = "_trimble_sync.json";

    private readonly ITrimbleApiClient _api;
    private readonly SyncJobStore _jobs;
    private readonly SyncLogBuffer _logs;
    private readonly ILogger<ProjectProvisioningService> _logger;
    private readonly object _gate = new();
    private readonly HashSet<string> _inFlight = new(StringComparer.OrdinalIgnoreCase);

    public ProjectProvisioningService(
        ITrimbleApiClient api,
        SyncJobStore jobs,
        SyncLogBuffer logs,
        ILogger<ProjectProvisioningService> logger)
    {
        _api = api;
        _jobs = jobs;
        _logs = logs;
        _logger = logger;
    }

    public static bool IsTriggerFile(string path, string? triggerFileName = null)
    {
        var name = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (name.Equals(DefaultTriggerFileName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(triggerFileName)
            && name.Equals(triggerFileName, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ConnectProject> ProvisionFromUiAsync(
        ProvisionProjectRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.LocalFolderPath))
        {
            throw new ArgumentException("name and localFolderPath are required.");
        }

        var config = _jobs.GetConfig();
        var templateId = FirstNonEmpty(request.TemplateProjectId, config.ProjectProvisioning.DefaultTemplateProjectId);
        if (string.IsNullOrWhiteSpace(templateId))
        {
            throw new ArgumentException("A template project id is required.");
        }

        Directory.CreateDirectory(request.LocalFolderPath);
        var project = await CloneOrCreateAsync(
            templateId,
            request.Name.Trim(),
            request.Description,
            config.ProjectProvisioning.DefaultRegion,
            cancellationToken).ConfigureAwait(false);

        LinkProject(
            project,
            request.LocalFolderPath.Trim(),
            request.FolderMappings,
            config.ProjectProvisioning);

        var triggerPath = Path.Combine(request.LocalFolderPath, config.ProjectProvisioning.TriggerFileName);
        WriteTriggerFile(triggerPath, new TriggerFilePayload
        {
            Name = request.Name.Trim(),
            Description = request.Description,
            TemplateProjectId = templateId,
            ProjectId = project.Id,
            RootId = project.EffectiveRootId,
            Status = "linked",
            LocalFolderPath = request.LocalFolderPath.Trim(),
            FolderMappings = request.FolderMappings
        });

        return project;
    }

    public async Task<bool> ScanTriggersAsync(CancellationToken cancellationToken)
    {
        var config = _jobs.GetConfig();
        if (!config.ProjectProvisioning.AutoProvisionOnFolderTrigger)
        {
            return false;
        }

        var triggerName = string.IsNullOrWhiteSpace(config.ProjectProvisioning.TriggerFileName)
            ? DefaultTriggerFileName
            : config.ProjectProvisioning.TriggerFileName;
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(config.ProjectProvisioning.WatchRoot)
            && Directory.Exists(config.ProjectProvisioning.WatchRoot))
        {
            foreach (var folder in Directory.EnumerateDirectories(config.ProjectProvisioning.WatchRoot))
            {
                candidates.Add(Path.Combine(folder, triggerName));
            }
        }

        foreach (var job in config.SyncJobs)
        {
            var root = job.EffectiveLocalRoot;
            if (!string.IsNullOrWhiteSpace(root))
            {
                candidates.Add(Path.Combine(root, triggerName));
            }
        }

        var changed = false;
        foreach (var path in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path))
            {
                changed |= await ProcessTriggerFileAsync(path, cancellationToken).ConfigureAwait(false);
            }
        }

        return changed;
    }

    public async Task<bool> ProcessTriggerFileAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        string? key = null;
        lock (_gate)
        {
            key = Path.GetFullPath(path);
            if (!_inFlight.Add(key))
            {
                return false;
            }
        }

        try
        {
            return await ProcessTriggerFileCoreAsync(path, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                if (key is not null)
                {
                    _inFlight.Remove(key);
                }
            }
        }
    }

    private async Task<bool> ProcessTriggerFileCoreAsync(string path, CancellationToken cancellationToken)
    {
        TriggerFilePayload payload;
        try
        {
            payload = JsonSerializer.Deserialize<TriggerFilePayload>(
                await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false),
                JsonDefaults.Serializer) ?? new TriggerFilePayload();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not parse trigger file {Path}.", path);
            return false;
        }

        var folder = Path.GetDirectoryName(path) ?? payload.LocalFolderPath;
        if (string.IsNullOrWhiteSpace(folder))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(payload.ProjectId)
            && string.Equals(payload.Status, "linked", StringComparison.OrdinalIgnoreCase))
        {
            return EnsureLinkedJob(payload.ProjectId, folder, payload.RootId, payload.FolderMappings);
        }

        var config = _jobs.GetConfig();
        var templateId = FirstNonEmpty(payload.TemplateProjectId, config.ProjectProvisioning.DefaultTemplateProjectId);
        var name = FirstNonEmpty(payload.Name, Path.GetFileName(folder));
        if (string.IsNullOrWhiteSpace(templateId) || string.IsNullOrWhiteSpace(name))
        {
            _logger.LogWarning("Trigger file {Path} is missing a template project id or name.", path);
            return false;
        }

        _logs.Add($"Provisioning Trimble Connect project '{name}' from template {templateId}.");
        var project = await CloneOrCreateAsync(
            templateId,
            name,
            payload.Description,
            config.ProjectProvisioning.DefaultRegion,
            cancellationToken).ConfigureAwait(false);

        LinkProject(project, folder, payload.FolderMappings, config.ProjectProvisioning);
        payload.ProjectId = project.Id;
        payload.RootId = project.EffectiveRootId;
        payload.Status = "linked";
        payload.LocalFolderPath = folder;
        payload.TemplateProjectId = templateId;
        payload.Name = name;
        WriteTriggerFile(path, payload);
        _logs.Add($"Linked local folder {folder} to project {project.Id}.");
        return true;
    }

    private async Task<ConnectProject> CloneOrCreateAsync(
        string templateProjectId,
        string name,
        string? description,
        string? region,
        CancellationToken cancellationToken)
    {
        try
        {
            var clone = await _api.CloneProjectAsync(templateProjectId, name, description, cancellationToken)
                .ConfigureAwait(false);
            if (clone.IsFailed)
            {
                throw new InvalidOperationException(clone.Error ?? $"Clone failed with status {clone.Status}.");
            }

            if (!string.IsNullOrWhiteSpace(clone.ResolvedProjectId) && clone.IsDone)
            {
                return await _api.GetProjectAsync(clone.ResolvedProjectId, cancellationToken).ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(clone.Id) && !clone.IsDone)
            {
                return await _api.WaitForCloneAsync(clone.Id, cancellationToken).ConfigureAwait(false);
            }

            if (clone.Project is not null && !string.IsNullOrWhiteSpace(clone.Project.Id))
            {
                return clone.Project;
            }

            throw new InvalidOperationException("Clone API did not return a project id.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed)
        {
            _logger.LogWarning(ex, "POST /projects/clones is unavailable. Falling back to POST /projects.");
            return await _api.CreateProjectAsync(name, description, region, cancellationToken).ConfigureAwait(false);
        }
    }

    private void LinkProject(
        ConnectProject project,
        string localFolder,
        List<FolderMapping>? mappings,
        ProjectProvisioningOptions provisioning)
    {
        var rootId = project.EffectiveRootId ?? string.Empty;
        var folderMappings = mappings is { Count: > 0 }
            ? mappings
            : [new FolderMapping
            {
                LocalSubPath = string.Empty,
                RemoteFolderId = rootId,
                Direction = SyncDirection.TwoWay
            }];

        _jobs.UpsertProjectJob(new SyncJobOptions
        {
            JobId = $"job-{project.Id}",
            ProjectId = project.Id,
            LocalProjectRoot = localFolder,
            LocalFolderPath = localFolder,
            RemoteFolderId = folderMappings[0].RemoteFolderId,
            FolderMappings = folderMappings,
            Direction = folderMappings[0].Direction,
            SyncIntervalSeconds = 60
        });

        _ = provisioning;
    }

    private bool EnsureLinkedJob(
        string projectId,
        string localFolder,
        string? rootId,
        List<FolderMapping>? mappings)
    {
        var existing = _jobs.GetConfig().SyncJobs.FirstOrDefault(job =>
            string.Equals(job.ProjectId, projectId, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return false;
        }

        LinkProject(
            new ConnectProject { Id = projectId, RootId = rootId },
            localFolder,
            mappings,
            _jobs.GetConfig().ProjectProvisioning);
        return true;
    }

    private static void WriteTriggerFile(string path, TriggerFilePayload payload)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonDefaults.Serializer)
        {
            WriteIndented = true
        }));
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
