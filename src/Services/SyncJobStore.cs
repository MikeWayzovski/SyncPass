using System.Text.Json;
using System.Text.Json.Nodes;
using TrimbleConnector.Config;

namespace TrimbleConnector.Services;

public sealed class SyncJobStore
{
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SyncJobStore> _logger;
    private readonly object _gate = new();
    private List<SyncJobOptions> _jobs;

    public SyncJobStore(
        IHostEnvironment environment,
        IConfiguration configuration,
        ILogger<SyncJobStore> logger)
    {
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
        _jobs = LoadFromDisk();
    }

    public IReadOnlyList<SyncJobOptions> GetJobs()
    {
        lock (_gate)
        {
            return _jobs.Select(Clone).ToList();
        }
    }

    public bool HasConfiguredJob => GetJobs().Any(job =>
        !string.IsNullOrWhiteSpace(job.ProjectId)
        && !string.IsNullOrWhiteSpace(job.LocalFolderPath)
        && !string.IsNullOrWhiteSpace(job.EffectiveRemoteFolderId));

    public void Save(SyncJobOptions job)
    {
        ArgumentNullException.ThrowIfNull(job);

        lock (_gate)
        {
            _jobs = [Clone(job)];
            Directory.CreateDirectory(Path.GetDirectoryName(JobsPath)!);
            File.WriteAllText(JobsPath, JsonSerializer.Serialize(_jobs, JsonDefaults.Serializer));
            PatchAppSettings(_jobs);
        }

        _logger.LogInformation(
            "Saved sync job {ProjectId} -> {Folder} ({Local}).",
            job.ProjectId,
            job.EffectiveRemoteFolderId,
            job.LocalFolderPath);
    }

    private string JobsPath => Path.Combine(_environment.ContentRootPath, "data", "sync-jobs.json");

    private List<SyncJobOptions> LoadFromDisk()
    {
        try
        {
            if (File.Exists(JobsPath))
            {
                var json = File.ReadAllText(JobsPath);
                var fromFile = JsonSerializer.Deserialize<List<SyncJobOptions>>(json, JsonDefaults.Serializer);
                if (fromFile is { Count: > 0 })
                {
                    return fromFile;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read data/sync-jobs.json.");
        }

        return _configuration.GetSection("SyncJobs").Get<List<SyncJobOptions>>() ?? [];
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

    private static SyncJobOptions Clone(SyncJobOptions job) => new()
    {
        ProjectId = job.ProjectId,
        LocalFolderPath = job.LocalFolderPath,
        RemoteFolderPath = job.RemoteFolderPath,
        RemoteFolderId = job.RemoteFolderId,
        SyncIntervalSeconds = job.SyncIntervalSeconds,
        Direction = job.Direction
    };
}
