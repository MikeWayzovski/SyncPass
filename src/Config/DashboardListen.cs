namespace TrimbleConnector.Config;

/// <summary>
/// Dashboard bind address. The process uses HttpListener, and reads the same
/// Kestrel URL key so appsettings stays the single place operators edit.
/// </summary>
public sealed class DashboardListenState
{
    public const string FallbackUrl = "http://*:5000";

    public string RequestedUrl { get; set; } = FallbackUrl;

    public int Port { get; set; } = 5000;

    public bool ListeningOnAllInterfaces { get; set; }
}

public static class DashboardListen
{
    public static string Resolve(IConfiguration configuration)
    {
        var configured = configuration["Kestrel:Endpoints:Http:Url"];
        var template = string.IsNullOrWhiteSpace(configured)
            ? DashboardListenState.FallbackUrl
            : configured.Trim();
        return WithPort(template, ResolvePort(configuration));
    }

    /// <summary>
    /// Port from --port / PORT, then TrimbleConnect:Port, then the Kestrel URL, then 5000.
    /// </summary>
    public static int ResolvePort(IConfiguration configuration)
    {
        if (TryPort(configuration["port"], out var fromEnvOrArg))
        {
            return fromEnvOrArg;
        }

        if (TryPort(configuration["TrimbleConnect:Port"], out var configured))
        {
            return configured;
        }

        var url = configuration["Kestrel:Endpoints:Http:Url"];
        if (!string.IsNullOrWhiteSpace(url))
        {
            var parsed = PortOf(url);
            if (parsed is > 0 and <= 65535)
            {
                return parsed;
            }
        }

        return 5000;
    }

    public static string WithPort(string url, int port)
    {
        var host = HostOf(string.IsNullOrWhiteSpace(url) ? DashboardListenState.FallbackUrl : url);
        if (string.IsNullOrWhiteSpace(host))
        {
            host = "*";
        }

        return $"http://{host}:{port}";
    }

    private static bool TryPort(string? value, out int port)
    {
        if (int.TryParse(value, out port) && port is > 0 and <= 65535)
        {
            return true;
        }

        port = 0;
        return false;
    }

    public static int PortOf(string url)
    {
        var hostPort = HostPort(url);
        if (hostPort.StartsWith('['))
        {
            var end = hostPort.IndexOf(']');
            if (end >= 0
                && end + 1 < hostPort.Length
                && hostPort[end + 1] == ':'
                && int.TryParse(hostPort[(end + 2)..], out var ipv6Port)
                && ipv6Port > 0)
            {
                return ipv6Port;
            }

            return 5000;
        }

        var colon = hostPort.LastIndexOf(':');
        if (colon > 0 && int.TryParse(hostPort[(colon + 1)..], out var port) && port > 0)
        {
            return port;
        }

        return 5000;
    }

    public static bool IsAllInterfaces(string url) =>
        HostOf(url) is "*" or "+" or "0.0.0.0" or "::" or "[::]";

    public static string HostOf(string url)
    {
        var hostPort = HostPort(url);
        if (hostPort.StartsWith('['))
        {
            var end = hostPort.IndexOf(']');
            return end >= 0 ? hostPort[..(end + 1)] : hostPort;
        }

        var colon = hostPort.LastIndexOf(':');
        return colon > 0 ? hostPort[..colon] : hostPort;
    }

    public static IReadOnlyList<string> ToPrefixes(string url)
    {
        var port = PortOf(url);
        if (IsAllInterfaces(url))
        {
            return [$"http://+:{port}/", $"http://*:{port}/"];
        }

        var host = HostOf(url);
        if (string.IsNullOrWhiteSpace(host)
            || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return [$"http://localhost:{port}/", $"http://127.0.0.1:{port}/"];
        }

        return [$"http://{host}:{port}/"];
    }

    private static string HostPort(string url)
    {
        var normalized = Normalize(url);
        var scheme = normalized.IndexOf("://", StringComparison.Ordinal);
        var rest = scheme >= 0 ? normalized[(scheme + 3)..] : normalized;
        var slash = rest.IndexOf('/');
        return slash >= 0 ? rest[..slash] : rest;
    }

    private static string Normalize(string url)
    {
        var trimmed = url.Trim();
        return trimmed.Contains("://", StringComparison.Ordinal) ? trimmed : "http://" + trimmed;
    }
}
