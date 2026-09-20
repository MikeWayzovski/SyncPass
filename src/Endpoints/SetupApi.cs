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
            UserThumbnail = user?.EffectiveThumbnail,
            User = user is null
                ? null
                : new SetupUserDto
                {
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    Thumbnail = user.EffectiveThumbnail
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

    private void EnsureAuthenticated()
    {
        if (!_auth.HasRefreshToken)
        {
            throw new UnauthorizedAccessException("Not authenticated.");
        }
    }
}
