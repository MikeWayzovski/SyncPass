using Microsoft.Data.Sqlite;
using TrimbleConnector.Models;

namespace TrimbleConnector.Services;

/// <summary>
/// Local SQLite catalog of synced files. The local network folder is the
/// single source of truth; this store records hashes and Trimble ids so the
/// engine can skip redundant transfers.
/// </summary>
public sealed class SyncStateRepository : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ILogger<SyncStateRepository> _logger;
    private readonly object _gate = new();
    private bool _disposed;

    public SyncStateRepository(IHostEnvironment environment, ILogger<SyncStateRepository> logger)
    {
        _logger = logger;
        var directory = Path.Combine(environment.ContentRootPath, "data");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "syncstate.db");
        _connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString());
        _connection.Open();
        using (var pragma = _connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA journal_mode=WAL;";
            pragma.ExecuteNonQuery();
        }

        EnsureSchema();
        _logger.LogInformation("Sync state database ready at {Path}.", path);
    }

    public FileState? Get(string projectId, string relativePath)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText =
                """
                SELECT RelativePath, ProjectId, LocalHash, LocalLastWriteTimeUtc,
                       TrimbleFileId, TrimbleVersionId, LastSyncedAtUtc
                FROM FileStates
                WHERE ProjectId = $projectId AND RelativePath = $relativePath
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("$projectId", projectId);
            command.Parameters.AddWithValue("$relativePath", Normalize(relativePath));
            using var reader = command.ExecuteReader();
            return reader.Read() ? Read(reader) : null;
        }
    }

    public IReadOnlyList<FileState> GetAll(string projectId)
    {
        lock (_gate)
        {
            var rows = new List<FileState>();
            using var command = _connection.CreateCommand();
            command.CommandText =
                """
                SELECT RelativePath, ProjectId, LocalHash, LocalLastWriteTimeUtc,
                       TrimbleFileId, TrimbleVersionId, LastSyncedAtUtc
                FROM FileStates
                WHERE ProjectId = $projectId;
                """;
            command.Parameters.AddWithValue("$projectId", projectId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(Read(reader));
            }

            return rows;
        }
    }

    public void Upsert(FileState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO FileStates (
                    RelativePath, ProjectId, LocalHash, LocalLastWriteTimeUtc,
                    TrimbleFileId, TrimbleVersionId, LastSyncedAtUtc)
                VALUES (
                    $relativePath, $projectId, $localHash, $localLastWrite,
                    $trimbleFileId, $trimbleVersionId, $lastSynced)
                ON CONFLICT(ProjectId, RelativePath) DO UPDATE SET
                    LocalHash = excluded.LocalHash,
                    LocalLastWriteTimeUtc = excluded.LocalLastWriteTimeUtc,
                    TrimbleFileId = excluded.TrimbleFileId,
                    TrimbleVersionId = excluded.TrimbleVersionId,
                    LastSyncedAtUtc = excluded.LastSyncedAtUtc;
                """;
            command.Parameters.AddWithValue("$relativePath", Normalize(state.RelativePath));
            command.Parameters.AddWithValue("$projectId", state.ProjectId);
            command.Parameters.AddWithValue("$localHash", (object?)state.LocalHash ?? DBNull.Value);
            command.Parameters.AddWithValue("$localLastWrite", ToText(state.LocalLastWriteTimeUtc));
            command.Parameters.AddWithValue("$trimbleFileId", (object?)state.TrimbleFileId ?? DBNull.Value);
            command.Parameters.AddWithValue("$trimbleVersionId", (object?)state.TrimbleVersionId ?? DBNull.Value);
            command.Parameters.AddWithValue("$lastSynced", ToText(state.LastSyncedAtUtc));
            command.ExecuteNonQuery();
        }
    }

    public void Delete(string projectId, string relativePath)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText =
                """
                DELETE FROM FileStates
                WHERE ProjectId = $projectId AND RelativePath = $relativePath;
                """;
            command.Parameters.AddWithValue("$projectId", projectId);
            command.Parameters.AddWithValue("$relativePath", Normalize(relativePath));
            command.ExecuteNonQuery();
        }
    }

    public void LogActivity(string projectId, string action, string filePath, string details)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO ActivityLogs (TimestampUtc, ProjectId, Action, FilePath, Details)
                VALUES ($timestamp, $projectId, $action, $filePath, $details);
                """;
            command.Parameters.AddWithValue("$timestamp", ToText(DateTime.UtcNow));
            command.Parameters.AddWithValue("$projectId", projectId ?? string.Empty);
            command.Parameters.AddWithValue("$action", action ?? string.Empty);
            command.Parameters.AddWithValue("$filePath", filePath ?? string.Empty);
            command.Parameters.AddWithValue("$details", details ?? string.Empty);
            command.ExecuteNonQuery();
        }
    }

    public IReadOnlyList<ActivityLog> GetRecentActivities(int limit = 50)
    {
        if (limit < 1)
        {
            limit = 1;
        }

        lock (_gate)
        {
            var rows = new List<ActivityLog>();
            using var command = _connection.CreateCommand();
            command.CommandText =
                """
                SELECT Id, TimestampUtc, ProjectId, Action, FilePath, Details
                FROM ActivityLogs
                ORDER BY Id DESC
                LIMIT $limit;
                """;
            command.Parameters.AddWithValue("$limit", limit);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new ActivityLog
                {
                    Id = reader.GetInt64(0),
                    TimestampUtc = ParseTime(reader.IsDBNull(1) ? null : reader.GetString(1)),
                    ProjectId = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Action = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    FilePath = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Details = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
                });
            }

            return rows;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connection.Dispose();
    }

    private void EnsureSchema()
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS FileStates (
                RelativePath TEXT NOT NULL,
                ProjectId TEXT NOT NULL,
                LocalHash TEXT,
                LocalLastWriteTimeUtc TEXT,
                TrimbleFileId TEXT,
                TrimbleVersionId TEXT,
                LastSyncedAtUtc TEXT,
                PRIMARY KEY (ProjectId, RelativePath)
            );
            CREATE INDEX IF NOT EXISTS IX_FileStates_TrimbleFileId
                ON FileStates (TrimbleFileId);
            CREATE TABLE IF NOT EXISTS ActivityLogs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TimestampUtc TEXT NOT NULL,
                ProjectId TEXT,
                Action TEXT,
                FilePath TEXT,
                Details TEXT
            );
            CREATE INDEX IF NOT EXISTS IX_ActivityLogs_TimestampUtc
                ON ActivityLogs (TimestampUtc);
            """;
        command.ExecuteNonQuery();
    }

    private static FileState Read(SqliteDataReader reader) => new()
    {
        RelativePath = reader.GetString(0),
        ProjectId = reader.GetString(1),
        LocalHash = reader.IsDBNull(2) ? null : reader.GetString(2),
        LocalLastWriteTimeUtc = ParseTime(reader.IsDBNull(3) ? null : reader.GetString(3)),
        TrimbleFileId = reader.IsDBNull(4) ? null : reader.GetString(4),
        TrimbleVersionId = reader.IsDBNull(5) ? null : reader.GetString(5),
        LastSyncedAtUtc = ParseTime(reader.IsDBNull(6) ? null : reader.GetString(6))
    };

    public static string Normalize(string relativePath) =>
        (relativePath ?? string.Empty).Replace('\\', '/').TrimStart('/');

    private static string ToText(DateTime value) =>
        (value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime()).ToString("o");

    private static DateTime ParseTime(string? value) =>
        DateTime.TryParse(value, out var parsed)
            ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
            : DateTime.MinValue;
}
