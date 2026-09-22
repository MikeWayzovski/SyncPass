using System.Net;
using System.Text;
using System.Text.Json;
using TrimbleConnector.Config;
using TrimbleConnector.Models;

namespace TrimbleConnector.Endpoints;

/// <summary>
/// Embedded account dashboard on http://localhost:5000. Serves wwwroot and the
/// /api/setup Minimal-API-equivalent routes, plus the OAuth /callback.
/// </summary>
public sealed class SetupWebServer : BackgroundService
{
    private readonly SetupApi _api;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<SetupWebServer> _logger;

    public SetupWebServer(SetupApi api, IHostEnvironment environment, ILogger<SetupWebServer> logger)
    {
        _api = api;
        _environment = environment;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:5000/");
        listener.Prefixes.Add("http://127.0.0.1:5000/");

        try
        {
            listener.Start();
        }
        catch (HttpListenerException ex)
        {
            _logger.LogError(ex, "Could not bind http://localhost:5000. The account dashboard is unavailable.");
            return;
        }

        _logger.LogInformation("Trimble Connector account dashboard: http://localhost:5000");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync().WaitAsync(stoppingToken).ConfigureAwait(false);
                _ = Task.Run(() => HandleAsync(context, stoppingToken), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (listener.IsListening)
            {
                listener.Stop();
            }
        }
    }

    private async Task HandleAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            var request = context.Request;
            var path = request.Url?.AbsolutePath.TrimEnd('/') ?? string.Empty;
            if (string.IsNullOrEmpty(path))
            {
                path = "/";
            }

            if (request.HttpMethod == "GET" && path == "/api/setup/status")
            {
                await WriteJsonAsync(context.Response, HttpStatusCode.OK, await _api.GetStatusAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "GET" && path == "/api/setup/avatar")
            {
                var avatar = await _api.GetAvatarAsync(cancellationToken).ConfigureAwait(false);
                if (avatar is null)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    context.Response.Close();
                    return;
                }

                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = avatar.Value.ContentType;
                context.Response.ContentLength64 = avatar.Value.Data.Length;
                await context.Response.OutputStream.WriteAsync(avatar.Value.Data, cancellationToken).ConfigureAwait(false);
                context.Response.Close();
                return;
            }

            if (request.HttpMethod == "GET" && path == "/api/setup/login-url")
            {
                await WriteJsonAsync(context.Response, HttpStatusCode.OK, _api.GetLoginUrl(), cancellationToken).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "GET" && (path == "/api/setup/projects" || path == "/api/projects"))
            {
                await WriteJsonAsync(context.Response, HttpStatusCode.OK, await _api.GetProjectsAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "GET" && path == "/api/setup/folders")
            {
                var projectId = request.QueryString["projectId"];
                var projectName = request.QueryString["projectName"];
                var folderId = request.QueryString["folderId"];
                var parentPath = request.QueryString["parentPath"];
                await WriteJsonAsync(
                    context.Response,
                    HttpStatusCode.OK,
                    await _api.GetFoldersAsync(projectId, projectName, folderId, parentPath, cancellationToken).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "POST" && path == "/api/setup/save")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                var body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                var payload = JsonSerializer.Deserialize<SaveSetupRequest>(body, JsonDefaults.Serializer)
                    ?? throw new ArgumentException("Invalid JSON payload.");
                var saved = _api.Save(payload);
                await WriteJsonAsync(context.Response, HttpStatusCode.OK, new { saved = true, jobId = saved.JobId, enabled = saved.Enabled }, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "GET" && path == "/api/setup/local-tree")
            {
                await WriteJsonAsync(
                    context.Response,
                    HttpStatusCode.OK,
                    _api.ScanLocalTree(request.QueryString["path"]),
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "GET" && path == "/api/setup/browse")
            {
                await WriteJsonAsync(
                    context.Response,
                    HttpStatusCode.OK,
                    _api.BrowseLocal(request.QueryString["path"]),
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "POST" && path == "/api/setup/folders")
            {
                using var folderReader = new StreamReader(request.InputStream, request.ContentEncoding);
                var folderBody = await folderReader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                var folderRequest = JsonSerializer.Deserialize<CreateFolderRequest>(folderBody, JsonDefaults.Serializer)
                    ?? throw new ArgumentException("Invalid JSON payload.");
                await WriteJsonAsync(
                    context.Response,
                    HttpStatusCode.OK,
                    await _api.CreateFolderAsync(folderRequest, cancellationToken).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "POST" && (path == "/api/projects/inventory" || path == "/api/setup/inventory"))
            {
                using var inventoryReader = new StreamReader(request.InputStream, request.ContentEncoding);
                var inventoryBody = await inventoryReader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                var inventory = JsonSerializer.Deserialize<InventoryRequest>(inventoryBody, JsonDefaults.Serializer)
                    ?? throw new ArgumentException("Invalid JSON payload.");
                await WriteJsonAsync(
                    context.Response,
                    HttpStatusCode.OK,
                    await _api.InventoryAsync(inventory, cancellationToken).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "POST" && path == "/api/setup/activate")
            {
                using var activateReader = new StreamReader(request.InputStream, request.ContentEncoding);
                var activateBody = await activateReader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                var activate = JsonSerializer.Deserialize<ActivateJobRequest>(activateBody, JsonDefaults.Serializer)
                    ?? throw new ArgumentException("Invalid JSON payload.");
                var job = _api.Activate(activate);
                await WriteJsonAsync(context.Response, HttpStatusCode.OK, new { activated = true, jobId = job.JobId }, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "GET" && path == "/api/setup/config")
            {
                await WriteJsonAsync(context.Response, HttpStatusCode.OK, _api.GetConfig(), cancellationToken).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "PUT" && path == "/api/setup/config")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                var body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                var payload = JsonSerializer.Deserialize<ConnectorSyncConfig>(body, JsonDefaults.Serializer)
                    ?? throw new ArgumentException("Invalid JSON payload.");
                _api.SaveConfig(payload);
                await WriteJsonAsync(context.Response, HttpStatusCode.OK, new { saved = true }, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "POST" && path == "/api/setup/provision")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                var body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                var payload = JsonSerializer.Deserialize<ProvisionProjectRequest>(body, JsonDefaults.Serializer)
                    ?? throw new ArgumentException("Invalid JSON payload.");
                var project = await _api.ProvisionAsync(payload, cancellationToken).ConfigureAwait(false);
                await WriteJsonAsync(context.Response, HttpStatusCode.OK, project, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "GET" && path == "/api/overview/jobs")
            {
                await WriteJsonAsync(context.Response, HttpStatusCode.OK, _api.GetOverviewJobs(), cancellationToken).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "GET" && path == "/api/overview/stats")
            {
                await WriteJsonAsync(context.Response, HttpStatusCode.OK, _api.GetOverviewStats(), cancellationToken).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "GET" && path == "/api/overview/tags")
            {
                await WriteJsonAsync(
                    context.Response,
                    HttpStatusCode.OK,
                    await _api.GetConnectTagsAsync(request.QueryString["projectId"], cancellationToken).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "POST" && path.StartsWith("/api/overview/jobs", StringComparison.OrdinalIgnoreCase)
                && (path == "/api/overview/jobs/toggle" || path.EndsWith("/toggle", StringComparison.OrdinalIgnoreCase)))
            {
                var toggleId = request.QueryString["jobId"] ?? await ReadJobIdAsync(request, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(toggleId) && path.StartsWith("/api/overview/jobs/", StringComparison.OrdinalIgnoreCase))
                {
                    toggleId = Uri.UnescapeDataString(path["/api/overview/jobs/".Length..^"/toggle".Length]);
                }

                await WriteJsonAsync(context.Response, HttpStatusCode.OK, _api.ToggleOverviewJob(toggleId ?? string.Empty), cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "POST" && (path == "/api/overview/jobs/sync" || path.EndsWith("/sync", StringComparison.OrdinalIgnoreCase)))
            {
                var syncId = request.QueryString["jobId"] ?? await ReadJobIdAsync(request, cancellationToken).ConfigureAwait(false);
                await WriteJsonAsync(context.Response, HttpStatusCode.OK, _api.RequestOverviewSync(syncId ?? string.Empty), cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "PUT" && path == "/api/overview/jobs/metadata")
            {
                using var metadataReader = new StreamReader(request.InputStream, request.ContentEncoding);
                var metadataBody = await metadataReader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                var metadata = JsonSerializer.Deserialize<OverviewMetadataUpdateRequest>(metadataBody, JsonDefaults.Serializer)
                    ?? throw new ArgumentException("Invalid JSON payload.");
                await WriteJsonAsync(
                    context.Response,
                    HttpStatusCode.OK,
                    await _api.UpdateOverviewMetadataAsync(metadata, cancellationToken).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "GET" && path == "/api/activity")
            {
                await WriteJsonAsync(context.Response, HttpStatusCode.OK, _api.GetRecentActivities(), cancellationToken).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "GET" && path == "/activity")
            {
                await ServeStaticAsync(context.Response, "/activity.html", cancellationToken).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "GET" && path == "/callback")
            {
                await HandleCallbackAsync(context, cancellationToken).ConfigureAwait(false);
                return;
            }

            await ServeStaticAsync(context.Response, path == "/" ? "/index.html" : path, cancellationToken).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            await WriteJsonAsync(context.Response, HttpStatusCode.Unauthorized, new { error = ex.Message }, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            await WriteJsonAsync(context.Response, HttpStatusCode.BadRequest, new { error = ex.Message }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Setup wizard request failed.");
            await WriteJsonAsync(context.Response, HttpStatusCode.InternalServerError, new { error = ex.Message }, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<string?> ReadJobIdAsync(HttpListenerRequest request, CancellationToken cancellationToken)
    {
        if (!request.HasEntityBody)
        {
            return null;
        }

        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        var body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var payload = JsonSerializer.Deserialize<OverviewJobActionRequest>(body, JsonDefaults.Serializer);
        return payload?.JobId;
    }

    private async Task HandleCallbackAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var error = context.Request.QueryString["error"];
        var description = context.Request.QueryString["error_description"] ?? error;
        if (!string.IsNullOrWhiteSpace(error))
        {
            Redirect(context.Response, $"/?authError={Uri.EscapeDataString(description ?? error)}");
            return;
        }

        var code = context.Request.QueryString["code"];
        if (string.IsNullOrWhiteSpace(code))
        {
            Redirect(context.Response, "/?authError=missing_code");
            return;
        }

        try
        {
            await _api.CompleteCallbackAsync(code, cancellationToken).ConfigureAwait(false);
            Redirect(context.Response, "/?loggedIn=1");
        }
        catch (Exception)
        {
            Redirect(context.Response, "/?authError=token_exchange_failed");
        }
    }

    private async Task ServeStaticAsync(HttpListenerResponse response, string requestPath, CancellationToken cancellationToken)
    {
        var relative = requestPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "wwwroot", relative));
        var root = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "wwwroot"));
        if (!full.StartsWith(root, StringComparison.Ordinal) || !File.Exists(full))
        {
            response.StatusCode = (int)HttpStatusCode.NotFound;
            response.Close();
            return;
        }

        var bytes = await File.ReadAllBytesAsync(full, cancellationToken).ConfigureAwait(false);
        response.StatusCode = (int)HttpStatusCode.OK;
        response.ContentType = ContentType(full);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        response.Close();
    }

    private static string ContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".html" => "text/html; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".js" => "text/javascript; charset=utf-8",
        ".json" => "application/json",
        ".svg" => "image/svg+xml",
        ".png" => "image/png",
        _ => "application/octet-stream"
    };

    private static async Task WriteJsonAsync(HttpListenerResponse response, HttpStatusCode status, object payload, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonDefaults.Serializer));
        response.StatusCode = (int)status;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        response.Close();
    }

    private static void Redirect(HttpListenerResponse response, string location)
    {
        response.StatusCode = (int)HttpStatusCode.Found;
        response.RedirectLocation = location;
        response.Close();
    }
}
