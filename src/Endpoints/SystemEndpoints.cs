using System.IO.Compression;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using TrimbleConnector.Config;
using TrimbleConnector.Services;

namespace TrimbleConnector.Endpoints;

/// <summary>
/// Backup and restore for data/ plus appsettings.json, and LAN address lookup
/// for the account dashboard.
/// </summary>
public sealed class SystemEndpoints
{
    public const long MaxUploadBytes = 256L * 1024 * 1024;

    private readonly IHostEnvironment _environment;
    private readonly SyncJobStore _jobs;
    private readonly SyncEngine _engine;
    private readonly SyncStateRepository _state;
    private readonly ITrimbleAuthService _auth;
    private readonly DashboardListenState _listen;
    private readonly IOptionsMonitor<TrimbleConnectOptions> _options;
    private readonly ILogger<SystemEndpoints> _logger;

    public SystemEndpoints(
        IHostEnvironment environment,
        SyncJobStore jobs,
        SyncEngine engine,
        SyncStateRepository state,
        ITrimbleAuthService auth,
        DashboardListenState listen,
        IOptionsMonitor<TrimbleConnectOptions> options,
        ILogger<SystemEndpoints> logger)
    {
        _environment = environment;
        _jobs = jobs;
        _engine = engine;
        _state = state;
        _auth = auth;
        _listen = listen;
        _options = options;
        _logger = logger;
    }

    public object DescribeNetwork()
    {
        var port = _listen.Port > 0 ? _listen.Port : 5000;
        var addresses = ListLanAddresses();
        var urls = addresses.Select(address => $"http://{address}:{port}").ToList();
        return new
        {
            port,
            hostName = Dns.GetHostName(),
            listeningOnAllInterfaces = _listen.ListeningOnAllInterfaces,
            addresses,
            urls,
            localUrl = $"http://localhost:{port}",
            redirectUri = _options.CurrentValue.EffectiveRedirectUri
        };
    }

    public object SaveListenPort(int port)
    {
        if (port is < 1 or > 65535)
        {
            throw new ArgumentException("Poort moet tussen 1 en 65535 liggen.");
        }

        var path = Path.Combine(_environment.ContentRootPath, "appsettings.json");
        var root = File.Exists(path)
            ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? new JsonObject()
            : new JsonObject();
        var trimble = root["TrimbleConnect"] as JsonObject ?? new JsonObject();
        root["TrimbleConnect"] = trimble;
        trimble["Port"] = port;
        var currentRedirect = trimble["RedirectUri"]?.GetValue<string>();
        var redirect = string.IsNullOrWhiteSpace(currentRedirect) || TrimbleConnectOptions.IsLocalhostCallback(currentRedirect)
            ? $"http://localhost:{port}/callback"
            : currentRedirect.Trim();
        trimble["RedirectUri"] = redirect;

        var kestrel = root["Kestrel"] as JsonObject ?? new JsonObject();
        root["Kestrel"] = kestrel;
        var endpoints = kestrel["Endpoints"] as JsonObject ?? new JsonObject();
        kestrel["Endpoints"] = endpoints;
        var http = endpoints["Http"] as JsonObject ?? new JsonObject();
        endpoints["Http"] = http;
        var currentUrl = http["Url"]?.GetValue<string>();
        http["Url"] = DashboardListen.WithPort(
            string.IsNullOrWhiteSpace(currentUrl) ? DashboardListenState.FallbackUrl : currentUrl,
            port);

        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        var restartRequired = port != (_listen.Port > 0 ? _listen.Port : 5000);
        return new
        {
            port,
            redirectUri = redirect,
            restartRequired,
            message = restartRequired
                ? $"Poort {port} is opgeslagen. Herstart de connector en registreer {redirect} in de Trimble Developer Console."
                : $"Poort {port} is opgeslagen."
        };
    }

    public (byte[] Content, string FileName) CreateBackup()
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmm");
        var fileName = $"TrimbleConnector-Backup-{stamp}.zip";
        var contentRoot = _environment.ContentRootPath;
        var dataDir = Path.Combine(contentRoot, "data");
        var settingsPath = Path.Combine(contentRoot, "appsettings.json");

        _state.Checkpoint();

        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            if (File.Exists(settingsPath))
            {
                AddFile(archive, settingsPath, "appsettings.json");
            }

            if (Directory.Exists(dataDir))
            {
                foreach (var file in Directory.EnumerateFiles(dataDir, "*", SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(contentRoot, file).Replace('\\', '/');
                    if (relative.Contains("/backups/", StringComparison.OrdinalIgnoreCase)
                        || relative.StartsWith("backups/", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    AddFile(archive, file, relative);
                }
            }
        }

        if (buffer.Length == 0)
        {
            throw new ArgumentException("Er is nog geen data/ of appsettings.json om te back-uppen.");
        }

        _logger.LogInformation("Created configuration backup {FileName}.", fileName);
        return (buffer.ToArray(), fileName);
    }

    public object Restore(byte[] zipBytes)
    {
        if (zipBytes.Length == 0)
        {
            throw new ArgumentException("Het geüploade bestand is leeg.");
        }

        List<ZipEntryPayload> entries;
        try
        {
            entries = ReadEntries(zipBytes);
        }
        catch (InvalidDataException)
        {
            throw new ArgumentException("Het bestand is geen geldig zip-archief.");
        }

        ValidateEntries(entries);

        var contentRoot = _environment.ContentRootPath;
        var dataDir = Path.Combine(contentRoot, "data");
        var settingsPath = Path.Combine(contentRoot, "appsettings.json");
        var rollback = Path.Combine(contentRoot, "backups", $"rollback-{DateTime.Now:yyyyMMdd-HHmmss}");

        _state.Checkpoint();
        _state.Suspend();
        try
        {
            Directory.CreateDirectory(rollback);
            if (Directory.Exists(dataDir))
            {
                CopyDirectory(dataDir, Path.Combine(rollback, "data"));
            }

            if (File.Exists(settingsPath))
            {
                CopyShared(settingsPath, Path.Combine(rollback, "appsettings.json"));
            }

            DeleteWalFiles(dataDir);

            foreach (var entry in entries)
            {
                var destination = ResolveDestination(contentRoot, entry.Name);
                if (destination is null)
                {
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.WriteAllBytes(destination, entry.Content);
            }
        }
        catch (Exception ex)
        {
            try
            {
                TryRollback(rollback, dataDir, settingsPath);
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(rollbackEx, "Rollback copy {Rollback} could not be restored after a failed import.", rollback);
            }

            _logger.LogError(ex, "Backup restore failed.");
            throw;
        }
        finally
        {
            _state.Resume();
        }

        _jobs.ReloadFromDisk();
        _auth.ReloadPersistedCredentials();
        _engine.ReloadJobs();
        _logger.LogInformation("Restored configuration from backup. Rollback copy: {Rollback}.", rollback);

        return new
        {
            success = true,
            message = "Backup succesvol hersteld!"
        };
    }

    public static async Task<byte[]> ReadUploadAsync(Stream body, string? contentType, long? contentLength, CancellationToken cancellationToken)
    {
        if (contentLength > MaxUploadBytes)
        {
            throw new ArgumentException("Het zip-bestand is te groot.");
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await body.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > MaxUploadBytes)
            {
                throw new ArgumentException("Het zip-bestand is te groot.");
            }

            buffer.Write(chunk, 0, read);
        }

        var bytes = buffer.ToArray();
        if (contentType is not null
            && contentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            return ExtractMultipartFile(bytes, contentType);
        }

        return bytes;
    }

    private static void ValidateEntries(IReadOnlyList<ZipEntryPayload> entries)
    {
        var hasDataFile = false;
        var sawJobs = false;
        var jobsValid = false;

        foreach (var entry in entries)
        {
            var name = entry.Name.Replace('\\', '/').TrimStart('/');
            if (IsJobsEntry(name))
            {
                sawJobs = true;
                jobsValid = IsJsonObjectOrArray(entry.Content);
            }

            if (name.StartsWith("data/", StringComparison.OrdinalIgnoreCase) && !name.EndsWith('/'))
            {
                hasDataFile = true;
            }
        }

        if (sawJobs && !jobsValid)
        {
            throw new ArgumentException("De backup bevat geen geldige sync-jobs.json.");
        }

        if (!jobsValid && !hasDataFile)
        {
            throw new ArgumentException("De backup bevat geen geldige sync-jobs.json of data/-bestanden.");
        }
    }

    private static bool IsJobsEntry(string name) =>
        name.Equals("sync-jobs.json", StringComparison.OrdinalIgnoreCase)
        || name.Equals("data/sync-jobs.json", StringComparison.OrdinalIgnoreCase);

    private static bool IsJsonObjectOrArray(byte[] content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            return document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static List<ZipEntryPayload> ReadEntries(byte[] zipBytes)
    {
        using var stream = new MemoryStream(zipBytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var entries = new List<ZipEntryPayload>();
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            using var entryStream = entry.Open();
            using var copy = new MemoryStream();
            entryStream.CopyTo(copy);
            entries.Add(new ZipEntryPayload(entry.FullName, copy.ToArray()));
        }

        return entries;
    }

    private static string? ResolveDestination(string contentRoot, string entryName)
    {
        var normalized = entryName.Replace('\\', '/').TrimStart('/');
        if (normalized.Length == 0 || normalized.EndsWith('/'))
        {
            return null;
        }

        if (normalized.Split('/').Any(part => part == ".."))
        {
            throw new ArgumentException("De backup bevat een ongeldig pad.");
        }

        string relative;
        if (normalized.Equals("appsettings.json", StringComparison.OrdinalIgnoreCase))
        {
            relative = "appsettings.json";
        }
        else if (normalized.Equals("sync-jobs.json", StringComparison.OrdinalIgnoreCase))
        {
            relative = Path.Combine("data", "sync-jobs.json");
        }
        else if (normalized.StartsWith("data/", StringComparison.OrdinalIgnoreCase))
        {
            relative = normalized.Replace('/', Path.DirectorySeparatorChar);
        }
        else
        {
            return null;
        }

        var root = Path.GetFullPath(contentRoot);
        var full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("De backup bevat een ongeldig pad.");
        }

        return full;
    }

    private static void TryRollback(string rollback, string dataDir, string settingsPath)
    {
        var rolledData = Path.Combine(rollback, "data");
        if (Directory.Exists(rolledData))
        {
            if (Directory.Exists(dataDir))
            {
                Directory.Delete(dataDir, recursive: true);
            }

            CopyDirectory(rolledData, dataDir);
        }

        var rolledSettings = Path.Combine(rollback, "appsettings.json");
        if (File.Exists(rolledSettings))
        {
            CopyShared(rolledSettings, settingsPath);
        }
    }

    private static void DeleteWalFiles(string dataDir)
    {
        if (!Directory.Exists(dataDir))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(dataDir, "syncstate.db-*", SearchOption.TopDirectoryOnly))
        {
            File.Delete(file);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            CopyShared(file, Path.Combine(destination, relative));
        }
    }

    private static void CopyShared(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        input.CopyTo(output);
    }

    private static void AddFile(ZipArchive archive, string fullPath, string entryName)
    {
        var entry = archive.CreateEntry(entryName.Replace('\\', '/'), CompressionLevel.Optimal);
        using var input = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var output = entry.Open();
        input.CopyTo(output);
    }

    private static byte[] ExtractMultipartFile(byte[] body, string contentType)
    {
        var boundary = ReadBoundary(contentType);
        var marker = Encoding.UTF8.GetBytes("--" + boundary);
        var headerBreak = Encoding.ASCII.GetBytes("\r\n\r\n");
        var search = 0;
        byte[]? file = null;

        while (search < body.Length)
        {
            var markerAt = IndexOf(body, marker, search);
            if (markerAt < 0)
            {
                break;
            }

            var afterMarker = markerAt + marker.Length;
            if (afterMarker + 1 < body.Length && body[afterMarker] == (byte)'-' && body[afterMarker + 1] == (byte)'-')
            {
                break;
            }

            var headersAt = afterMarker;
            if (headersAt + 1 < body.Length && body[headersAt] == (byte)'\r' && body[headersAt + 1] == (byte)'\n')
            {
                headersAt += 2;
            }

            var headerEnd = IndexOf(body, headerBreak, headersAt);
            if (headerEnd < 0)
            {
                break;
            }

            var headers = Encoding.UTF8.GetString(body, headersAt, headerEnd - headersAt);
            var dataStart = headerEnd + headerBreak.Length;
            var next = IndexOf(body, marker, dataStart);
            var dataEnd = next < 0 ? body.Length : next;
            if (dataEnd >= 2 && body[dataEnd - 2] == (byte)'\r' && body[dataEnd - 1] == (byte)'\n')
            {
                dataEnd -= 2;
            }

            if (headers.Contains("filename=", StringComparison.OrdinalIgnoreCase))
            {
                file = body[dataStart..dataEnd];
                break;
            }

            search = next < 0 ? body.Length : next;
        }

        if (file is null || file.Length == 0)
        {
            throw new ArgumentException("De upload bevat geen zip-bestand.");
        }

        return file;
    }

    private static string ReadBoundary(string contentType)
    {
        const string key = "boundary=";
        var index = contentType.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            throw new ArgumentException("De upload mist een multipart-boundary.");
        }

        var boundary = contentType[(index + key.Length)..].Trim().Trim('"');
        var semicolon = boundary.IndexOf(';');
        if (semicolon >= 0)
        {
            boundary = boundary[..semicolon].Trim().Trim('"');
        }

        if (string.IsNullOrWhiteSpace(boundary))
        {
            throw new ArgumentException("De upload mist een multipart-boundary.");
        }

        return boundary;
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int start)
    {
        if (needle.Length == 0 || start >= haystack.Length)
        {
            return -1;
        }

        for (var i = start; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return i;
            }
        }

        return -1;
    }

    private static IReadOnlyList<string> ListLanAddresses()
    {
        var addresses = new List<string>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback)
            {
                continue;
            }

            foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork)
                {
                    continue;
                }

                if (IPAddress.IsLoopback(unicast.Address))
                {
                    continue;
                }

                var text = unicast.Address.ToString();
                if (text.StartsWith("169.254.", StringComparison.Ordinal) || addresses.Contains(text))
                {
                    continue;
                }

                addresses.Add(text);
            }
        }

        return addresses;
    }

    private sealed record ZipEntryPayload(string Name, byte[] Content);
}

public sealed class ListenPortUpdateRequest
{
    public int Port { get; set; }
}
