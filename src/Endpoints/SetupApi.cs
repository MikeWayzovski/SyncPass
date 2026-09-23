using System.Text.Json;
using TrimbleConnector.Config;
using TrimbleConnector.Models;
using TrimbleConnector.Services;

namespace TrimbleConnector.Endpoints;

public sealed class SetupApi
{
    private readonly AuthSetupHelper _auth;
    private readonly ITrimbleApiClient _api;
    private readonly SyncJobStore _jobs;
    private readonly SyncEngine _engine;
    private readonly SyncLogBuffer _logs;
    private readonly SyncStateRepository _states;
    private readonly ProjectProvisioningService _provisioning;
    private readonly SyncInventoryService _inventory;

    public SetupApi(
        AuthSetupHelper auth,
        ITrimbleApiClient api,
        SyncJobStore jobs,
        SyncEngine engine,
        SyncLogBuffer logs,
        SyncStateRepository states,
        ProjectProvisioningService provisioning,
        SyncInventoryService inventory)
    {
        _auth = auth;
        _api = api;
        _jobs = jobs;
        _engine = engine;
        _logs = logs;
        _states = states;
        _provisioning = provisioning;
        _inventory = inventory;
    }

    public async Task<SetupStatusResponse> GetStatusAsync(CancellationToken cancellationToken)
    {
        ConnectUser? user = null;
        if (_auth.HasRefreshToken)
        {
            try
            {
                user = await _api.GetLoggedInUserAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        var configured = _jobs.HasConfiguredJob;
        var config = _jobs.GetConfig();
        return new SetupStatusResponse
        {
            Authenticated = _auth.HasRefreshToken,
            Configured = configured,
            UserName = user?.DisplayLabel,
            UserFirstName = user?.FirstName,
            UserLastName = user?.LastName,
            UserEmail = user?.Email,
            UserThumbnail = null,
            User = user is null
                ? null
                : new SetupUserDto
                {
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    HasImage = user.HasImage,
                    Thumbnail = null
                },
            Jobs = _engine.CurrentJobs.Count > 0
                ? _engine.CurrentJobs
                : _jobs.GetJobs().Select(job => new SetupJobStatus
                {
                    JobId = job.JobId,
                    ProjectName = job.ProjectName,
                    ProjectId = job.ProjectId,
                    RemoteFolderPath = RemotePath.IsRoot(job.RemoteFolderPath)
                        ? job.RemoteFolderPath
                        : job.EffectiveRemotePath,
                    RemoteFolderId = job.EffectiveRemoteFolderId,
                    LocalFolderPath = job.LocalFolderPath,
                    Direction = job.Direction.ToString(),
                    SyncIntervalSeconds = job.SyncIntervalSeconds,
                    State = configured ? "ready" : "idle"
                }).ToList(),
            ProjectCount = config.SyncJobs.Count,
            SharedRuleCount = config.SharedSyncRules.Count,
            Logs = _logs.Snapshot(),
            Version = global::TrimbleConnector.ProductInfo.DisplayVersion
        };
    }

    public Task<(byte[] Data, string ContentType)?> GetAvatarAsync(CancellationToken cancellationToken) =>
        _api.DownloadUserThumbnailAsync(cancellationToken);

    public IReadOnlyList<ActivityLog> GetRecentActivities() => _states.GetRecentActivities(50);

    public LoginUrlResponse GetLoginUrl() => new() { Url = _auth.BuildAuthorizeUrl() };

    public async Task<IReadOnlyList<SetupProjectDto>> GetProjectsAsync(CancellationToken cancellationToken)
    {
        EnsureAuthenticated();
        var projects = await _api.GetProjectsAsync(cancellationToken).ConfigureAwait(false);
        return projects.Select(project => new SetupProjectDto
        {
            Id = project.Id,
            Name = project.Name ?? project.Id,
            RootId = project.EffectiveRootId,
            Location = project.EffectiveLocation
        }).ToList();
    }

    public async Task<IReadOnlyList<SetupFolderDto>> GetFoldersAsync(
        string? projectId,
        string? projectName,
        string? folderId,
        string? parentPath,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticated();
        var project = await _api.ResolveProjectAsync(projectId, projectName, cancellationToken).ConfigureAwait(false);
        var resolvedFolderId = folderId;
        if (string.IsNullOrWhiteSpace(resolvedFolderId))
        {
            resolvedFolderId = project.EffectiveRootId;
        }

        if (string.IsNullOrWhiteSpace(resolvedFolderId))
        {
            throw new InvalidOperationException("Could not resolve the project root folder.");
        }

        var folders = await _api.ListSubfoldersAsync(resolvedFolderId, cancellationToken).ConfigureAwait(false);
        var basePath = RemotePath.Normalize(parentPath);
        return folders.Select(folder => new SetupFolderDto
        {
            Id = folder.Id,
            Name = folder.Name ?? folder.Id,
            Path = RemotePath.Combine(basePath, folder.Name),
            ParentId = folder.ParentId ?? resolvedFolderId
        }).ToList();
    }

    public ConnectorSyncConfig GetConfig() => _jobs.GetConfig();

    public void SaveConfig(ConnectorSyncConfig config)
    {
        _jobs.SaveConfig(config);
        _engine.ReloadJobs();
    }

    public async Task<ConnectProject> ProvisionAsync(ProvisionProjectRequest request, CancellationToken cancellationToken)
    {
        EnsureAuthenticated();
        var project = await _provisioning.ProvisionFromUiAsync(request, cancellationToken).ConfigureAwait(false);
        _engine.ReloadJobs();
        return project;
    }

    public SyncJobOptions Save(SaveSetupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectId))
        {
            throw new ArgumentException("projectId is required. Select a Trimble Connect project.");
        }

        if (string.IsNullOrWhiteSpace(request.LocalFolderPath))
        {
            throw new ArgumentException("localFolderPath is required.");
        }

        if (!Enum.TryParse<SyncDirection>(request.Direction, ignoreCase: true, out var direction))
        {
            direction = SyncDirection.TwoWay;
        }

        var interval = request.SyncIntervalSeconds < 15 ? 15 : request.SyncIntervalSeconds;
        Directory.CreateDirectory(request.LocalFolderPath);

        var mappings = request.FolderMappings is { Count: > 0 }
            ? request.FolderMappings
            : [new FolderMapping
            {
                LocalSubPath = string.Empty,
                RemoteFolderPath = RemotePath.Copy(request.RemoteFolderPath),
                RemoteFolderId = request.RemoteFolderId?.Trim() ?? string.Empty,
                Direction = direction
            }];

        if (!mappings.Any(mapping => mapping.HasRemoteTarget))
        {
            throw new ArgumentException("At least one remote folder path is required.");
        }

        foreach (var mapping in mappings)
        {
            mapping.RemoteFolderPath = RemotePath.Copy(mapping.RemoteFolderPath);
        }

        var job = new SyncJobOptions
        {
            ProjectName = request.ProjectName.Trim(),
            ProjectId = request.ProjectId?.Trim() ?? string.Empty,
            RemoteFolderPath = mappings[0].RemoteFolderPath,
            RemoteFolderId = mappings[0].RemoteFolderId,
            LocalProjectRoot = request.LocalFolderPath.Trim(),
            LocalFolderPath = request.LocalFolderPath.Trim(),
            SyncIntervalSeconds = interval,
            Direction = direction,
            FolderMappings = mappings,
            Enabled = request.Enabled
        };
        _jobs.UpsertProjectJob(job);
        _engine.ReloadJobs();
        return _jobs.GetConfig().SyncJobs.FirstOrDefault(item =>
                string.Equals(item.ProjectId, job.ProjectId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.EffectiveLocalRoot, job.EffectiveLocalRoot, StringComparison.OrdinalIgnoreCase))
            ?? job;
    }

    public LocalTreeResponse ScanLocalTree(string? path) => _inventory.ScanLocal(path);

    public BrowseResponse BrowseLocal(string? path) => _inventory.Browse(path);

    public async Task<SetupFolderDto> CreateFolderAsync(CreateFolderRequest request, CancellationToken cancellationToken)
    {
        EnsureAuthenticated();
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("folder name is required.");
        }

        var project = await _api.ResolveProjectAsync(request.ProjectId, request.ProjectName, cancellationToken)
            .ConfigureAwait(false);
        var path = RemotePath.Combine(request.ParentPath, request.Name.Trim());
        var id = await _api.ResolveOrCreateFolderAsync(project.Id, path, cancellationToken).ConfigureAwait(false);
        return new SetupFolderDto
        {
            Id = id,
            Name = request.Name.Trim(),
            Path = path
        };
    }

    public Task<InventoryResponse> InventoryAsync(InventoryRequest request, CancellationToken cancellationToken)
    {
        EnsureAuthenticated();
        return _inventory.BuildAsync(request, cancellationToken);
    }

    public SyncJobOptions Activate(ActivateJobRequest request)
    {
        var config = _jobs.GetConfig();
        var job = config.SyncJobs.FirstOrDefault(item =>
            (!string.IsNullOrWhiteSpace(request.JobId)
                && string.Equals(item.JobId, request.JobId, StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrWhiteSpace(request.ProjectId)
                && string.Equals(item.ProjectId, request.ProjectId, StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(request.LocalFolderPath)
                    || string.Equals(item.EffectiveLocalRoot, request.LocalFolderPath, StringComparison.OrdinalIgnoreCase))));
        if (job is null)
        {
            throw new ArgumentException("Sync job was not found.");
        }

        job.Enabled = true;
        _jobs.UpsertProjectJob(job);
        _engine.ReloadJobs();
        return job;
    }

    public async Task CompleteCallbackAsync(string code, CancellationToken cancellationToken) =>
        await _auth.CompleteAuthorizationAsync(code, cancellationToken).ConfigureAwait(false);

    public IReadOnlyList<OverviewJobDto> GetOverviewJobs()
    {
        var live = _engine.CurrentJobs;
        var activities = _states.GetRecentActivities(20);
        return _jobs.GetJobs().Select(job =>
        {
            var stats = _states.GetFileStats(StateScope(job));
            var liveState = live.FirstOrDefault(item =>
                (!string.IsNullOrWhiteSpace(job.JobId) && item.JobId == job.JobId)
                || (item.ProjectId == job.ProjectId && item.LocalFolderPath == job.LocalFolderPath));
            var status = MapStatus(job, liveState?.State);
            var lastActivity = activities.FirstOrDefault(item =>
                string.Equals(item.ProjectId, job.ProjectId, StringComparison.OrdinalIgnoreCase));
            var tags = job.Metadata?.Tags?.Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                ?? [];
            return new OverviewJobDto
            {
                JobId = job.JobId,
                ParentJobId = ParentJobId(job.JobId),
                ProjectName = job.ProjectName,
                ProjectId = job.ProjectId,
                LocalFolderPath = job.LocalFolderPath,
                RemoteProjectName = job.ProjectName,
                RemoteFolderName = RemoteFolderName(job.RemoteFolderPath),
                RemoteFolderPath = RemotePath.IsRoot(job.RemoteFolderPath) ? "/" : job.EffectiveRemotePath,
                RemoteFolderId = job.EffectiveRemoteFolderId,
                Direction = job.Direction.ToString(),
                SyncIntervalSeconds = job.SyncIntervalSeconds,
                Enabled = job.Enabled,
                SyncStatus = status,
                ErpProjectId = job.Metadata?.ErpProjectId,
                Description = job.Metadata?.Description,
                Tags = tags,
                LastSyncTime = stats.LastSyncedUtc.HasValue
                    ? new DateTimeOffset(DateTime.SpecifyKind(stats.LastSyncedUtc.Value, DateTimeKind.Utc))
                    : job.Metadata?.LastSyncTime,
                TotalFilesSynced = stats.FileCount,
                TotalSizeMb = LocalSizeMb(job.LocalFolderPath),
                LastActivity = lastActivity is null ? null : $"{lastActivity.Action} {lastActivity.FilePath}".Trim()
            };
        }).ToList();
    }

    public OverviewStatsDto GetOverviewStats()
    {
        var jobs = GetOverviewJobs();
        var files = _states.GetGlobalFileStats();
        var last = _states.GetRecentActivities(1).FirstOrDefault();
        return new OverviewStatsDto
        {
            TotalActiveMappings = jobs.Count(job => job.Enabled && job.SyncStatus != FolderSyncStatus.Paused),
            TotalMappings = jobs.Count,
            TotalSyncedFiles = files.FileCount,
            DiskUsageMb = Math.Round(jobs.Sum(job => job.TotalSizeMb), 2),
            LastActivity = last?.TimestampUtc is { Year: > 2000 } time
                ? new DateTimeOffset(DateTime.SpecifyKind(time, DateTimeKind.Utc))
                : files.LastSyncedUtc is { } synced
                    ? new DateTimeOffset(DateTime.SpecifyKind(synced, DateTimeKind.Utc))
                    : null,
            LastActivitySummary = last is null ? null : $"{last.Action} · {last.FilePath}".Trim(' ', '·')
        };
    }

    public OverviewJobDto ToggleOverviewJob(string jobId)
    {
        var target = RequireJob(jobId);
        var enabled = !target.Enabled;
        ApplyToSource(jobId, (parent, mapping, rule) =>
        {
            if (mapping is not null)
            {
                mapping.Enabled = enabled;
                if (parent is not null)
                {
                    parent.Enabled = enabled || parent.FolderMappings.Exists(item => item.Enabled);
                }
            }
            else if (parent is not null)
            {
                parent.Enabled = enabled;
            }
            else if (rule is not null)
            {
                // Shared rules stay enabled as a group; pause by clearing nothing here.
            }
        });
        _engine.ReloadJobs();
        return GetOverviewJobs().First(item => item.JobId == jobId);
    }

    public OverviewJobDto RequestOverviewSync(string jobId)
    {
        var job = RequireJob(jobId);
        if (!job.Enabled)
        {
            ApplyToSource(jobId, (parent, mapping, _) =>
            {
                if (mapping is not null)
                {
                    mapping.Enabled = true;
                }

                if (parent is not null)
                {
                    parent.Enabled = true;
                }
            });
            _engine.ReloadJobs();
        }

        _engine.RequestImmediateSync(jobId);
        return GetOverviewJobs().First(item => item.JobId == jobId);
    }

    public async Task<OverviewJobDto> UpdateOverviewMetadataAsync(
        OverviewMetadataUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.JobId))
        {
            throw new ArgumentException("jobId is required.");
        }

        var tags = (request.Tags ?? [])
            .Select(tag => tag.Trim())
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        ApplyToSource(request.JobId, (parent, mapping, _) =>
        {
            var metadata = mapping?.Metadata ?? parent?.Metadata ?? new FolderMetadata();
            metadata.ErpProjectId = string.IsNullOrWhiteSpace(request.ErpProjectId) ? null : request.ErpProjectId.Trim();
            metadata.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            metadata.Tags = tags;
            if (mapping is not null)
            {
                mapping.Metadata = metadata;
            }

            if (parent is not null)
            {
                if (request.SyncIntervalSeconds is >= 15)
                {
                    parent.SyncIntervalSeconds = request.SyncIntervalSeconds.Value;
                }

                if (mapping is null)
                {
                    parent.Metadata = metadata;
                }
            }
        });

        var updated = GetOverviewJobs().First(item => item.JobId == request.JobId);
        if (request.WriteConnectTags && !string.IsNullOrWhiteSpace(updated.ProjectId) && tags.Count > 0)
        {
            await WriteConnectTagsAsync(updated, tags, cancellationToken).ConfigureAwait(false);
        }

        _engine.ReloadJobs();
        return GetOverviewJobs().First(item => item.JobId == request.JobId);
    }

    public async Task<IReadOnlyList<ConnectTag>> GetConnectTagsAsync(string? projectId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            return [];
        }

        EnsureAuthenticated();
        return await _api.GetProjectTagsAsync(projectId, cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteConnectTagsAsync(
        OverviewJobDto job,
        IReadOnlyList<string> tags,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(job.RemoteFolderId))
        {
            return;
        }

        try
        {
            EnsureAuthenticated();
            var existing = await _api.GetProjectTagsAsync(job.ProjectId, cancellationToken).ConfigureAwait(false);
            foreach (var name in tags)
            {
                var match = existing.FirstOrDefault(tag =>
                    string.Equals(tag.DisplayName, name, StringComparison.OrdinalIgnoreCase));
                match ??= await _api.CreateTagAsync(job.ProjectId, name, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(match.Id))
                {
                    await _api.AssignTagToObjectsAsync(match.Id, [job.RemoteFolderId], "FOLDER", cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }
        catch (Exception)
        {
            _logs.Add($"Kon Trimble-tags niet schrijven voor {job.RemoteFolderPath}.");
        }
    }

    private SyncJobOptions RequireJob(string jobId)
    {
        var job = _jobs.GetJobs().FirstOrDefault(item =>
            string.Equals(item.JobId, jobId, StringComparison.OrdinalIgnoreCase));
        if (job is null)
        {
            throw new ArgumentException("Sync job was not found.");
        }

        return job;
    }

    private void ApplyToSource(
        string jobId,
        Action<SyncJobOptions?, FolderMapping?, SharedSyncRule?> mutate)
    {
        var config = _jobs.GetConfig();
        var expanded = _jobs.GetJobs().FirstOrDefault(item =>
            string.Equals(item.JobId, jobId, StringComparison.OrdinalIgnoreCase));

        foreach (var parent in config.SyncJobs)
        {
            if (string.Equals(parent.JobId, jobId, StringComparison.OrdinalIgnoreCase))
            {
                mutate(parent, null, null);
                _jobs.SaveConfig(config);
                return;
            }

            foreach (var mapping in parent.FolderMappings)
            {
                var remotePath = RemotePath.Copy(
                    !string.IsNullOrWhiteSpace(mapping.RemoteFolderPath) ? mapping.RemoteFolderPath : parent.RemoteFolderPath);
                var candidate = string.IsNullOrWhiteSpace(parent.JobId)
                    ? null
                    : $"{parent.JobId}:{mapping.LocalSubPath}:{remotePath}";
                var sameFolder = expanded is not null
                    && string.Equals(parent.ProjectId, expanded.ProjectId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(RemotePath.Copy(remotePath), expanded.EffectiveRemotePath, StringComparison.OrdinalIgnoreCase)
                    && LocalEndsWith(expanded.LocalFolderPath, mapping.LocalSubPath);
                if (string.Equals(candidate, jobId, StringComparison.OrdinalIgnoreCase) || sameFolder)
                {
                    mutate(parent, mapping, null);
                    _jobs.SaveConfig(config);
                    return;
                }
            }
        }

        foreach (var rule in config.SharedSyncRules)
        {
            if (expanded is not null
                && string.Equals(rule.LocalFolderPath, expanded.LocalFolderPath, StringComparison.OrdinalIgnoreCase)
                && rule.SyncTargets.Any(target =>
                    string.Equals(target.ProjectId, expanded.ProjectId, StringComparison.OrdinalIgnoreCase)))
            {
                mutate(null, null, rule);
                _jobs.SaveConfig(config);
                return;
            }
        }

        throw new ArgumentException("Sync job was not found.");
    }

    private static bool LocalEndsWith(string localPath, string? subPath)
    {
        if (string.IsNullOrWhiteSpace(subPath))
        {
            return true;
        }

        return localPath.Replace('\\', '/').TrimEnd('/')
            .EndsWith(subPath.Replace('\\', '/').Trim('/'), StringComparison.OrdinalIgnoreCase);
    }

    private static string MapStatus(SyncJobOptions job, string? liveState)
    {
        if (!job.Enabled || string.Equals(liveState, "paused", StringComparison.OrdinalIgnoreCase))
        {
            return FolderSyncStatus.Paused;
        }

        if (string.Equals(liveState, "error", StringComparison.OrdinalIgnoreCase))
        {
            return FolderSyncStatus.Error;
        }

        if (string.Equals(liveState, "syncing", StringComparison.OrdinalIgnoreCase))
        {
            return FolderSyncStatus.Syncing;
        }

        return FolderSyncStatus.Ok;
    }

    private static string ParentJobId(string jobId)
    {
        var separator = jobId.IndexOf(':');
        return separator > 0 ? jobId[..separator] : jobId;
    }

    private static string RemoteFolderName(string? path)
    {
        var normalized = RemotePath.Normalize(path);
        if (RemotePath.IsRoot(normalized))
        {
            return "/";
        }

        return normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "/";
    }

    private static string StateScope(SyncJobOptions job) =>
        string.IsNullOrWhiteSpace(job.JobId) ? job.ProjectId : $"{job.ProjectId}::{job.JobId}";

    private static double LocalSizeMb(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return 0;
        }

        try
        {
            long bytes = 0;
            var count = 0;
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    bytes += new FileInfo(file).Length;
                }
                catch (IOException)
                {
                }

                if (++count >= 8000)
                {
                    break;
                }
            }

            return Math.Round(bytes / 1024d / 1024d, 2);
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private void EnsureAuthenticated()
    {
        if (!_auth.HasRefreshToken)
        {
            throw new UnauthorizedAccessException("Not authenticated.");
        }
    }
}
