using System.Collections.Concurrent;

namespace TrimbleConnector.Services;

public sealed class SyncLogBuffer
{
    private const int Capacity = 80;
    private readonly ConcurrentQueue<string> _entries = new();

    public void Add(string message)
    {
        var timeStamp = DateTimeOffset.Now.ToString("dd-MM-yyyy, HH:mm:ss");
        var line = $"{timeStamp} {message}";
        _entries.Enqueue(line);
        while (_entries.Count > Capacity && _entries.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyList<string> Snapshot() => _entries.ToArray();
}
