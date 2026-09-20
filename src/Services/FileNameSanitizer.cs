namespace TrimbleConnector.Services;

/// <summary>
/// Strips characters that Trimble Connect rejects in file names
/// (for example ':' from ISO timestamps) before upload.
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
        return string.Create(name.Length, name, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                span[i] = IsInvalid(source[i]) ? '_' : source[i];
            }
        });
    }

    private static bool IsInvalid(char value) =>
        value is ':' or '*' or '?' or '"' or '<' or '>' or '|' or '\\' or '/';
}
