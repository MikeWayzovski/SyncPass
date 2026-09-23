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
    private readonly SystemEndpoints _system;
    private readonly DashboardListenState _listen;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<SetupWebServer> _logger;

    public SetupWebServer(
        SetupApi api,
        SystemEndpoints system,
        DashboardListenState listen,
        IHostEnvironment environment,
        ILogger<SetupWebServer> logger)
    {
        _api = api;
        _system = system;
        _listen = listen;
        _environment = environment;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var listener = BindListener();
        if (listener is null)
        {
            return;
        }

        using (listener)
        {

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
    }

    private HttpListener? BindListener()
    {
        var prefixes = DashboardListen.ToPrefixes(_listen.RequestedUrl);
        foreach (var prefix in prefixes)
        {
            var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            try
            {
                listener.Start();
                _listen.Port = DashboardListen.PortOf(_listen.RequestedUrl);
                _listen.ListeningOnAllInterfaces = prefix.Contains("://+", StringComparison.Ordinal)
                    || prefix.Contains("://*", StringComparison.Ordinal);
                _logger.LogInformation(
                    "Trimble Connector account dashboard listening on {Prefix} (requested {Url}).",
                    prefix,
                    _listen.RequestedUrl);
                return listener;
            }
            catch (Exception ex) when (ex is HttpListenerException or System.Net.Sockets.SocketException)
            {
                _logger.LogWarning(ex, "Could not bind {Prefix}.", prefix);
                listener.Close();
            }
        }

        if (DashboardListen.IsAllInterfaces(_listen.RequestedUrl))
        {
            var port = _listen.Port > 0 ? _listen.Port : DashboardListen.PortOf(_listen.RequestedUrl);
            var fallback = new HttpListener();
            fallback.Prefixes.Add($"http://localhost:{port}/");
            fallback.Prefixes.Add($"http://127.0.0.1:{port}/");
            try
            {
                fallback.Start();
                _listen.Port = port;
                _listen.ListeningOnAllInterfaces = false;
                _logger.LogWarning(
                    "LAN binding {Url} failed. Dashboard is only available at http://localhost:{Port}. On Windows, reserve the URL with: netsh http add urlacl url=http://+:{Port}/ user=Everyone",
                    _listen.RequestedUrl,
                    port,
                    port);
                return fallback;
            }
            catch (HttpListenerException ex)
            {
                _logger.LogError(ex, "Could not bind http://localhost:{Port}. The account dashboard is unavailable.", port);
                fallback.Close();
            }
        }
        else
        {
            _logger.LogError("Could not bind {Url}. The account dashboard is unavailable.", _listen.RequestedUrl);
        }

        return null;
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

            if (request.HttpMethod == "GET" && (path == "/api/user/avatar" || path == "/api/setup/avatar"))
            {
                await WriteAvatarAsync(context.Response, cancellationToken).ConfigureAwait(false);
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

            if (request.HttpMethod == "GET" && path == "/api/system/network")
            {
                await WriteJsonAsync(context.Response, HttpStatusCode.OK, _system.DescribeNetwork(), cancellationToken).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "PUT" && path == "/api/system/port")
            {
                using var portReader = new StreamReader(request.InputStream, request.ContentEncoding);
                var portBody = await portReader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                var update = JsonSerializer.Deserialize<ListenPortUpdateRequest>(portBody, JsonDefaults.Serializer)
                    ?? throw new ArgumentException("Ongeldige poort.");
                await WriteJsonAsync(
                    context.Response,
                    HttpStatusCode.OK,
                    _system.SaveListenPort(update.Port),
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "GET" && path == "/api/system/backup")
            {
                var backup = _system.CreateBackup();
                await WriteBytesAsync(
                    context.Response,
                    HttpStatusCode.OK,
                    "application/zip",
                    $"attachment; filename=\"{backup.FileName}\"",
                    backup.Content,
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "POST" && path == "/api/system/restore")
            {
                var upload = await SystemEndpoints.ReadUploadAsync(
                    request.InputStream,
                    request.ContentType,
                    request.ContentLength64 > 0 ? request.ContentLength64 : null,
                    cancellationToken).ConfigureAwait(false);
                await WriteJsonAsync(context.Response, HttpStatusCode.OK, _system.Restore(upload), cancellationToken).ConfigureAwait(false);
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

    private static async Task WriteBytesAsync(
        HttpListenerResponse response,
        HttpStatusCode status,
        string contentType,
        string contentDisposition,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        response.StatusCode = (int)status;
        response.ContentType = contentType;
        response.Headers["Content-Disposition"] = contentDisposition;
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        response.Close();
    }

    private async Task WriteAvatarAsync(HttpListenerResponse response, CancellationToken cancellationToken)
    {
        byte[] data;
        string contentType;
        try
        {
            var avatar = await _api.GetAvatarAsync(cancellationToken).ConfigureAwait(false);
            if (avatar is null || avatar.Value.Data.Length == 0)
            {
                data = AvatarFallbackSvg;
                contentType = "image/svg+xml";
            }
            else
            {
                data = avatar.Value.Data;
                contentType = string.IsNullOrWhiteSpace(avatar.Value.ContentType)
                    ? "image/jpeg"
                    : avatar.Value.ContentType;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "User avatar proxy failed. Serving the local fallback.");
            data = AvatarFallbackSvg;
            contentType = "image/svg+xml";
        }

        response.StatusCode = (int)HttpStatusCode.OK;
        response.ContentType = contentType;
        response.ContentLength64 = data.Length;
        response.Headers["Cache-Control"] = "private, no-store";
        await response.OutputStream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
        response.Close();
    }

    private static readonly byte[] AvatarFallbackSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" role="img" aria-label="Gebruiker">
          <rect width="64" height="64" rx="32" fill="#d0d3d4"/>
          <circle cx="32" cy="24" r="10" fill="#6a6e79"/>
          <path d="M14 54c2.2-11 9.5-16 18-16s15.8 5 18 16" fill="#6a6e79"/>
        </svg>
        """u8.ToArray();

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
