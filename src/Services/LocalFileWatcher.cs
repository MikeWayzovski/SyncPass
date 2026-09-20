using System.Collections.Concurrent;
using TrimbleConnector.Models;

namespace TrimbleConnector.Services;

public interface ILocalFileWatcher : IDisposable
{
    event EventHandler<LocalFileChange>? Changed;

    void Watch(string folderPath);

    IReadOnlyCollection<LocalFileChange> Drain();
}

/// <summary>
/// Watches a local or UNC folder and coalesces FileSystemWatcher noise from CAD/BIM
/// save sequences (Ctrl+S). Consumers should still call <see cref="WaitForUnlockAsync"/>
/// before reading or uploading a file.
/// </summary>
public sealed class LocalFileWatcher : ILocalFileWatcher
{
    private static readonly TimeSpan DefaultDebounce = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan LockRetryDelay = TimeSpan.FromMilliseconds(500);
    private const int LockRetryAttempts = 24;

    private readonly ILogger<LocalFileWatcher> _logger;
    private readonly ConcurrentDictionary<string, PendingChange> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<LocalFileChange> _ready = new();
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly object _watcherLock = new();
    private readonly Timer _flushTimer;

    public LocalFileWatcher(ILogger<LocalFileWatcher> logger)
    {
        _logger = logger;
        _flushTimer = new Timer(FlushDue, null, DefaultDebounce, DefaultDebounce);
    }

    public event EventHandler<LocalFileChange>? Changed;

    public void Watch(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new ArgumentException("Folder path is required.", nameof(folderPath));
        }

        Directory.CreateDirectory(folderPath);

        lock (_watcherLock)
        {
            if (_watchers.Any(w => string.Equals(w.Path, folderPath, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var watcher = new FileSystemWatcher(folderPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName
                    | NotifyFilters.DirectoryName
                    | NotifyFilters.LastWrite
                    | NotifyFilters.Size
                    | NotifyFilters.CreationTime,
                InternalBufferSize = 64 * 1024
            };

            watcher.Created += (_, e) => Enqueue(e.FullPath, LocalChangeKind.Created);
            watcher.Changed += (_, e) => Enqueue(e.FullPath, LocalChangeKind.Changed);
            watcher.Deleted += (_, e) => Enqueue(e.FullPath, LocalChangeKind.Deleted);
            watcher.Renamed += (_, e) => Enqueue(e.FullPath, LocalChangeKind.Renamed, e.OldFullPath);
            watcher.Error += (_, e) =>
                _logger.LogError(e.GetException(), "FileSystemWatcher error under {Folder}.", folderPath);

            watcher.EnableRaisingEvents = true;
            _watchers.Add(watcher);
            _logger.LogInformation("Watching local folder {Folder}.", folderPath);
        }
    }

    public IReadOnlyCollection<LocalFileChange> Drain()
    {
        FlushDue(null);

        var batch = new List<LocalFileChange>();
        while (_ready.TryDequeue(out var change))
        {
            batch.Add(change);
        }

        return batch;
    }

    public static async Task<bool> WaitForUnlockAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        for (var attempt = 0; attempt < LockRetryAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.None);
                return stream.Length >= 0;
            }
            catch (IOException)
            {
                await Task.Delay(LockRetryDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
                await Task.Delay(LockRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        return false;
    }

    public static bool ShouldIgnore(string path)
    {
        var name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(name))
        {
            return true;
        }

        if (name.StartsWith("~$", StringComparison.Ordinal) || name.StartsWith('.'))
        {
            return true;
        }

        var extension = Path.GetExtension(name);
        if (name.Contains(".conflict-", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return extension.Equals(".tmp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".temp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bak", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".dwl", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".dwl2", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".sv$", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".lck", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase)
            || name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".DS_Store", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{Path.DirectorySeparatorChar}.trimble-connector{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{Path.AltDirectorySeparatorChar}.trimble-connector{Path.AltDirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    private void Enqueue(string path, LocalChangeKind kind, string? oldPath = null)
    {
        if (ShouldIgnore(path) || Directory.Exists(path))
        {
            return;
        }

        _pending.AddOrUpdate(
            path,
            _ => new PendingChange(kind, oldPath, DateTimeOffset.UtcNow),
            (_, existing) => existing with
            {
                Kind = Merge(existing.Kind, kind),
                OldPath = oldPath ?? existing.OldPath,
                LastSeen = DateTimeOffset.UtcNow
            });
    }

    private static LocalChangeKind Merge(LocalChangeKind current, LocalChangeKind incoming)
    {
        if (incoming == LocalChangeKind.Deleted)
        {
            return LocalChangeKind.Deleted;
        }

        if (current == LocalChangeKind.Deleted && incoming is LocalChangeKind.Created or LocalChangeKind.Renamed)
        {
            return LocalChangeKind.Changed;
        }

        return current == LocalChangeKind.Created ? LocalChangeKind.Created : incoming;
    }

    private void FlushDue(object? _)
    {
        var cutoff = DateTimeOffset.UtcNow - DefaultDebounce;
        foreach (var pair in _pending)
        {
            if (pair.Value.LastSeen > cutoff)
            {
                continue;
            }

            if (_pending.TryRemove(pair.Key, out var pending))
            {
                var change = new LocalFileChange(pair.Key, pending.Kind, pending.OldPath);
                _ready.Enqueue(change);
                Changed?.Invoke(this, change);
            }
        }
    }

    public void Dispose()
    {
        _flushTimer.Dispose();
        lock (_watcherLock)
        {
            foreach (var watcher in _watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            _watchers.Clear();
        }
    }

    private sealed record PendingChange(LocalChangeKind Kind, string? OldPath, DateTimeOffset LastSeen);
}
