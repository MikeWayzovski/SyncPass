using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace TrimbleConnector.Services;

public sealed class HostProbe
{
    public string Host { get; init; } = string.Empty;

    public bool DnsResolved { get; init; }

    public bool Reachable { get; init; }

    public string Detail { get; init; } = string.Empty;
}

public sealed class SystemDiagnostics
{
    public IReadOnlyList<HostProbe> Hosts { get; init; } = [];

    public bool DataDirectoryWritable { get; init; }

    public bool DatabaseWritable { get; init; }

    public string StorageDetail { get; init; } = string.Empty;
}

/// <summary>
/// Checks Trimble DNS and reachability, plus local write access for the sync database.
/// </summary>
public sealed class SystemHealthService
{
    private static readonly string[] Hosts = ["id.trimble.com", "app.connect.trimble.com"];

    private readonly IHostEnvironment _environment;

    public SystemHealthService(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<SystemDiagnostics> CheckAsync(CancellationToken cancellationToken)
    {
        var hosts = new List<HostProbe>(Hosts.Length);
        foreach (var host in Hosts)
        {
            hosts.Add(await ProbeHostAsync(host, cancellationToken).ConfigureAwait(false));
        }

        var (dataWritable, databaseWritable, detail) = ProbeStorage();
        return new SystemDiagnostics
        {
            Hosts = hosts,
            DataDirectoryWritable = dataWritable,
            DatabaseWritable = databaseWritable,
            StorageDetail = detail
        };
    }

    private async Task<HostProbe> ProbeHostAsync(string host, CancellationToken cancellationToken)
    {
        try
        {
            var addresses = await System.Net.Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
            if (addresses.Length == 0)
            {
                return new HostProbe { Host = host, Detail = "DNS gaf geen adres terug." };
            }
        }
        catch (Exception)
        {
            return new HostProbe { Host = host, Detail = "DNS-naam kon niet worden opgelost." };
        }

        var (reachable, detail) = await ProbeReachableAsync(host, cancellationToken).ConfigureAwait(false);
        return new HostProbe
        {
            Host = host,
            DnsResolved = true,
            Reachable = reachable,
            Detail = detail
        };
    }

    private static async Task<(bool Reachable, string Detail)> ProbeReachableAsync(string host, CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, 3000).ConfigureAwait(false);
            if (reply.Status == IPStatus.Success)
            {
                return (true, $"Ping {reply.RoundtripTime} ms.");
            }
        }
        catch (Exception)
        {
            // ICMP is often blocked. A TCP check on 443 still shows whether Trimble is reachable.
        }

        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            await client.ConnectAsync(host, 443, timeout.Token).ConfigureAwait(false);
            return (true, "HTTPS-poort 443 is bereikbaar.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (false, "Geen antwoord op poort 443.");
        }
        catch (Exception)
        {
            return (false, "Host is niet bereikbaar.");
        }
    }

    private (bool DataWritable, bool DatabaseWritable, string Detail) ProbeStorage()
    {
        var directory = Path.Combine(_environment.ContentRootPath, "data");
        var database = Path.Combine(directory, "syncstate.db");
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, ".health-probe");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
        }
        catch (Exception)
        {
            return (false, false, "De map data/ is niet beschrijfbaar.");
        }

        try
        {
            if (File.Exists(database))
            {
                using var stream = new FileStream(database, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            }

            return (true, true, "data/ en de sync-database zijn beschrijfbaar.");
        }
        catch (Exception)
        {
            return (true, false, "data/ is beschrijfbaar, de sync-database niet.");
        }
    }
}
