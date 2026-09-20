using System.Text.Json;
using Microsoft.Extensions.Options;
using TrimbleConnector.Config;
using TrimbleConnector.Models;

namespace TrimbleConnector.Services;

/// <summary>
/// Coordinates local file events and Trimble Connect polling. The local network
/// folder is the single source of truth. SHA-256 hashes and Trimble version ids
/// are stored in SQLite so unchanged files are not transferred again.
/// </summary>
public sealed class SyncEngine
{
    private readonly ITrimbleApiClient _api;
    private readonly ILocalFileWatcher _watcher;
    private readonly IOptionsMonitor<TrimbleConnectOptions> _connectOptions;
    private readonly SyncJobStore _jobs;
    private readonly SyncLogBuffer _logs;
    private readonly SyncStateRepository _states;
    private readonly ProjectProvisioningService _provisioning;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<SyncEngine> _logger;
    private readonly object _statusLock = new();
    private List<SetupJobStatus> _status = [];
    private CancellationTokenSource? _reloadCts;

    public SyncEngine(
        ITrimbleApiClient api,
        ILocalFileWatcher watcher,
        IOptionsMonitor<TrimbleConnectOptions> connectOptions,
        SyncJobStore jobs,
        SyncLogBuffer logs,
        SyncStateRepository states,
        ProjectProvisioningService provisioning,
        IHostEnvironment environment,
        ILogger<SyncEngine> logger)
    {
        _api = api;
        _watcher = watcher;
        _connectOptions = connectOptions;
        _jobs = jobs;
        _logs = logs;
        _states = states;
        _provisioning = provisioning;
        _environment = environment;
        _logger = logger;
    }

    public IReadOnlyList<SetupJobStatus> CurrentJobs
    {
        get
        {
            lock (_statusLock)
            {
                return _status.ToList();
            }
        }
    }

    public void ReloadJobs()
    {
        _logs.Add("Configuration updated. Reloading sync jobs.");
        _reloadCts?.Cancel();
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using var reload = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _reloadCts = reload;

            var contexts = BuildContexts();
            if (contexts.Count == 0)
            {
                _logs.Add("Waiting for a saved sync job and Trimble login.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), reload.Token).ConfigureAwait(false);
                    continue;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    continue;
                }
            }

            try
            {
                await RunCycleLoopAsync(contexts, reload.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logs.Add("Sync jobs reloaded.");
            }
        }
    }

    private List<JobContext> BuildContexts()
    {
        var contexts = new List<JobContext>();
        var snapshot = new List<SetupJobStatus>();

        foreach (var job in _jobs.GetJobs())
        {
            if (!Validate(job))
            {
                continue;
            }

            if (!job.Enabled)
            {
                snapshot.Add(ToStatus(job, "paused"));
                continue;
            }

            Directory.CreateDirectory(job.LocalFolderPath);
            var context = new JobContext(job, LoadState(job));

            if (job.Direction is SyncDirection.TwoWay or SyncDirection.LocalToCloud)
            {
                _watcher.Watch(job.LocalFolderPath);
            }

            if (!string.IsNullOrWhiteSpace(job.LocalProjectRoot)
                && !string.Equals(job.LocalProjectRoot, job.LocalFolderPath, StringComparison.OrdinalIgnoreCase))
            {
                _watcher.Watch(job.LocalProjectRoot);
            }

            contexts.Add(context);
            snapshot.Add(ToStatus(job, "ready"));
            _logger.LogInformation(
                "Registered sync job {JobId} {Project} ({Direction}) {Local} <-> {RemotePath}.",
                job.JobId,
                string.IsNullOrWhiteSpace(job.ProjectName) ? job.ProjectId : job.ProjectName,
                job.Direction,
                job.LocalFolderPath,
                job.EffectiveRemotePath);
            _logs.Add($"Sync job ready for {job.ProjectName ?? job.ProjectId} ({job.EffectiveRemotePath}).");
        }

        var watchRoot = _jobs.GetConfig().ProjectProvisioning.WatchRoot;
        if (!string.IsNullOrWhiteSpace(watchRoot))
        {
            Directory.CreateDirectory(watchRoot);
            _watcher.Watch(watchRoot);
        }

        lock (_statusLock)
        {
            _status = snapshot;
        }

        return contexts;
    }

    private async Task RunCycleLoopAsync(List<JobContext> contexts, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (await _provisioning.ScanTriggersAsync(cancellationToken).ConfigureAwait(false))
            {
                ReloadJobs();
                cancellationToken.ThrowIfCancellationRequested();
            }

            var wait = TimeSpan.FromSeconds(300);

            foreach (var context in contexts)
            {
                try
                {
                    UpdateStatus(context.Job, "syncing");
                    await SyncJobAsync(context, cancellationToken).ConfigureAwait(false);
                    SaveState(context);
                    UpdateStatus(context.Job, "idle");
                    _logs.Add($"Sync cycle finished for {context.Job.ProjectId}.");
                    if (context.Job.Interval < wait)
                    {
                        wait = context.Job.Interval;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    UpdateStatus(context.Job, "error");
                    _logs.Add($"Sync job {context.Job.ProjectId} failed: {ex.Message}");
                    _states.LogActivity(context.Job.ProjectId, "ERROR", string.Empty, ex.Message);
                    _logger.LogError(
                        ex,
                        "Sync job {ProjectId} failed. It will be retried on the next interval.",
                        context.Job.ProjectId);
                }
            }

            await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SyncJobAsync(JobContext context, CancellationToken cancellationToken)
    {
        var job = context.Job;
        var project = await _api.ResolveProjectAsync(job.ProjectId, job.ProjectName, cancellationToken)
            .ConfigureAwait(false);
        job.ProjectId = project.Id;
        job.ProjectName = project.Name ?? job.ProjectName;

        _logger.LogDebug(
            "Starting sync cycle for {Project} against EU API {Api} folder {Folder}.",
            string.IsNullOrWhiteSpace(job.ProjectName) ? job.ProjectId : job.ProjectName,
            _connectOptions.CurrentValue.EffectiveApiBaseUrl,
            job.EffectiveRemotePath);

        var remoteFolderId = await ResolveRemoteFolderAsync(job, cancellationToken).ConfigureAwait(false);

        var changes = _watcher.Drain();
        foreach (var change in changes.Where(item => ProjectProvisioningService.IsTriggerFile(item.FullPath)))
        {
            if (await _provisioning.ProcessTriggerFileAsync(change.FullPath, cancellationToken).ConfigureAwait(false))
            {
                ReloadJobs();
            }
        }

        var remoteFiles = await LoadRemoteFilesAsync(context, remoteFolderId, cancellationToken)
            .ConfigureAwait(false);
        var remoteByPath = remoteFiles
            .Where(item => !string.IsNullOrWhiteSpace(item.RelativePath))
            .GroupBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deletedLocally = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var localPath in EnumerateLocalFiles(job.LocalFolderPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = SyncStateRepository.Normalize(
                Path.GetRelativePath(job.LocalFolderPath, localPath));
            if (string.IsNullOrWhiteSpace(relative))
            {
                continue;
            }

            seen.Add(relative);
            remoteByPath.TryGetValue(relative, out var remote);
            await SyncLocalFileAsync(context, remoteFolderId, localPath, relative, remote, cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var stored in _states.GetAll(StateScope(job)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (seen.Contains(stored.RelativePath))
            {
                continue;
            }

            var localPath = Path.Combine(
                job.LocalFolderPath,
                stored.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(localPath))
            {
                continue;
            }

            if (job.Direction is SyncDirection.TwoWay or SyncDirection.LocalToCloud
                && !string.IsNullOrWhiteSpace(stored.TrimbleFileId))
            {
                await _api.DeleteFileAsync(stored.TrimbleFileId, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(
                    "Deleted remote file {FileId} because local {Path} was removed (local is source of truth).",
                    stored.TrimbleFileId,
                    stored.RelativePath);
                _logs.Add($"Deleted remote copy of {stored.RelativePath}.");
                _states.LogActivity(job.ProjectId, "DELETE", stored.RelativePath, $"Deleted remote copy of {stored.RelativePath}.");
            }

            _states.Delete(StateScope(job), stored.RelativePath);
            deletedLocally.Add(stored.RelativePath);
        }

        if (job.Direction is SyncDirection.TwoWay or SyncDirection.CloudToLocal)
        {
            foreach (var remote in remoteByPath.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (seen.Contains(remote.RelativePath)
                    || deletedLocally.Contains(remote.RelativePath)
                    || _states.Get(StateScope(job), remote.RelativePath) is not null)
                {
                    continue;
                }

                await DownloadRemoteAsync(context, remote, cancellationToken).ConfigureAwait(false);
            }
        }

        if (!string.IsNullOrWhiteSpace(context.PendingCursor))
        {
            context.State.Cursor = context.PendingCursor;
        }
    }

    private async Task SyncLocalFileAsync(
        JobContext context,
        string remoteFolderId,
        string localPath,
        string relative,
        RemoteFile? remote,
        CancellationToken cancellationToken)
    {
        if (!await LocalFileWatcher.WaitForUnlockAsync(localPath, cancellationToken).ConfigureAwait(false))
        {
            _logger.LogWarning("Skipped {Path}; AutoCAD/Revit still holds the file lock.", localPath);
            return;
        }

        var localHash = await ChecksumService.ComputeSha256Async(localPath, cancellationToken)
            .ConfigureAwait(false);
        var stored = _states.Get(StateScope(context.Job), relative);
        var localChanged = stored is null
            || !ChecksumService.EqualsOrdinalIgnoreCase(stored.LocalHash, localHash);
        var remoteChanged = RemoteVersionChanged(stored, remote);

        var canPush = context.Job.Direction is SyncDirection.TwoWay or SyncDirection.LocalToCloud;
        var canPull = context.Job.Direction is SyncDirection.TwoWay or SyncDirection.CloudToLocal;

        if (stored is not null && !localChanged && !remoteChanged)
        {
            _logger.LogInformation("Skipping {FileName}, already in sync.", Path.GetFileName(localPath));
            return;
        }

        if (stored is null)
        {
            if (!canPush)
            {
                return;
            }

            _logger.LogInformation("New local file {Path}; uploading to Trimble Connect.", relative);
            await UploadLocalAsync(context, remoteFolderId, localPath, relative, existingFileId: null, localHash, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (localChanged && remoteChanged)
        {
            _logger.LogWarning(
                "Conflict on {Path}: local and Trimble both changed. Local network wins; backing up the cloud version.",
                relative);
            _logs.Add($"Conflict on {relative}: local file kept, cloud copy backed up.");
            _states.LogActivity(context.Job.ProjectId, "CONFLICT", relative, "Local file kept, cloud copy backed up.");
            if (canPull)
            {
                await BackupRemoteVersionAsync(context, remote!, cancellationToken).ConfigureAwait(false);
            }

            if (canPush)
            {
                await UploadLocalAsync(context, remoteFolderId, localPath, relative, stored.TrimbleFileId ?? remote?.Id, localHash, cancellationToken)
                    .ConfigureAwait(false);
            }

            return;
        }

        if (localChanged)
        {
            if (!canPush)
            {
                return;
            }

            _logger.LogInformation("Local file {Path} changed; uploading a new version.", relative);
            await UploadLocalAsync(context, remoteFolderId, localPath, relative, stored.TrimbleFileId ?? remote?.Id, localHash, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (remoteChanged && remote is not null && canPull)
        {
            _logger.LogInformation(
                "Trimble version of {Path} changed while local hash is unchanged; downloading.",
                relative);
            await DownloadRemoteAsync(context, remote, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (remote is null && canPush)
        {
            _logger.LogInformation(
                "Remote copy of {Path} is missing; re-uploading from local (source of truth).",
                relative);
            await UploadLocalAsync(context, remoteFolderId, localPath, relative, existingFileId: null, localHash, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task UploadLocalAsync(
        JobContext context,
        string remoteFolderId,
        string localPath,
        string relative,
        string? existingFileId,
        string localHash,
        CancellationToken cancellationToken)
    {
        var parentId = await ResolveParentFolderAsync(context.Job, remoteFolderId, relative, cancellationToken)
            .ConfigureAwait(false);
        var length = new FileInfo(localPath).Length;
        if (length <= 0)
        {
            _logger.LogWarning("Skipped {Path}; Trimble Connect rejects zero-byte initiate payloads.", relative);
            _logs.Add($"Skipped {relative}: empty file.");
            return;
        }

        var uploaded = await _api.UploadFileAsync(
                context.Job.ProjectId,
                parentId,
                localPath,
                existingFileId,
                cancellationToken)
            .ConfigureAwait(false);

        SaveFileState(
            StateScope(context.Job),
            relative,
            localPath,
            localHash,
            uploaded.Id,
            uploaded.VersionId);
        _logs.Add($"Uploaded {relative}.");
        _states.LogActivity(context.Job.ProjectId, "UPLOAD", relative, $"Uploaded {relative}.");
    }

    private async Task DownloadRemoteAsync(
        JobContext context,
        RemoteFile remote,
        CancellationToken cancellationToken)
    {
        var localPath = Path.Combine(
            context.Job.LocalFolderPath,
            remote.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
        await _api.DownloadFileAsync(remote.Id, localPath, cancellationToken).ConfigureAwait(false);

        if (!await LocalFileWatcher.WaitForUnlockAsync(localPath, cancellationToken).ConfigureAwait(false))
        {
            _logger.LogWarning("Downloaded {Path} but could not hash it; file is locked.", localPath);
            return;
        }

        var localHash = await ChecksumService.ComputeSha256Async(localPath, cancellationToken)
            .ConfigureAwait(false);
        SaveFileState(
            StateScope(context.Job),
            remote.RelativePath,
            localPath,
            localHash,
            remote.Id,
            remote.VersionId);
        _logs.Add($"Downloaded {remote.RelativePath}.");
        _states.LogActivity(context.Job.ProjectId, "DOWNLOAD", remote.RelativePath, $"Downloaded {remote.RelativePath}.");
    }

    private async Task BackupRemoteVersionAsync(
        JobContext context,
        RemoteFile remote,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(
            Path.Combine(context.Job.LocalFolderPath, remote.RelativePath.Replace('/', Path.DirectorySeparatorChar)))
            ?? context.Job.LocalFolderPath;
        var name = Path.GetFileNameWithoutExtension(remote.RelativePath);
        var extension = Path.GetExtension(remote.RelativePath);
        var stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
        var backupPath = Path.Combine(directory, $"{name}.conflict-{stamp}{extension}");
        Directory.CreateDirectory(directory);
        await _api.DownloadFileAsync(remote.Id, backupPath, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Wrote Trimble conflict backup {Path}.", backupPath);
    }

    private void SaveFileState(
        string projectId,
        string relative,
        string localPath,
        string localHash,
        string? trimbleFileId,
        string? trimbleVersionId)
    {
        _states.Upsert(new FileState
        {
            ProjectId = projectId,
            RelativePath = relative,
            LocalHash = localHash,
            LocalLastWriteTimeUtc = File.GetLastWriteTimeUtc(localPath),
            TrimbleFileId = trimbleFileId,
            TrimbleVersionId = trimbleVersionId,
            LastSyncedAtUtc = DateTime.UtcNow
        });
    }

    private static bool RemoteVersionChanged(FileState? stored, RemoteFile? remote)
    {
        if (stored is null || remote is null)
        {
            return remote is not null && stored is not null;
        }

        if (!string.IsNullOrWhiteSpace(remote.Id)
            && !string.IsNullOrWhiteSpace(stored.TrimbleFileId)
            && !string.Equals(remote.Id, stored.TrimbleFileId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(remote.VersionId) || string.IsNullOrWhiteSpace(stored.TrimbleVersionId))
        {
            return false;
        }

        return !string.Equals(remote.VersionId, stored.TrimbleVersionId, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<List<RemoteFile>> LoadRemoteFilesAsync(
        JobContext context,
        string remoteFolderId,
        CancellationToken cancellationToken)
    {
        var status = await _api.GetProjectSyncStatusAsync(context.Job.ProjectId, cancellationToken)
            .ConfigureAwait(false);
        context.PendingCursor = status.EffectiveCursor;

        var scopedFolderId = context.Job.EffectiveRemoteFolderId ?? remoteFolderId;
        List<SyncObject> relevant;

        if (!string.IsNullOrWhiteSpace(context.Job.EffectiveRemoteFolderId))
        {
            relevant = (await _api.ListFolderFilesRecursiveAsync(scopedFolderId, cancellationToken).ConfigureAwait(false))
                .Select(file => new SyncObject
                {
                    Id = file.Id,
                    Name = file.Name,
                    ParentId = file.ParentId ?? scopedFolderId,
                    Path = file.Path ?? file.Name,
                    Hash = file.Hash,
                    Size = file.Size,
                    VersionId = file.VersionId,
                    Type = "FILE"
                })
                .ToList();
        }
        else if (!string.IsNullOrWhiteSpace(context.State.Cursor))
        {
            var prefix = NormalizeRemotePath(context.Job.RemoteFolderPath);
            var objects = await _api.GetProjectSyncObjectsAsync(
                    context.Job.ProjectId,
                    context.State.Cursor,
                    cancellationToken)
                .ConfigureAwait(false);
            relevant = objects.Where(item => item.IsFile && IsUnderRemoteRoot(item, prefix)).ToList();
        }
        else
        {
            var prefix = NormalizeRemotePath(context.Job.RemoteFolderPath);
            relevant = (await _api.ListFolderFilesRecursiveAsync(remoteFolderId, cancellationToken).ConfigureAwait(false))
                .Select(file => new SyncObject
                {
                    Id = file.Id,
                    Name = file.Name,
                    ParentId = file.ParentId,
                    Path = string.IsNullOrWhiteSpace(file.Path)
                        ? $"{prefix.TrimEnd('/')}/{file.Name}"
                        : file.Path.StartsWith('/') ? file.Path : $"{prefix.TrimEnd('/')}/{file.Path.TrimStart('/')}",
                    Hash = file.Hash,
                    Size = file.Size,
                    VersionId = file.VersionId,
                    Type = "FILE"
                })
                .ToList();
        }

        var pathPrefix = string.IsNullOrWhiteSpace(context.Job.EffectiveRemoteFolderId)
            ? NormalizeRemotePath(context.Job.RemoteFolderPath)
            : string.Empty;

        var files = new List<RemoteFile>();
        foreach (var item in relevant)
        {
            if (item.IsRemoved)
            {
                continue;
            }

            var relative = string.IsNullOrWhiteSpace(pathPrefix)
                ? SyncStateRepository.Normalize(item.Path ?? item.Name ?? string.Empty)
                : SyncStateRepository.Normalize(ToRelativeLocalPath(item, pathPrefix));
            if (string.IsNullOrWhiteSpace(relative))
            {
                continue;
            }

            files.Add(new RemoteFile(item.Id, relative, item.VersionId, item.Hash));
        }

        return files;
    }

    private async Task<string> ResolveRemoteFolderAsync(SyncJobOptions job, CancellationToken cancellationToken)
    {
        if (!RemotePath.IsRoot(job.RemoteFolderPath))
        {
            var created = await _api.ResolveOrCreateFolderAsync(
                    job.ProjectId,
                    RemotePath.Normalize(job.RemoteFolderPath),
                    cancellationToken)
                .ConfigureAwait(false);
            job.RemoteFolderId = created;
            return created;
        }

        if (!string.IsNullOrWhiteSpace(job.EffectiveRemoteFolderId))
        {
            return job.EffectiveRemoteFolderId!;
        }

        var root = await _api.ResolveOrCreateFolderAsync(job.ProjectId, "/", cancellationToken).ConfigureAwait(false);
        job.RemoteFolderId = root;
        return root;
    }

    private async Task<string> ResolveParentFolderAsync(
        SyncJobOptions job,
        string remoteRootId,
        string relativeFilePath,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(relativeFilePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return remoteRootId;
        }

        var relativeDirectory = directory.Replace('\\', '/');
        if (!string.IsNullOrWhiteSpace(job.EffectiveRemoteFolderId))
        {
            return await _api.ResolveOrCreateFolderAsync(
                    job.ProjectId,
                    relativeDirectory,
                    job.EffectiveRemoteFolderId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var remotePath = $"{job.RemoteFolderPath.TrimEnd('/', '\\')}/{relativeDirectory}";
        return await _api.ResolveOrCreateFolderAsync(job.ProjectId, remotePath, cancellationToken).ConfigureAwait(false);
    }

    private void UpdateStatus(SyncJobOptions job, string state)
    {
        lock (_statusLock)
        {
            var match = _status.FirstOrDefault(item =>
                (!string.IsNullOrWhiteSpace(job.JobId) && item.JobId == job.JobId)
                || (item.ProjectId == job.ProjectId
                    && item.LocalFolderPath == job.LocalFolderPath
                    && item.RemoteFolderId == job.EffectiveRemoteFolderId));
            if (match is not null)
            {
                _status[_status.IndexOf(match)] = ToStatus(job, state);
            }
            else
            {
                _status.Add(ToStatus(job, state));
            }
        }
    }

    private static SetupJobStatus ToStatus(SyncJobOptions job, string state) => new()
    {
        JobId = job.JobId,
        ProjectName = job.ProjectName,
        ProjectId = job.ProjectId,
        RemoteFolderPath = RemotePath.IsRoot(job.RemoteFolderPath) ? job.RemoteFolderPath : job.EffectiveRemotePath,
        RemoteFolderId = job.EffectiveRemoteFolderId,
        LocalFolderPath = job.LocalFolderPath,
        Direction = job.Direction.ToString(),
        SyncIntervalSeconds = job.SyncIntervalSeconds,
        State = state
    };

    private bool Validate(SyncJobOptions job)
    {
        if (!job.HasProject || string.IsNullOrWhiteSpace(job.LocalFolderPath))
        {
            _logger.LogWarning("Skipping incomplete SyncJob. Project name/id and LocalFolderPath are required.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(job.EffectiveRemoteFolderId) && string.IsNullOrWhiteSpace(job.RemoteFolderPath))
        {
            _logger.LogWarning(
                "Skipping SyncJob {Project}. A remote folder path is required.",
                job.ProjectName ?? job.ProjectId);
            return false;
        }

        if (string.IsNullOrWhiteSpace(_connectOptions.CurrentValue.ClientId))
        {
            _logger.LogWarning("TrimbleConnect credentials are not configured.");
        }

        return true;
    }

    private string StatePath(SyncJobOptions job)
    {
        var key = string.IsNullOrWhiteSpace(job.JobId)
            ? job.ProjectId
            : job.JobId;
        var safeId = string.Concat(key.Where(char.IsLetterOrDigit));
        if (string.IsNullOrEmpty(safeId))
        {
            safeId = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key)))[..12];
        }

        return Path.Combine(_environment.ContentRootPath, "data", $"sync-state-{safeId}.json");
    }

    private static string StateScope(SyncJobOptions job) =>
        string.IsNullOrWhiteSpace(job.JobId) ? job.ProjectId : $"{job.ProjectId}::{job.JobId}";

    private SyncState LoadState(SyncJobOptions job)
    {
        var path = StatePath(job);
        try
        {
            if (!File.Exists(path))
            {
                return new SyncState();
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SyncState>(json, JsonDefaults.Serializer) ?? new SyncState();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load sync state for {ProjectId}. Starting a fresh cursor.", job.ProjectId);
            return new SyncState();
        }
    }

    private void SaveState(JobContext context)
    {
        var path = StatePath(context.Job);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(context.State, JsonDefaults.Serializer));
    }

    private static IEnumerable<string> EnumerateLocalFiles(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !LocalFileWatcher.ShouldIgnore(path) && !ProjectProvisioningService.IsTriggerFile(path));

    private static string NormalizeRemotePath(string path) =>
        "/" + string.Join('/', (path ?? "/").Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries));

    private static bool IsUnderRemoteRoot(SyncObject item, string remoteRoot)
    {
        if (string.IsNullOrWhiteSpace(item.Path))
        {
            return true;
        }

        var path = NormalizeRemotePath(item.Path);
        return path.Equals(remoteRoot, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(remoteRoot.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToRelativeLocalPath(SyncObject item, string remoteRoot)
    {
        if (!string.IsNullOrWhiteSpace(item.Path))
        {
            var path = NormalizeRemotePath(item.Path);
            var root = remoteRoot.TrimEnd('/');
            if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                var remainder = path[root.Length..].TrimStart('/');
                return remainder.Replace('/', Path.DirectorySeparatorChar);
            }
        }

        return item.Name ?? string.Empty;
    }

    private sealed class JobContext
    {
        public JobContext(SyncJobOptions job, SyncState state)
        {
            Job = job;
            State = state;
        }

        public SyncJobOptions Job { get; }

        public SyncState State { get; }

        public string? PendingCursor { get; set; }
    }

    private sealed record RemoteFile(string Id, string RelativePath, string? VersionId, string? Hash);
}
