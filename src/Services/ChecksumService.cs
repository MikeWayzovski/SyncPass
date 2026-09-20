using System.Security.Cryptography;

namespace TrimbleConnector.Services;

public static class ChecksumService
{
    public static Task<string> ComputeMd5Async(string filePath, CancellationToken cancellationToken)
    {
#pragma warning disable CA5351 // MD5 matches Trimble Connect file hash metadata
        return ComputeHashAsync(filePath, (stream, token) => MD5.HashDataAsync(stream, token), cancellationToken);
#pragma warning restore CA5351
    }

    public static Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken) =>
        ComputeHashAsync(filePath, (stream, token) => SHA256.HashDataAsync(stream, token), cancellationToken);

    private static async Task<string> ComputeHashAsync(
        string filePath,
        Func<Stream, CancellationToken, ValueTask<byte[]>> hashAsync,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 128,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        var hash = await hashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool EqualsOrdinalIgnoreCase(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left)
        && !string.IsNullOrWhiteSpace(right)
        && string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string value) => value.Replace("-", string.Empty).Trim();
}
