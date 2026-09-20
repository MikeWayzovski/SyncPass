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

    public SetupApi(
        AuthSetupHelper auth,
        ITrimbleApiClient api,
        SyncJobStore jobs,
        SyncEngine engine,
        SyncLogBuffer logs)
    {
        _auth = auth;
        _api = api;
        _jobs = jobs;
        _engine = engine;
        _logs = logs;
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
                    ProjectId = job.ProjectId,
                    RemoteFolderId = job.EffectiveRemoteFolderId,
                    LocalFolderPath = job.LocalFolderPath,
                    Direction = job.Direction.ToString(),
                    SyncIntervalSeconds = job.SyncIntervalSeconds,
                    State = configured ? "ready" : "idle"
                }).ToList(),
            Logs = _logs.Snapshot()
        };
    }

    public Task<(byte[] Data, string ContentType)?> GetAvatarAsync(CancellationToken cancellationToken) =>
        _api.DownloadUserThumbnailAsync(cancellationToken);

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
        string projectId,
        string? folderId,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticated();
        var resolvedFolderId = folderId;
        if (string.IsNullOrWhiteSpace(resolvedFolderId))
        {
            var project = await _api.GetProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
            resolvedFolderId = project.EffectiveRootId;
        }

        if (string.IsNullOrWhiteSpace(resolvedFolderId))
        {
            throw new InvalidOperationException("Could not resolve the project root folder.");
        }

        var folders = await _api.ListSubfoldersAsync(resolvedFolderId, cancellationToken).ConfigureAwait(false);
        return folders.Select(folder => new SetupFolderDto
        {
            Id = folder.Id,
            Name = folder.Name ?? folder.Id,
            ParentId = folder.ParentId ?? resolvedFolderId
        }).ToList();
    }

    public void Save(SaveSetupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectId)
            || string.IsNullOrWhiteSpace(request.RemoteFolderId)
            || string.IsNullOrWhiteSpace(request.LocalFolderPath))
        {
            throw new ArgumentException("projectId, remoteFolderId, and localFolderPath are required.");
        }

        if (!Enum.TryParse<SyncDirection>(request.Direction, ignoreCase: true, out var direction))
        {
            direction = SyncDirection.TwoWay;
        }

        var interval = request.SyncIntervalSeconds < 15 ? 15 : request.SyncIntervalSeconds;
        Directory.CreateDirectory(request.LocalFolderPath);

        _jobs.Save(new SyncJobOptions
        {
            ProjectId = request.ProjectId.Trim(),
            RemoteFolderId = request.RemoteFolderId.Trim(),
            LocalFolderPath = request.LocalFolderPath.Trim(),
            SyncIntervalSeconds = interval,
            Direction = direction
        });

        _engine.ReloadJobs();
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
