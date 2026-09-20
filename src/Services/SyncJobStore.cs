using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TrimbleConnector.Config;

namespace TrimbleConnector.Services;

public sealed class SyncJobStore
{
    private static readonly JsonSerializerOptions PersistJson = new(JsonDefaults.Serializer)
    {
        WriteIndented = true
    };

    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SyncJobStore> _logger;
    private readonly object _gate = new();
    private ConnectorSyncConfig _config;

    public SyncJobStore(
        IHostEnvironment environment,
        IConfiguration configuration,
        ILogger<SyncJobStore> logger)
    {
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
        _config = LoadFromDisk();
        Normalize(_config);
    }

    public ConnectorSyncConfig GetConfig()
    {
        lock (_gate)
        {
            return CloneConfig(_config);
        }
    }

    public IReadOnlyList<SyncJobOptions> GetJobs()
    {
        lock (_gate)
        {
            return Expand(_config).Select(CloneJob).ToList();
        }
    }

    public bool HasConfiguredJob => GetJobs().Any(IsComplete);

    public void Save(SyncJobOptions job)
    {
        ArgumentNullException.ThrowIfNull(job);
        UpsertProjectJob(job);
    }

    public void SaveConfig(ConnectorSyncConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        Normalize(config);
        Persist(config);
        _logger.LogInformation(
            "Saved connector config: {JobCount} project jobs, {SharedCount} shared rules.",
            config.SyncJobs.Count,
            config.SharedSyncRules.Count);
    }

    public void UpsertProjectJob(SyncJobOptions job)
    {
        ArgumentNullException.ThrowIfNull(job);

        lock (_gate)
        {
            NormalizeJob(job);
            var existing = _config.SyncJobs.FirstOrDefault(item =>
                (!string.IsNullOrWhiteSpace(job.JobId) && string.Equals(item.JobId, job.JobId, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(job.ProjectId)
                    && string.Equals(item.ProjectId, job.ProjectId, StringComparison.OrdinalIgnoreCase))
                || (string.IsNullOrWhiteSpace(job.ProjectId)
                    && !string.IsNullOrWhiteSpace(job.ProjectName)
                    && string.Equals(item.ProjectName, job.ProjectName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.EffectiveLocalRoot, job.EffectiveLocalRoot, StringComparison.OrdinalIgnoreCase)));

            if (existing is null)
            {
                _config.SyncJobs.Add(CloneJob(job));
            }
            else
            {
                var index = _config.SyncJobs.IndexOf(existing);
                _config.SyncJobs[index] = CloneJob(job);
                if (string.IsNullOrWhiteSpace(_config.SyncJobs[index].JobId))
                {
                    _config.SyncJobs[index].JobId = existing.JobId;
                }
            }

            PersistLocked();
        }

        _logger.LogInformation(
            "Saved sync job {JobId} {ProjectId} -> {Folder} ({Local}).",
            job.JobId,
            job.ProjectId,
            job.EffectiveRemoteFolderId,
            job.EffectiveLocalRoot);
    }

    public void UpsertSharedRule(SharedSyncRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            throw new ArgumentException("Shared rule name is required.", nameof(rule));
        }

        lock (_gate)
        {
            var existing = _config.SharedSyncRules.FirstOrDefault(item =>
                string.Equals(item.Name, rule.Name, StringComparison.OrdinalIgnoreCase));
            var copy = CloneRule(rule);
            if (existing is null)
            {
                _config.SharedSyncRules.Add(copy);
            }
            else
            {
                var index = _config.SharedSyncRules.IndexOf(existing);
                _config.SharedSyncRules[index] = copy;
            }

            PersistLocked();
        }
    }

    public void UpdateProvisioning(ProjectProvisioningOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        lock (_gate)
        {
            _config.ProjectProvisioning = CloneProvisioning(options);
            PersistLocked();
        }
    }

    private string JobsPath => Path.Combine(_environment.ContentRootPath, "data", "sync-jobs.json");

    private ConnectorSyncConfig LoadFromDisk()
    {
        try
        {
            if (File.Exists(JobsPath))
            {
                var json = File.ReadAllText(JobsPath);
                var trimmed = json.TrimStart();
                if (trimmed.StartsWith('['))
                {
                    var legacy = JsonSerializer.Deserialize<List<SyncJobOptions>>(json, JsonDefaults.Serializer);
                    if (legacy is { Count: > 0 })
                    {
                        return new ConnectorSyncConfig { SyncJobs = legacy };
                    }
                }
                else
                {
                    var fromFile = JsonSerializer.Deserialize<ConnectorSyncConfig>(json, JsonDefaults.Serializer);
                    if (fromFile is not null)
                    {
                        return fromFile;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read data/sync-jobs.json.");
        }

        var fromSettings = _configuration.GetSection("SyncJobs").Get<List<SyncJobOptions>>() ?? [];
        return new ConnectorSyncConfig { SyncJobs = fromSettings };
    }

    private void Persist(ConnectorSyncConfig config)
    {
        lock (_gate)
        {
            _config = CloneConfig(config);
            Normalize(_config);
            PersistLocked();
        }
    }

    private void PersistLocked()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(JobsPath)!);
        File.WriteAllText(JobsPath, JsonSerializer.Serialize(_config, PersistJson));
        PatchAppSettings(Expand(_config));
    }

    private void PatchAppSettings(List<SyncJobOptions> jobs)
    {
        var path = Path.Combine(_environment.ContentRootPath, "appsettings.json");
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? [];
            root["SyncJobs"] = JsonNode.Parse(JsonSerializer.Serialize(jobs, JsonDefaults.Serializer));
            File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not patch SyncJobs in appsettings.json. data/sync-jobs.json is still up to date.");
        }
    }

    internal static List<SyncJobOptions> Expand(ConnectorSyncConfig config)
    {
        var jobs = new List<SyncJobOptions>();
        foreach (var job in config.SyncJobs)
        {
            NormalizeJob(job);
            var root = job.EffectiveLocalRoot;
            if (job.FolderMappings.Count > 0)
            {
                foreach (var mapping in job.FolderMappings)
                {
                    if (!mapping.HasRemoteTarget)
                    {
                        continue;
                    }

                    var local = CombineLocal(root, mapping.LocalSubPath);
                    var rawPath = !string.IsNullOrWhiteSpace(mapping.RemoteFolderPath)
                        ? mapping.RemoteFolderPath
                        : job.RemoteFolderPath;
                    var remotePath = RemotePath.Copy(rawPath);
                    var remoteId = string.IsNullOrWhiteSpace(mapping.RemoteFolderId)
                        ? job.RemoteFolderId
                        : mapping.RemoteFolderId;
                    jobs.Add(new SyncJobOptions
                    {
                        JobId = string.IsNullOrWhiteSpace(job.JobId)
                            ? MakeId("map", job.ProjectId, job.ProjectName, local, remotePath, remoteId)
                            : $"{job.JobId}:{mapping.LocalSubPath}:{remotePath}",
                        ProjectName = job.ProjectName,
                        ProjectId = job.ProjectId,
                        LocalProjectRoot = root,
                        LocalFolderPath = local,
                        RemoteFolderPath = remotePath,
                        RemoteFolderId = remoteId?.Trim() ?? string.Empty,
                        SyncIntervalSeconds = job.SyncIntervalSeconds,
                        Direction = mapping.Direction,
                        Enabled = job.Enabled
                    });
                }

                continue;
            }

            if (IsComplete(job))
            {
                var copy = CloneJob(job);
                copy.LocalFolderPath = string.IsNullOrWhiteSpace(copy.LocalFolderPath) ? root : copy.LocalFolderPath;
                jobs.Add(copy);
            }
        }

        foreach (var rule in config.SharedSyncRules)
        {
            if (string.IsNullOrWhiteSpace(rule.LocalFolderPath))
            {
                continue;
            }

            foreach (var target in rule.SyncTargets)
            {
                if (string.IsNullOrWhiteSpace(target.ProjectId) && string.IsNullOrWhiteSpace(target.ProjectName))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(target.RemoteFolderId) && string.IsNullOrWhiteSpace(target.RemoteFolderPath))
                {
                    continue;
                }

                jobs.Add(new SyncJobOptions
                {
                    JobId = MakeId("shared", rule.Name, target.ProjectId, target.ProjectName, target.RemoteFolderPath, target.RemoteFolderId),
                    ProjectName = target.ProjectName,
                    ProjectId = target.ProjectId.Trim(),
                    LocalProjectRoot = rule.LocalFolderPath.Trim(),
                    LocalFolderPath = rule.LocalFolderPath.Trim(),
                    RemoteFolderPath = RemotePath.Copy(target.RemoteFolderPath),
                    RemoteFolderId = target.RemoteFolderId?.Trim() ?? string.Empty,
                    SyncIntervalSeconds = rule.SyncIntervalSeconds < 15 ? 15 : rule.SyncIntervalSeconds,
                    Direction = rule.Direction
                });
            }
        }

        return jobs;
    }

    internal static bool IsComplete(SyncJobOptions job) =>
        job.HasProject
        && !string.IsNullOrWhiteSpace(job.LocalFolderPath)
        && (!string.IsNullOrWhiteSpace(job.EffectiveRemoteFolderId) || !string.IsNullOrWhiteSpace(job.RemoteFolderPath));

    private static void Normalize(ConnectorSyncConfig config)
    {
        config.ProjectProvisioning ??= new ProjectProvisioningOptions();
        config.SharedSyncRules ??= [];
        config.SyncJobs ??= [];
        if (string.IsNullOrWhiteSpace(config.ProjectProvisioning.TriggerFileName))
        {
            config.ProjectProvisioning.TriggerFileName = "_trimble_sync.json";
        }

        if (string.IsNullOrWhiteSpace(config.ProjectProvisioning.DefaultRegion))
        {
            config.ProjectProvisioning.DefaultRegion = "europe";
        }

        foreach (var job in config.SyncJobs)
        {
            NormalizeJob(job);
        }
    }

    private static void NormalizeJob(SyncJobOptions job)
    {
        job.FolderMappings ??= [];
        if (string.IsNullOrWhiteSpace(job.LocalProjectRoot) && !string.IsNullOrWhiteSpace(job.LocalFolderPath))
        {
            job.LocalProjectRoot = job.LocalFolderPath;
        }

        if (string.IsNullOrWhiteSpace(job.JobId)
            && (job.HasProject || !string.IsNullOrWhiteSpace(job.EffectiveLocalRoot)))
        {
            job.JobId = MakeId("job", job.ProjectId, job.ProjectName, job.EffectiveLocalRoot);
        }

        if (job.FolderMappings.Count == 0
            && (!string.IsNullOrWhiteSpace(job.EffectiveRemoteFolderId) || !string.IsNullOrWhiteSpace(job.RemoteFolderPath)))
        {
            job.FolderMappings.Add(new FolderMapping
            {
                LocalSubPath = string.Empty,
                RemoteFolderPath = RemotePath.Copy(job.RemoteFolderPath),
                RemoteFolderId = job.RemoteFolderId?.Trim() ?? string.Empty,
                Direction = job.Direction
            });
        }
    }

    private static string CombineLocal(string root, string? subPath)
    {
        if (string.IsNullOrWhiteSpace(subPath))
        {
            return root;
        }

        var parts = subPath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        return Path.GetFullPath(Path.Combine(new[] { root }.Concat(parts).ToArray()));
    }

    private static string MakeId(params string?[] parts)
    {
        var raw = string.Join('|', parts.Select(part => part?.Trim() ?? string.Empty));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..12].ToLowerInvariant();
    }

    private static ConnectorSyncConfig CloneConfig(ConnectorSyncConfig config) => new()
    {
        ProjectProvisioning = CloneProvisioning(config.ProjectProvisioning),
        SharedSyncRules = config.SharedSyncRules.Select(CloneRule).ToList(),
        SyncJobs = config.SyncJobs.Select(CloneJob).ToList()
    };

    private static ProjectProvisioningOptions CloneProvisioning(ProjectProvisioningOptions options) => new()
    {
        AutoProvisionOnFolderTrigger = options.AutoProvisionOnFolderTrigger,
        TriggerFileName = options.TriggerFileName,
        DefaultTemplateProjectId = options.DefaultTemplateProjectId,
        DefaultTemplateProjectName = options.DefaultTemplateProjectName,
        DefaultRegion = options.DefaultRegion,
        WatchRoot = options.WatchRoot
    };

    private static SharedSyncRule CloneRule(SharedSyncRule rule) => new()
    {
        Name = rule.Name,
        LocalFolderPath = rule.LocalFolderPath,
        Direction = rule.Direction,
        SyncIntervalSeconds = rule.SyncIntervalSeconds,
        SyncTargets = rule.SyncTargets.Select(target => new SyncTarget
        {
            ProjectName = target.ProjectName,
            ProjectId = target.ProjectId,
            RemoteFolderPath = target.RemoteFolderPath,
            RemoteFolderId = target.RemoteFolderId
        }).ToList()
    };

    private static SyncJobOptions CloneJob(SyncJobOptions job) => new()
    {
        JobId = job.JobId,
        ProjectName = job.ProjectName,
        ProjectId = job.ProjectId,
        LocalProjectRoot = job.LocalProjectRoot,
        LocalFolderPath = job.LocalFolderPath,
        RemoteFolderPath = job.RemoteFolderPath,
        RemoteFolderId = job.RemoteFolderId,
        SyncIntervalSeconds = job.SyncIntervalSeconds,
        Direction = job.Direction,
        Enabled = job.Enabled,
        FolderMappings = job.FolderMappings.Select(mapping => new FolderMapping
        {
            LocalSubPath = mapping.LocalSubPath,
            RemoteFolderPath = mapping.RemoteFolderPath,
            RemoteFolderId = mapping.RemoteFolderId,
            Direction = mapping.Direction
        }).ToList()
    };
}
