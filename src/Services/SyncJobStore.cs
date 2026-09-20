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
                || string.Equals(item.ProjectId, job.ProjectId, StringComparison.OrdinalIgnoreCase));

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
                    if (string.IsNullOrWhiteSpace(mapping.RemoteFolderId))
                    {
                        continue;
                    }

                    var local = CombineLocal(root, mapping.LocalSubPath);
                    jobs.Add(new SyncJobOptions
                    {
                        JobId = string.IsNullOrWhiteSpace(job.JobId)
                            ? MakeId("map", job.ProjectId, local, mapping.RemoteFolderId)
                            : $"{job.JobId}:{mapping.LocalSubPath}:{mapping.RemoteFolderId}",
                        ProjectId = job.ProjectId,
                        LocalProjectRoot = root,
                        LocalFolderPath = local,
                        RemoteFolderPath = job.RemoteFolderPath,
                        RemoteFolderId = mapping.RemoteFolderId.Trim(),
                        SyncIntervalSeconds = job.SyncIntervalSeconds,
                        Direction = mapping.Direction
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
                if (string.IsNullOrWhiteSpace(target.ProjectId) || string.IsNullOrWhiteSpace(target.RemoteFolderId))
                {
                    continue;
                }

                jobs.Add(new SyncJobOptions
                {
                    JobId = MakeId("shared", rule.Name, target.ProjectId, target.RemoteFolderId),
                    ProjectId = target.ProjectId.Trim(),
                    LocalProjectRoot = rule.LocalFolderPath.Trim(),
                    LocalFolderPath = rule.LocalFolderPath.Trim(),
                    RemoteFolderId = target.RemoteFolderId.Trim(),
                    SyncIntervalSeconds = rule.SyncIntervalSeconds < 15 ? 15 : rule.SyncIntervalSeconds,
                    Direction = rule.Direction
                });
            }
        }

        return jobs;
    }

    internal static bool IsComplete(SyncJobOptions job) =>
        !string.IsNullOrWhiteSpace(job.ProjectId)
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

        if (string.IsNullOrWhiteSpace(job.JobId) && !string.IsNullOrWhiteSpace(job.ProjectId))
        {
            job.JobId = MakeId("job", job.ProjectId, job.EffectiveLocalRoot);
        }

        if (job.FolderMappings.Count == 0
            && !string.IsNullOrWhiteSpace(job.EffectiveRemoteFolderId))
        {
            job.FolderMappings.Add(new FolderMapping
            {
                LocalSubPath = string.Empty,
                RemoteFolderId = job.RemoteFolderId.Trim(),
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
            ProjectId = target.ProjectId,
            RemoteFolderId = target.RemoteFolderId
        }).ToList()
    };

    private static SyncJobOptions CloneJob(SyncJobOptions job) => new()
    {
        JobId = job.JobId,
        ProjectId = job.ProjectId,
        LocalProjectRoot = job.LocalProjectRoot,
        LocalFolderPath = job.LocalFolderPath,
        RemoteFolderPath = job.RemoteFolderPath,
        RemoteFolderId = job.RemoteFolderId,
        SyncIntervalSeconds = job.SyncIntervalSeconds,
        Direction = job.Direction,
        FolderMappings = job.FolderMappings.Select(mapping => new FolderMapping
        {
            LocalSubPath = mapping.LocalSubPath,
            RemoteFolderId = mapping.RemoteFolderId,
            Direction = mapping.Direction
        }).ToList()
    };
}
