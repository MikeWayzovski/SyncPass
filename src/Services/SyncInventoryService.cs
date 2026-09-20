using TrimbleConnector.Config;
using TrimbleConnector.Models;

namespace TrimbleConnector.Services;

public sealed class SyncInventoryService
{
    private const int MaxListedItems = 200;

    private readonly ITrimbleApiClient _api;
    private readonly SyncStateRepository _states;
    private readonly ILogger<SyncInventoryService> _logger;

    public SyncInventoryService(
        ITrimbleApiClient api,
        SyncStateRepository states,
        ILogger<SyncInventoryService> logger)
    {
        _api = api;
        _states = states;
        _logger = logger;
    }

    public LocalTreeResponse ScanLocal(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new LocalTreeResponse { Exists = false, Error = "localFolderPath is required." };
        }

        var full = Path.GetFullPath(path.Trim());
        if (!Directory.Exists(full))
        {
            return new LocalTreeResponse
            {
                Exists = false,
                Path = full,
                Error = "De lokale map bestaat niet."
            };
        }

        var folders = new List<LocalFolderDto>
        {
            new()
            {
                Name = Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                RelativePath = string.Empty,
                FileCount = CountFiles(full, recursive: false)
            }
        };

        foreach (var directory in Directory.EnumerateDirectories(full))
        {
            if (LocalFileWatcher.ShouldIgnore(directory))
            {
                continue;
            }

            folders.Add(new LocalFolderDto
            {
                Name = Path.GetFileName(directory),
                RelativePath = Path.GetFileName(directory),
                FileCount = CountFiles(directory, recursive: true)
            });
        }

        return new LocalTreeResponse
        {
            Exists = true,
            Path = full,
            Folders = folders
        };
    }

    public async Task<InventoryResponse> BuildAsync(InventoryRequest request, CancellationToken cancellationToken)
    {
        EnsureAuthenticatedPath(request);
        var project = await _api.ResolveProjectAsync(request.ProjectId, request.ProjectName, cancellationToken)
            .ConfigureAwait(false);
        var root = Path.GetFullPath(request.LocalFolderPath.Trim());
        if (!Directory.Exists(root))
        {
            throw new ArgumentException("De lokale map bestaat niet.");
        }

        var remoteRoot = RemotePath.Copy(request.RemoteFolderPath);
        var mappings = request.FolderMappings is { Count: > 0 }
            ? request.FolderMappings
            : [new FolderMapping
            {
                LocalSubPath = string.Empty,
                RemoteFolderPath = string.IsNullOrWhiteSpace(remoteRoot) ? "/" : remoteRoot,
                Direction = SyncDirection.LocalToCloud
            }];

        var items = new List<InventoryItemDto>();
        foreach (var mapping in mappings)
        {
            cancellationToken.ThrowIfCancellationRequested();
            items.AddRange(await ScanMappingAsync(project, root, mapping, cancellationToken).ConfigureAwait(false));
        }

        var distinct = items
            .GroupBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        return new InventoryResponse
        {
            ProjectName = project.Name ?? request.ProjectName,
            UploadCount = distinct.Count(item => item.Action == "upload"),
            DownloadCount = distinct.Count(item => item.Action == "download"),
            SyncedCount = distinct.Count(item => item.Action == "synced"),
            Items = distinct
                .OrderBy(item => item.Action == "synced" ? 2 : item.Action == "download" ? 1 : 0)
                .ThenBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
                .Take(MaxListedItems)
                .ToList()
        };
    }

    private async Task<List<InventoryItemDto>> ScanMappingAsync(
        ConnectProject project,
        string localRoot,
        FolderMapping mapping,
        CancellationToken cancellationToken)
    {
        var localFolder = CombineLocal(localRoot, mapping.LocalSubPath);
        var remotePath = string.IsNullOrWhiteSpace(mapping.RemoteFolderPath)
            ? "/"
            : RemotePath.Normalize(mapping.RemoteFolderPath);
        var canPull = mapping.Direction is SyncDirection.TwoWay or SyncDirection.CloudToLocal;
        var remoteByPath = await LoadRemoteFilesAsync(project, remotePath, cancellationToken).ConfigureAwait(false);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<InventoryItemDto>();
        var folderLabel = string.IsNullOrWhiteSpace(mapping.LocalSubPath) ? "(projectroot)" : mapping.LocalSubPath;

        if (Directory.Exists(localFolder))
        {
            foreach (var localPath in EnumerateLocalFiles(localFolder))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = SyncStateRepository.Normalize(Path.GetRelativePath(localFolder, localPath));
                if (string.IsNullOrWhiteSpace(relative))
                {
                    continue;
                }

                seen.Add(relative);
                remoteByPath.TryGetValue(relative, out var remote);
                var action = await ClassifyLocalAsync(project.Id, localPath, relative, remote, cancellationToken)
                    .ConfigureAwait(false);
                results.Add(Item(relative, folderLabel, action));
            }
        }

        if (canPull)
        {
            foreach (var remote in remoteByPath.Values)
            {
                if (seen.Contains(remote.RelativePath))
                {
                    continue;
                }

                results.Add(Item(remote.RelativePath, folderLabel, "download"));
            }
        }

        return results;
    }

    private async Task<string> ClassifyLocalAsync(
        string projectId,
        string localPath,
        string relative,
        RemoteFile? remote,
        CancellationToken cancellationToken)
    {
        var stored = _states.Get(projectId, relative);
        if (stored is not null)
        {
            var localHash = await ChecksumService.ComputeSha256Async(localPath, cancellationToken).ConfigureAwait(false);
            var localChanged = !ChecksumService.EqualsOrdinalIgnoreCase(stored.LocalHash, localHash);
            var remoteChanged = remote is not null
                && !string.IsNullOrWhiteSpace(remote.VersionId)
                && !string.IsNullOrWhiteSpace(stored.TrimbleVersionId)
                && !string.Equals(remote.VersionId, stored.TrimbleVersionId, StringComparison.OrdinalIgnoreCase);
            if (!localChanged && !remoteChanged)
            {
                return "synced";
            }

            return localChanged ? "upload" : "download";
        }

        if (remote is null)
        {
            return "upload";
        }

        if (!string.IsNullOrWhiteSpace(remote.Hash))
        {
            var md5 = await ChecksumService.ComputeMd5Async(localPath, cancellationToken).ConfigureAwait(false);
            if (ChecksumService.EqualsOrdinalIgnoreCase(md5, remote.Hash))
            {
                return "synced";
            }
        }

        return "upload";
    }

    private async Task<Dictionary<string, RemoteFile>> LoadRemoteFilesAsync(
        ConnectProject project,
        string remotePath,
        CancellationToken cancellationToken)
    {
        var files = new Dictionary<string, RemoteFile>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var objects = await TryLoadSyncObjectsAsync(project.Id, cancellationToken).ConfigureAwait(false);
            if (objects is { Count: > 0 })
            {
                var prefix = RemotePath.Normalize(remotePath);
                foreach (var item in objects.Where(entry => entry.IsFile && !entry.IsRemoved))
                {
                    var path = string.IsNullOrWhiteSpace(item.Path) ? item.Name ?? string.Empty : item.Path;
                    var normalized = RemotePath.Normalize(path);
                    if (prefix != "/"
                        && !normalized.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                        && !normalized.StartsWith(prefix.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var relative = prefix == "/"
                        ? normalized.TrimStart('/')
                        : normalized[prefix.Length..].TrimStart('/');
                    if (string.IsNullOrWhiteSpace(relative))
                    {
                        continue;
                    }

                    files[relative] = new RemoteFile(item.Id, relative, item.VersionId, item.Hash);
                }

                return files;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Object Sync inventory fallback for {ProjectId}.", project.Id);
        }

        var folderId = await _api.TryResolveFolderAsync(project.Id, remotePath, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(folderId))
        {
            return files;
        }

        var listed = await _api.ListFolderFilesRecursiveAsync(folderId, cancellationToken).ConfigureAwait(false);
        foreach (var file in listed)
        {
            var relative = SyncStateRepository.Normalize(file.Path ?? file.Name ?? string.Empty);
            if (string.IsNullOrWhiteSpace(relative))
            {
                continue;
            }

            files[relative] = new RemoteFile(file.Id, relative, file.VersionId, file.Hash);
        }

        return files;
    }

    private async Task<IReadOnlyList<SyncObject>?> TryLoadSyncObjectsAsync(
        string projectId,
        CancellationToken cancellationToken)
    {
        try
        {
            var status = await _api.GetProjectSyncStatusAsync(projectId, cancellationToken).ConfigureAwait(false);
            var cursor = status.EffectiveCursor;
            if (string.IsNullOrWhiteSpace(cursor))
            {
                return null;
            }

            return await _api.GetProjectSyncObjectsAsync(projectId, cursor, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private static InventoryItemDto Item(string relative, string folder, string action) => new()
    {
        RelativePath = relative,
        Folder = folder,
        Action = action,
        Label = action switch
        {
            "upload" => "Uploaden",
            "download" => "Downloaden",
            _ => "Gesynchroniseerd"
        }
    };

    private static string CombineLocal(string root, string? subPath)
    {
        if (string.IsNullOrWhiteSpace(subPath))
        {
            return root;
        }

        var parts = subPath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        return Path.GetFullPath(Path.Combine(new[] { root }.Concat(parts).ToArray()));
    }

    private static IEnumerable<string> EnumerateLocalFiles(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !LocalFileWatcher.ShouldIgnore(path) && !ProjectProvisioningService.IsTriggerFile(path));

    private static int CountFiles(string root, bool recursive)
    {
        try
        {
            var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            return Directory.EnumerateFiles(root, "*", option)
                .Count(path => !LocalFileWatcher.ShouldIgnore(path) && !ProjectProvisioningService.IsTriggerFile(path));
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static void EnsureAuthenticatedPath(InventoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.LocalFolderPath))
        {
            throw new ArgumentException("localFolderPath is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ProjectId) && string.IsNullOrWhiteSpace(request.ProjectName))
        {
            throw new ArgumentException("projectName is required.");
        }
    }

    private sealed record RemoteFile(string Id, string RelativePath, string? VersionId, string? Hash);
}
