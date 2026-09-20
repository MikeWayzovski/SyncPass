namespace TrimbleConnector.Services;

/// <summary>
/// Applies Trimble Connect file naming conventions before upload.
/// Forbidden characters: &lt; &gt; : " / \ | ? *
/// Forbidden patterns: ".." and a trailing period. Parentheses are allowed.
/// The original file extension is preserved.
/// </summary>
public static class FileNameSanitizer
{
    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        var name = Path.GetFileName(fileName.Trim());
        var stem = Path.GetFileNameWithoutExtension(name);
        var extension = Path.GetExtension(name);
        return SanitizeStem(stem) + extension;
    }

    private static string SanitizeStem(string value)
    {
        var sanitized = string.Create(value.Length, value, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                span[i] = IsInvalid(source[i]) ? '_' : source[i];
            }
        });

        while (sanitized.Contains("..", StringComparison.Ordinal))
        {
            sanitized = sanitized.Replace("..", "_", StringComparison.Ordinal);
        }

        sanitized = sanitized.TrimEnd('.');
        return string.IsNullOrWhiteSpace(sanitized) ? "_" : sanitized;
    }

    private static bool IsInvalid(char value) =>
        value is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*';
}
