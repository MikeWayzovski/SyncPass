using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrimbleConnector.Config;
using TrimbleConnector.Models;

namespace TrimbleConnector.Services;

public interface ITrimbleApiClient
{
    Task<ProjectSyncStatus> GetProjectSyncStatusAsync(string projectId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SyncObject>> GetProjectSyncObjectsAsync(
        string projectId,
        string statusCursor,
        CancellationToken cancellationToken);

    Task DownloadFileAsync(string fileId, string targetLocalPath, CancellationToken cancellationToken);

    Task<ConnectFile> UploadFileAsync(
        string projectId,
        string parentFolderId,
        string localFilePath,
        CancellationToken cancellationToken);

    Task<ConnectFile> UploadFileAsync(
        string projectId,
        string parentFolderId,
        string localFilePath,
        string? existingFileId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ConnectFile>> ListFolderFilesRecursiveAsync(
        string folderId,
        CancellationToken cancellationToken);

    Task<ConnectProject> GetProjectAsync(string projectId, CancellationToken cancellationToken);

    Task<ConnectProject> ResolveProjectAsync(string? projectId, string? projectName, CancellationToken cancellationToken);

    Task<string> ResolveOrCreateFolderAsync(
        string projectId,
        string remoteFolderPath,
        CancellationToken cancellationToken);

    Task<string> ResolveOrCreateFolderAsync(
        string projectId,
        string remoteFolderPath,
        string? startFolderId,
        CancellationToken cancellationToken);

    Task<ConnectFolder> CreateFolderAsync(
        string projectId,
        string parentFolderId,
        string name,
        CancellationToken cancellationToken);

    Task<string?> TryResolveFolderAsync(
        string projectId,
        string remoteFolderPath,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ConnectFile>> ListFolderFilesAsync(string folderId, CancellationToken cancellationToken);

    Task DeleteFileAsync(string fileId, CancellationToken cancellationToken);

    Task<ConnectUser?> GetCurrentUserAsync(CancellationToken cancellationToken);

    Task<ConnectUser?> GetLoggedInUserAsync(CancellationToken cancellationToken);

    Task<(byte[] Data, string ContentType)?> DownloadUserThumbnailAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ConnectProject>> GetProjectsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ConnectFolder>> ListSubfoldersAsync(string folderId, CancellationToken cancellationToken);

    Task<CloneOperation> CloneProjectAsync(
        string sourceProjectId,
        string name,
        string? description,
        CancellationToken cancellationToken);

    Task<CloneOperation> GetCloneStatusAsync(string cloneId, CancellationToken cancellationToken);

    Task<ConnectProject> WaitForCloneAsync(string cloneId, CancellationToken cancellationToken);

    Task<ConnectProject> CreateProjectAsync(
        string name,
        string? description,
        string? location,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ConnectTag>> GetProjectTagsAsync(string projectId, CancellationToken cancellationToken);

    Task<ConnectTag> CreateTagAsync(string projectId, string name, CancellationToken cancellationToken);

    Task AssignTagToObjectsAsync(
        string tagId,
        IReadOnlyList<string> objectIds,
        string objectType,
        CancellationToken cancellationToken);
}

/// <summary>
/// Thin HttpClient wrapper for Trimble Connect Core REST:
/// v2.0 Object Sync / files / folders, v2.1 projects.
/// </summary>
public sealed class TrimbleApiClient : ITrimbleApiClient
{
    private enum ApiVersion
    {
        V20,
        V21
    }

    private static readonly TimeSpan UserCacheTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan UserErrorBackoff = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions ApiJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.Strict
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITrimbleAuthService _auth;
    private readonly ILogger<TrimbleApiClient> _logger;
    private readonly SemaphoreSlim _userGate = new(1, 1);
    private ConnectUser? _cachedUser;
    private DateTimeOffset _userCacheUntil;
    private (byte[] Data, string ContentType, string Url)? _cachedAvatar;

    public TrimbleApiClient(
        IHttpClientFactory httpClientFactory,
        ITrimbleAuthService auth,
        ILogger<TrimbleApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _auth = auth;
        _logger = logger;
    }

    public Task<ProjectSyncStatus> GetProjectSyncStatusAsync(string projectId, CancellationToken cancellationToken) =>
        SendJsonAsync<ProjectSyncStatus>(HttpMethod.Get, $"projects/{projectId}/status", null, cancellationToken, ApiVersion.V20);

    public async Task<IReadOnlyList<SyncObject>> GetProjectSyncObjectsAsync(
        string projectId,
        string statusCursor,
        CancellationToken cancellationToken)
    {
        var items = new List<SyncObject>();
        var cursor = statusCursor;

        while (true)
        {
            var query = string.IsNullOrWhiteSpace(cursor)
                ? $"projects/{projectId}/objects"
                : $"projects/{projectId}/objects?from={Uri.EscapeDataString(cursor)}";

            var page = await SendJsonAsync<SyncObjectPage>(HttpMethod.Get, query, null, cancellationToken, ApiVersion.V20)
                .ConfigureAwait(false);

            items.AddRange(page.AllItems);

            if (string.IsNullOrWhiteSpace(page.NextCursor) || page.NextCursor == cursor)
            {
                break;
            }

            cursor = page.NextCursor;
        }

        return items;
    }

    public async Task DownloadFileAsync(string fileId, string targetLocalPath, CancellationToken cancellationToken)
    {
        var signed = await SendJsonAsync<DownloadUrlResponse>(
                HttpMethod.Get,
                $"files/fs/{fileId}/downloadurl",
                null,
                cancellationToken,
                ApiVersion.V20)
            .ConfigureAwait(false);

        var url = signed.EffectiveUrl
            ?? throw new InvalidOperationException($"No download URL returned for file {fileId}.");

        Directory.CreateDirectory(Path.GetDirectoryName(targetLocalPath)!);
        var tempPath = targetLocalPath + ".partial";

        var transfer = _httpClientFactory.CreateClient("Transfer");
        using var response = await transfer.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        await ThrowIfUnsuccessfulAsync(response, HttpMethod.Get, $"files/fs/{fileId}/download", cancellationToken)
            .ConfigureAwait(false);

        await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        await using (var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        }

        if (File.Exists(targetLocalPath))
        {
            File.Delete(targetLocalPath);
        }

        File.Move(tempPath, targetLocalPath);
        _logger.LogInformation("Downloaded {FileId} to {Path}.", fileId, targetLocalPath);
    }

    public Task<ConnectFile> UploadFileAsync(
        string projectId,
        string parentFolderId,
        string localFilePath,
        CancellationToken cancellationToken) =>
        UploadFileAsync(projectId, parentFolderId, localFilePath, existingFileId: null, cancellationToken);

    public async Task<ConnectFile> UploadFileAsync(
        string projectId,
        string parentFolderId,
        string localFilePath,
        string? existingFileId,
        CancellationToken cancellationToken)
    {
        var info = new FileInfo(localFilePath);
        if (!info.Exists)
        {
            throw new FileNotFoundException("Local file is missing.", localFilePath);
        }

        var remoteName = FileNameSanitizer.SanitizeFileName(info.Name);
        if (!string.Equals(remoteName, info.Name, StringComparison.Ordinal))
        {
            _logger.LogInformation(
                "Sanitized upload file name from {Original} to {Sanitized}.",
                info.Name,
                remoteName);
        }

        info.Refresh();
        if (info.Length <= 0)
        {
            throw new InvalidOperationException(
                $"Trimble Connect rejects zero-byte initiate payloads. Skipped '{remoteName}'.");
        }

        var initUrl = $"files/fs/initiate?projectId={Uri.EscapeDataString(projectId)}";
        var init = await InitiateUploadAsync(
                initUrl,
                remoteName,
                info.Length,
                parentFolderId,
                cancellationToken)
            .ConfigureAwait(false);

        var uploadUrl = init.EffectiveUrl
            ?? throw new InvalidOperationException($"Upload initiate for {remoteName} returned no signed URL.");

        var transfer = _httpClientFactory.CreateClient("Transfer");
        await using var fileStream = new FileStream(
            localFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 128,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        using var content = new StreamContent(fileStream);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var put = await transfer.PutAsync(uploadUrl, content, cancellationToken).ConfigureAwait(false);
        await ThrowIfUnsuccessfulAsync(put, HttpMethod.Put, "files/fs/upload", cancellationToken)
            .ConfigureAwait(false);

        var committed = await SendJsonAsync<ConnectFile>(
                HttpMethod.Post,
                "files/fs/commit",
                new UploadCommitRequest
                {
                    UploadId = init.UploadId,
                    FileId = string.IsNullOrWhiteSpace(init.FileId) ? null : init.FileId
                },
                cancellationToken,
                ApiVersion.V20)
            .ConfigureAwait(false);

        committed.Id = string.IsNullOrWhiteSpace(committed.Id) ? init.FileId ?? existingFileId ?? string.Empty : committed.Id;
        committed.Name ??= remoteName;
        committed.ParentId ??= parentFolderId;
        committed.Size ??= info.Length;

        _logger.LogInformation("Uploaded {File} to project {ProjectId}.", remoteName, projectId);
        return committed;
    }

    private Task<UploadInitResponse> InitiateUploadAsync(
        string initUrl,
        string remoteName,
        long size,
        string parentFolderId,
        CancellationToken cancellationToken) =>
        SendJsonAsync<UploadInitResponse>(
            HttpMethod.Post,
            initUrl,
            new UploadInitRequest
            {
                Name = remoteName,
                Size = size,
                ParentId = parentFolderId,
                ParentType = "FOLDER"
                // fileId is [JsonIgnore] and must never be sent on initiate.
            },
            cancellationToken,
            ApiVersion.V20);

    public Task<ConnectProject> GetProjectAsync(string projectId, CancellationToken cancellationToken) =>
        SendJsonAsync<ConnectProject>(HttpMethod.Get, $"projects/{projectId}", null, cancellationToken, ApiVersion.V21);

    public async Task<ConnectProject> ResolveProjectAsync(
        string? projectId,
        string? projectName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(projectId) && string.IsNullOrWhiteSpace(projectName))
        {
            throw new InvalidOperationException("A project name or project id is required.");
        }

        var projects = await GetProjectsAsync(cancellationToken).ConfigureAwait(false);
        ConnectProject? byId = null;
        if (!string.IsNullOrWhiteSpace(projectId))
        {
            byId = projects.FirstOrDefault(project =>
                string.Equals(project.Id, projectId, StringComparison.OrdinalIgnoreCase));
            if (byId is not null
                && (string.IsNullOrWhiteSpace(projectName)
                    || string.Equals(byId.Name, projectName, StringComparison.OrdinalIgnoreCase)))
            {
                return byId;
            }
        }

        if (!string.IsNullOrWhiteSpace(projectName))
        {
            var match = projects.FirstOrDefault(project =>
                    string.Equals(project.Name, projectName, StringComparison.OrdinalIgnoreCase))
                ?? projects.FirstOrDefault(project =>
                    (project.Name ?? string.Empty).Contains(projectName, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        if (byId is not null)
        {
            return byId;
        }

        throw new InvalidOperationException(
            string.IsNullOrWhiteSpace(projectName)
                ? $"Trimble Connect project '{projectId}' was not found."
                : $"No Trimble Connect project named '{projectName}' was found.");
    }

    public async Task<CloneOperation> CloneProjectAsync(
        string sourceProjectId,
        string name,
        string? description,
        CancellationToken cancellationToken)
    {
        var body = new
        {
            projectId = sourceProjectId,
            name,
            description,
            include = new[] { "folders", "groups", "settings", "folderPermissions" }
        };

        using var response = await SendAsync(HttpMethod.Post, "projects/clones", body, cancellationToken, ApiVersion.V21)
            .ConfigureAwait(false);
        var payload = await ReadResponseBodyAsync(response, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            ThrowApiError(
                HttpMethod.Post,
                "projects/clones",
                response.StatusCode,
                payload,
                JsonSerializer.Serialize(body, ApiJsonOptions));
        }

        return ParseCloneOperation(payload);
    }

    public async Task<CloneOperation> GetCloneStatusAsync(string cloneId, CancellationToken cancellationToken)
    {
        var payload = await SendRawJsonAsync(HttpMethod.Get, $"projects/clones/{cloneId}", cancellationToken, ApiVersion.V21)
            .ConfigureAwait(false);
        return ParseCloneOperation(payload.GetRawText());
    }

    public async Task<ConnectProject> WaitForCloneAsync(string cloneId, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(3);
        CloneOperation? last = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            last = await GetCloneStatusAsync(cloneId, cancellationToken).ConfigureAwait(false);
            if (last.IsFailed)
            {
                throw new InvalidOperationException(last.Error ?? $"Project clone {cloneId} failed with status {last.Status}.");
            }

            if (last.IsDone)
            {
                var projectId = last.ResolvedProjectId;
                if (!string.IsNullOrWhiteSpace(projectId))
                {
                    return await ResolveProjectAsync(projectId, null, cancellationToken).ConfigureAwait(false);
                }

                if (last.Project is not null && !string.IsNullOrWhiteSpace(last.Project.Id))
                {
                    return last.Project;
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException($"Project clone {cloneId} did not finish in time. Last status: {last?.Status ?? "unknown"}.");
    }

    public Task<ConnectProject> CreateProjectAsync(
        string name,
        string? description,
        string? location,
        CancellationToken cancellationToken)
    {
        var body = new
        {
            name,
            description,
            location
        };

        return SendJsonAsync<ConnectProject>(HttpMethod.Post, "projects", body, cancellationToken, ApiVersion.V21);
    }

    private static CloneOperation ParseCloneOperation(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return new CloneOperation();
        }

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var operation = new CloneOperation
        {
            Id = ReadString(root, "id", "cloneId", "operationId") ?? string.Empty,
            Status = ReadString(root, "status", "state") ?? string.Empty,
            ProjectId = ReadString(root, "projectId", "clonedProjectId", "targetProjectId", "newProjectId"),
            Error = ReadString(root, "error", "message", "errorMessage")
        };

        if (root.TryGetProperty("project", out var projectEl) && projectEl.ValueKind == JsonValueKind.Object)
        {
            operation.Project = projectEl.Deserialize<ConnectProject>(ApiJsonOptions);
            operation.ProjectId ??= operation.Project?.Id;
        }

        if (string.IsNullOrWhiteSpace(operation.Status)
            && !string.IsNullOrWhiteSpace(ReadString(root, "id"))
            && root.TryGetProperty("name", out _))
        {
            operation.Project = root.Deserialize<ConnectProject>(ApiJsonOptions);
            operation.ProjectId = operation.Project?.Id;
            operation.Status = "DONE";
        }

        return operation;
    }

    private static string? ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return null;
    }

    public Task<string> ResolveOrCreateFolderAsync(
        string projectId,
        string remoteFolderPath,
        CancellationToken cancellationToken) =>
        ResolveOrCreateFolderAsync(projectId, remoteFolderPath, startFolderId: null, cancellationToken);

    public async Task<string> ResolveOrCreateFolderAsync(
        string projectId,
        string remoteFolderPath,
        string? startFolderId,
        CancellationToken cancellationToken)
    {
        var currentId = await ResolveRootFolderIdAsync(projectId, startFolderId, cancellationToken).ConfigureAwait(false);
        var segments = (remoteFolderPath ?? "/")
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var segment in segments)
        {
            var children = await ListFolderItemsAsync(currentId, cancellationToken).ConfigureAwait(false);
            var match = children.FirstOrDefault(item =>
                item.IsFolder && string.Equals(item.Name, segment, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                currentId = match.Id;
                continue;
            }

            var created = await CreateFolderAsync(projectId, currentId, segment, cancellationToken).ConfigureAwait(false);
            currentId = created.Id;
            _logger.LogInformation("Created remote folder {Folder} in project {ProjectId}.", segment, projectId);
        }

        return currentId;
    }

    public async Task<ConnectFolder> CreateFolderAsync(
        string projectId,
        string parentFolderId,
        string name,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new ArgumentException("projectId is required.", nameof(projectId));
        }

        if (string.IsNullOrWhiteSpace(parentFolderId)
            || string.Equals(parentFolderId, projectId, StringComparison.OrdinalIgnoreCase))
        {
            parentFolderId = await ResolveRootFolderIdAsync(projectId, startFolderId: null, cancellationToken)
                .ConfigureAwait(false);
        }

        var body = new FolderCreateRequest
        {
            Name = name,
            ParentId = parentFolderId,
            ParentType = "FOLDER"
        };
        var url = $"folders?projectId={Uri.EscapeDataString(projectId)}";
        var created = await SendJsonAsync<ConnectFolder>(
                HttpMethod.Post,
                url,
                body,
                cancellationToken,
                ApiVersion.V20)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(created.Id))
        {
            throw new InvalidOperationException($"Trimble Connect created folder '{name}' without an id.");
        }

        created.Name ??= name;
        created.ParentId ??= parentFolderId;
        return created;
    }

    private async Task<string> ResolveRootFolderIdAsync(
        string projectId,
        string? startFolderId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(startFolderId)
            && !string.Equals(startFolderId, projectId, StringComparison.OrdinalIgnoreCase))
        {
            return startFolderId;
        }

        var project = await ResolveProjectAsync(projectId, null, cancellationToken).ConfigureAwait(false);
        var rootId = project.EffectiveRootId;
        if (!string.IsNullOrWhiteSpace(rootId)
            && !string.Equals(rootId, project.Id, StringComparison.OrdinalIgnoreCase))
        {
            return rootId;
        }

        throw new InvalidOperationException(
            $"Project {projectId} did not return a root folder id. Cannot create folders without the Trimble Connect root mapping.");
    }

    public async Task<string?> TryResolveFolderAsync(
        string projectId,
        string remoteFolderPath,
        CancellationToken cancellationToken)
    {
        var project = await ResolveProjectAsync(projectId, null, cancellationToken).ConfigureAwait(false);
        var currentId = project.EffectiveRootId;
        if (string.IsNullOrWhiteSpace(currentId))
        {
            return null;
        }

        var segments = (remoteFolderPath ?? "/")
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var segment in segments)
        {
            var children = await ListFolderItemsAsync(currentId, cancellationToken).ConfigureAwait(false);
            var match = children.FirstOrDefault(item =>
                item.IsFolder && string.Equals(item.Name, segment, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                return null;
            }

            currentId = match.Id;
        }

        return currentId;
    }

    public async Task<IReadOnlyList<ConnectFile>> ListFolderFilesAsync(string folderId, CancellationToken cancellationToken)
    {
        var items = await ListFolderItemsAsync(folderId, cancellationToken).ConfigureAwait(false);
        return items
            .Where(item => item.IsFile)
            .Select(ToConnectFile)
            .ToList();
    }

    public async Task<IReadOnlyList<ConnectFile>> ListFolderFilesRecursiveAsync(
        string folderId,
        CancellationToken cancellationToken)
    {
        var files = new List<ConnectFile>();
        var queue = new Queue<(string Id, string RelativePath)>();
        queue.Enqueue((folderId, string.Empty));

        while (queue.Count > 0)
        {
            var (current, relativePath) = queue.Dequeue();
            var items = await ListFolderItemsAsync(current, cancellationToken).ConfigureAwait(false);
            foreach (var item in items)
            {
                var childPath = string.IsNullOrEmpty(relativePath)
                    ? item.Name ?? string.Empty
                    : $"{relativePath}/{item.Name}";

                if (item.IsFolder)
                {
                    queue.Enqueue((item.Id, childPath));
                }
                else if (item.IsFile)
                {
                    var file = ToConnectFile(item);
                    file.Path ??= childPath;
                    files.Add(file);
                }
            }
        }

        return files;
    }

    private static ConnectFile ToConnectFile(SyncObject item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        ParentId = item.ParentId,
        Path = item.Path,
        Hash = item.Hash,
        Size = item.Size,
        VersionId = item.VersionId,
        Type = item.Type
    };

    public Task<ConnectUser?> GetCurrentUserAsync(CancellationToken cancellationToken) =>
        GetLoggedInUserAsync(cancellationToken);

    public async Task<ConnectUser?> GetLoggedInUserAsync(CancellationToken cancellationToken)
    {
        if (DateTimeOffset.UtcNow < _userCacheUntil)
        {
            return _cachedUser;
        }

        await _userGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (DateTimeOffset.UtcNow < _userCacheUntil)
            {
                return _cachedUser;
            }

            try
            {
                var payload = await SendRawJsonAsync(HttpMethod.Get, "users/me", cancellationToken, ApiVersion.V20)
                    .ConfigureAwait(false);
                if (payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
                {
                    CacheUser(null, UserErrorBackoff);
                    return null;
                }

                var user = payload.Deserialize<ConnectUser>(JsonDefaults.Serializer) ?? new ConnectUser();
                if (string.IsNullOrWhiteSpace(user.Thumbnail))
                {
                    user.Thumbnail = ReadThumbnail(payload);
                }

                CacheUser(user, UserCacheTtl);
                return user;
            }
            catch (HttpRequestException)
            {
                CacheUser(null, UserErrorBackoff);
                return null;
            }
        }
        finally
        {
            _userGate.Release();
        }
    }

    public async Task<(byte[] Data, string ContentType)?> DownloadUserThumbnailAsync(CancellationToken cancellationToken)
    {
        var user = await GetLoggedInUserAsync(cancellationToken).ConfigureAwait(false);
        var url = user?.EffectiveThumbnail;
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (_cachedAvatar is { } cached && string.Equals(cached.Url, url, StringComparison.Ordinal))
        {
            return (cached.Data, cached.ContentType);
        }

        var token = await _auth.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        var client = _httpClientFactory.CreateClient("Transfer");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var error = await ReadResponseBodyAsync(response, cancellationToken).ConfigureAwait(false);
            _logger.LogError(
                "{Error}",
                FormatApiError(HttpMethod.Get, "users/me/thumbnail", response.StatusCode, error, requestBody: null));
            return null;
        }

        var data = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
        _cachedAvatar = (data, contentType, url);
        return (data, contentType);
    }

    public async Task<IReadOnlyList<ConnectProject>> GetProjectsAsync(CancellationToken cancellationToken)
    {
        var projects = new List<ConnectProject>();
        string? skip = null;

        while (true)
        {
            var url = string.IsNullOrWhiteSpace(skip)
                ? "projects?fullyLoaded=true"
                : $"projects?fullyLoaded=true&skipToken={Uri.EscapeDataString(skip)}";

            var payload = await SendRawJsonAsync(HttpMethod.Get, url, cancellationToken, ApiVersion.V21).ConfigureAwait(false);
            if (payload.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in payload.EnumerateArray())
                {
                    var project = item.Deserialize<ConnectProject>(JsonDefaults.Serializer);
                    if (project is not null && !string.IsNullOrWhiteSpace(project.Id))
                    {
                        projects.Add(project);
                    }
                }

                break;
            }

            if (payload.ValueKind == JsonValueKind.Object)
            {
                if (payload.TryGetProperty("items", out var items) || payload.TryGetProperty("data", out items))
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        var project = item.Deserialize<ConnectProject>(JsonDefaults.Serializer);
                        if (project is not null && !string.IsNullOrWhiteSpace(project.Id))
                        {
                            projects.Add(project);
                        }
                    }
                }

                skip = payload.TryGetProperty("skipToken", out var token) ? token.GetString()
                    : payload.TryGetProperty("next", out var next) ? next.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(skip))
                {
                    break;
                }

                continue;
            }

            break;
        }

        return projects;
    }

    public async Task<IReadOnlyList<ConnectFolder>> ListSubfoldersAsync(string folderId, CancellationToken cancellationToken)
    {
        var items = await ListFolderItemsAsync(folderId, foldersOnly: true, cancellationToken).ConfigureAwait(false);
        return items
            .Where(item => item.IsFolder)
            .Select(item => new ConnectFolder
            {
                Id = item.Id,
                Name = item.Name,
                ParentId = item.ParentId ?? folderId
            })
            .ToList();
    }

    public async Task<IReadOnlyList<ConnectTag>> GetProjectTagsAsync(string projectId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            return [];
        }

        var url = $"tags?projectId={Uri.EscapeDataString(projectId)}";
        foreach (var version in new[] { ApiVersion.V21, ApiVersion.V20 })
        {
            try
            {
                var payload = await SendRawJsonAsync(HttpMethod.Get, url, cancellationToken, version).ConfigureAwait(false);
                return ParseTags(payload);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "GET {Url} failed on {Version}.", url, version);
            }
        }

        return [];
    }

    public async Task<ConnectTag> CreateTagAsync(string projectId, string name, CancellationToken cancellationToken)
    {
        var body = new TagCreateRequest
        {
            Name = name.Trim(),
            ProjectId = projectId
        };

        try
        {
            return await SendJsonAsync<ConnectTag>(HttpMethod.Post, "tags", body, cancellationToken, ApiVersion.V21)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "POST tags on v2.1 failed; retrying v2.0.");
            return await SendJsonAsync<ConnectTag>(
                    HttpMethod.Post,
                    $"tags?projectId={Uri.EscapeDataString(projectId)}",
                    body,
                    cancellationToken,
                    ApiVersion.V20)
                .ConfigureAwait(false);
        }
    }

    public async Task AssignTagToObjectsAsync(
        string tagId,
        IReadOnlyList<string> objectIds,
        string objectType,
        CancellationToken cancellationToken)
    {
        var body = new TagAssignRequest
        {
            ObjectIds = objectIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ObjectType = string.IsNullOrWhiteSpace(objectType) ? "FOLDER" : objectType
        };
        if (body.ObjectIds.Count == 0)
        {
            return;
        }

        var url = $"tags/{Uri.EscapeDataString(tagId)}/objects";
        try
        {
            using var response = await SendAsync(HttpMethod.Post, url, body, cancellationToken, ApiVersion.V21)
                .ConfigureAwait(false);
            await ThrowIfUnsuccessfulAsync(response, HttpMethod.Post, url, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "POST {Url} on v2.1 failed; retrying v2.0.", url);
            using var retry = await SendAsync(HttpMethod.Post, url, body, cancellationToken, ApiVersion.V20)
                .ConfigureAwait(false);
            await ThrowIfUnsuccessfulAsync(retry, HttpMethod.Post, url, cancellationToken).ConfigureAwait(false);
        }
    }

    private static IReadOnlyList<ConnectTag> ParseTags(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.Array)
        {
            return payload.Deserialize<List<ConnectTag>>(ApiJsonOptions) ?? [];
        }

        if (payload.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in new[] { "items", "tags", "data", "value" })
            {
                if (payload.TryGetProperty(name, out var items) && items.ValueKind == JsonValueKind.Array)
                {
                    return items.Deserialize<List<ConnectTag>>(ApiJsonOptions) ?? [];
                }
            }
        }

        return [];
    }

    public async Task DeleteFileAsync(string fileId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"files/{fileId}", null, cancellationToken, ApiVersion.V20)
            .ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            return;
        }

        await ThrowIfUnsuccessfulAsync(response, HttpMethod.Delete, $"files/{fileId}", cancellationToken)
            .ConfigureAwait(false);
    }

    private Task<List<SyncObject>> ListFolderItemsAsync(string folderId, CancellationToken cancellationToken) =>
        ListFolderItemsAsync(folderId, foldersOnly: false, cancellationToken);

    private async Task<List<SyncObject>> ListFolderItemsAsync(
        string folderId,
        bool foldersOnly,
        CancellationToken cancellationToken)
    {
        var suffix = foldersOnly ? "?objectTypes=FOLDER" : string.Empty;
        try
        {
            var page = await SendJsonAsync<SyncObjectPage>(
                    HttpMethod.Get,
                    $"folders/{folderId}/items{suffix}",
                    null,
                    cancellationToken,
                    ApiVersion.V21)
                .ConfigureAwait(false);
            return page.AllItems.ToList();
        }
        catch (HttpRequestException)
        {
            var folder = await SendJsonAsync<SyncObjectPage>(
                    HttpMethod.Get,
                    $"folders/{folderId}",
                    null,
                    cancellationToken,
                    ApiVersion.V21)
                .ConfigureAwait(false);
            return folder.AllItems.ToList();
        }
    }

    private static string? ReadThumbnail(JsonElement payload)
    {
        foreach (var name in new[] { "thumbnail", "thumbnailUrl", "picture", "avatar", "imageUrl" })
        {
            if (payload.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        if (payload.TryGetProperty("image", out var image) && image.ValueKind == JsonValueKind.Object)
        {
            if (image.TryGetProperty("href", out var href) && href.ValueKind == JsonValueKind.String)
            {
                return href.GetString();
            }

            if (image.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String)
            {
                return url.GetString();
            }
        }

        return null;
    }

    private static string ClientName(ApiVersion version) =>
        version == ApiVersion.V20 ? "TrimbleConnectV20" : "TrimbleConnect";

    private async Task<JsonElement> SendRawJsonAsync(
        HttpMethod method,
        string relativeUrl,
        CancellationToken cancellationToken,
        ApiVersion version)
    {
        using var response = await SendAsync(method, relativeUrl, null, cancellationToken, version).ConfigureAwait(false);
        var payload = await ReadResponseBodyAsync(response, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            ThrowApiError(method, relativeUrl, response.StatusCode, payload);
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            return default;
        }

        using var document = JsonDocument.Parse(payload);
        return document.RootElement.Clone();
    }

    private async Task<T> SendJsonAsync<T>(
        HttpMethod method,
        string relativeUrl,
        object? body,
        CancellationToken cancellationToken,
        ApiVersion version)
    {
        using var response = await SendAsync(method, relativeUrl, body, cancellationToken, version).ConfigureAwait(false);
        var payload = await ReadResponseBodyAsync(response, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var requestJson = body is null ? null : JsonSerializer.Serialize(body, ApiJsonOptions);
            ThrowApiError(method, relativeUrl, response.StatusCode, payload, requestJson);
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            return Activator.CreateInstance<T>();
        }

        return JsonSerializer.Deserialize<T>(payload, ApiJsonOptions)
            ?? throw new InvalidOperationException($"Could not deserialize {typeof(T).Name} from {relativeUrl}.");
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string relativeUrl,
        object? body,
        CancellationToken cancellationToken,
        ApiVersion version)
    {
        var token = await _auth.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        var client = _httpClientFactory.CreateClient(ClientName(version));

        using var request = new HttpRequestMessage(method, relativeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (body is not null)
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(body, ApiJsonOptions),
                Encoding.UTF8,
                "application/json");
        }

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw WrapTransportError(method, relativeUrl, ex);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            token = await _auth.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            using var retry = new HttpRequestMessage(method, relativeUrl);
            retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null)
            {
                retry.Content = new StringContent(
                    JsonSerializer.Serialize(body, ApiJsonOptions),
                    Encoding.UTF8,
                    "application/json");
            }

            try
            {
                response = await client.SendAsync(retry, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw WrapTransportError(method, relativeUrl, ex);
            }
        }

        return response;
    }

    private async Task ThrowIfUnsuccessfulAsync(
        HttpResponseMessage response,
        HttpMethod method,
        string relativeUrl,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await ReadResponseBodyAsync(response, cancellationToken).ConfigureAwait(false);
        ThrowApiError(method, relativeUrl, response.StatusCode, body);
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private void ThrowApiError(
        HttpMethod method,
        string relativeUrl,
        HttpStatusCode statusCode,
        string? responseBody,
        string? requestBody = null)
    {
        var message = FormatApiError(method, relativeUrl, statusCode, responseBody, requestBody);
        _logger.LogError("{Error}", message);
        throw new HttpRequestException(message, inner: null, statusCode);
    }

    private HttpRequestException WrapTransportError(HttpMethod method, string relativeUrl, HttpRequestException ex)
    {
        var message = $"Trimble Connect API error [{method.Method} {relativeUrl}]: {ex.Message}";
        _logger.LogError(ex, "{Error}", message);
        return new HttpRequestException(message, ex, ex.StatusCode);
    }

    private void CacheUser(ConnectUser? user, TimeSpan ttl)
    {
        _cachedUser = user;
        _userCacheUntil = DateTimeOffset.UtcNow.Add(ttl);
        if (user is null)
        {
            _cachedAvatar = null;
        }
    }

    private static string FormatApiError(
        HttpMethod method,
        string relativeUrl,
        HttpStatusCode statusCode,
        string? responseBody,
        string? requestBody)
    {
        var response = string.IsNullOrWhiteSpace(responseBody) ? "(empty)" : responseBody.Trim();
        var request = string.IsNullOrWhiteSpace(requestBody) ? "(none)" : requestBody.Trim();
        return $"Trimble Connect API error [{method.Method} {relativeUrl}]: HTTP {(int)statusCode} - Request Body: {request} - Response Body: {response}";
    }

    private static async Task<string> ReadResponseBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return $"(could not read response body: {ex.Message})";
        }
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
