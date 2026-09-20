namespace TrimbleConnector.Services;

/// <summary>
/// Strips characters that Trimble Connect rejects in file names
/// (for example ':' from ISO timestamps, or '(' ')' from copy names)
/// before upload. The original file extension is preserved.
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
        return SanitizeSegment(stem) + extension;
    }

    private static string SanitizeSegment(string value) =>
        string.Create(value.Length, value, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                span[i] = IsInvalid(source[i]) ? '_' : source[i];
            }
        });

    private static bool IsInvalid(char value) =>
        value is ':' or '(' or ')' or '*' or '?' or '"' or '<' or '>' or '|' or '\\' or '/';
}
