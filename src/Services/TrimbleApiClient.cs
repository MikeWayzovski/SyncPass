using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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

    Task<string> ResolveOrCreateFolderAsync(
        string projectId,
        string remoteFolderPath,
        CancellationToken cancellationToken);

    Task<string> ResolveOrCreateFolderAsync(
        string projectId,
        string remoteFolderPath,
        string? startFolderId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ConnectFile>> ListFolderFilesAsync(string folderId, CancellationToken cancellationToken);

    Task DeleteFileAsync(string fileId, CancellationToken cancellationToken);

    Task<ConnectUser?> GetCurrentUserAsync(CancellationToken cancellationToken);

    Task<ConnectUser?> GetLoggedInUserAsync(CancellationToken cancellationToken);

    Task<(byte[] Data, string ContentType)?> DownloadUserThumbnailAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ConnectProject>> GetProjectsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ConnectFolder>> ListSubfoldersAsync(string folderId, CancellationToken cancellationToken);
}

/// <summary>
/// Thin HttpClient wrapper for Trimble Connect Core REST:
/// v2.0 Object Sync / files / users, v2.1 projects and folders.
/// </summary>
public sealed class TrimbleApiClient : ITrimbleApiClient
{
    private enum ApiVersion
    {
        V20,
        V21
    }

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITrimbleAuthService _auth;
    private readonly ILogger<TrimbleApiClient> _logger;

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
        response.EnsureSuccessStatusCode();

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

        var init = await SendJsonAsync<UploadInitResponse>(
                HttpMethod.Post,
                "files/fs/initiate",
                new UploadInitRequest
                {
                    ProjectId = projectId,
                    ParentId = parentFolderId,
                    ParentType = "FOLDER",
                    Name = info.Name,
                    Size = info.Length,
                    FileId = existingFileId
                },
                cancellationToken,
                ApiVersion.V20)
            .ConfigureAwait(false);

        var uploadUrl = init.EffectiveUrl
            ?? throw new InvalidOperationException($"Upload initiate for {info.Name} returned no signed URL.");

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
        if (!put.IsSuccessStatusCode)
        {
            var error = await put.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException($"Pre-signed upload failed with HTTP {(int)put.StatusCode}: {error}");
        }

        var committed = await SendJsonAsync<ConnectFile>(
                HttpMethod.Post,
                "files/fs/commit",
                new UploadCommitRequest
                {
                    UploadId = init.UploadId,
                    ProjectId = projectId,
                    FileId = init.FileId ?? existingFileId
                },
                cancellationToken,
                ApiVersion.V20)
            .ConfigureAwait(false);

        committed.Id = string.IsNullOrWhiteSpace(committed.Id) ? init.FileId ?? existingFileId ?? string.Empty : committed.Id;
        committed.Name ??= info.Name;
        committed.ParentId ??= parentFolderId;
        committed.Size ??= info.Length;

        _logger.LogInformation("Uploaded {File} to project {ProjectId}.", info.Name, projectId);
        return committed;
    }

    public Task<ConnectProject> GetProjectAsync(string projectId, CancellationToken cancellationToken) =>
        SendJsonAsync<ConnectProject>(HttpMethod.Get, $"projects/{projectId}", null, cancellationToken, ApiVersion.V21);

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
        string currentId;
        if (!string.IsNullOrWhiteSpace(startFolderId))
        {
            currentId = startFolderId;
        }
        else
        {
            var project = await GetProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
            currentId = project.EffectiveRootId
                ?? throw new InvalidOperationException($"Project {projectId} did not return a root folder id.");
        }

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

            var created = await SendJsonAsync<ConnectFolder>(
                    HttpMethod.Post,
                    "folders",
                    new
                    {
                        name = segment,
                        parentId = currentId,
                        parentType = "FOLDER",
                        projectId
                    },
                    cancellationToken,
                    ApiVersion.V21)
                .ConfigureAwait(false);

            currentId = created.Id;
            _logger.LogInformation("Created remote folder {Folder} in project {ProjectId}.", segment, projectId);
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
        try
        {
            var payload = await SendRawJsonAsync(HttpMethod.Get, "users/me", cancellationToken, ApiVersion.V20)
                .ConfigureAwait(false);
            if (payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            {
                return null;
            }

            var user = payload.Deserialize<ConnectUser>(JsonDefaults.Serializer) ?? new ConnectUser();
            if (string.IsNullOrWhiteSpace(user.Thumbnail))
            {
                user.Thumbnail = ReadThumbnail(payload);
            }

            return user;
        }
        catch (HttpRequestException)
        {
            return null;
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

        var token = await _auth.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        var client = _httpClientFactory.CreateClient("Transfer");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var data = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
        return (data, contentType);
    }

    public async Task<IReadOnlyList<ConnectProject>> GetProjectsAsync(CancellationToken cancellationToken)
    {
        var projects = new List<ConnectProject>();
        string? skip = null;

        while (true)
        {
            var url = string.IsNullOrWhiteSpace(skip)
                ? "projects?fullyLoaded=false"
                : $"projects?fullyLoaded=false&skipToken={Uri.EscapeDataString(skip)}";

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

    public async Task DeleteFileAsync(string fileId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"files/{fileId}", null, cancellationToken, ApiVersion.V20)
            .ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
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
        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Trimble Connect {method} {relativeUrl} failed with HTTP {(int)response.StatusCode}.");
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
        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Trimble Connect {Method} {Url} failed with {StatusCode}.",
                method,
                relativeUrl,
                (int)response.StatusCode);
            throw new HttpRequestException($"Trimble Connect {method} {relativeUrl} failed with HTTP {(int)response.StatusCode}.");
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            return Activator.CreateInstance<T>();
        }

        return JsonSerializer.Deserialize<T>(payload, JsonDefaults.Serializer)
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
                JsonSerializer.Serialize(body, JsonDefaults.Serializer),
                Encoding.UTF8,
                "application/json");
        }

        var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            token = await _auth.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            using var retry = new HttpRequestMessage(method, relativeUrl);
            retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null)
            {
                retry.Content = new StringContent(
                    JsonSerializer.Serialize(body, JsonDefaults.Serializer),
                    Encoding.UTF8,
                    "application/json");
            }

            response = await client.SendAsync(retry, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }
}
