namespace TrimbleConnector.Config;

internal static class RemotePath
{
    public static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var parts = path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? "/" : "/" + string.Join('/', parts);
    }

    public static bool IsRoot(string? path) =>
        string.IsNullOrWhiteSpace(path) || Normalize(path) == "/";

    public static string Copy(string? path) =>
        string.IsNullOrWhiteSpace(path) ? string.Empty : Normalize(path);

    public static string Combine(string? parent, string? name)
    {
        var root = Normalize(parent);
        if (string.IsNullOrWhiteSpace(name))
        {
            return root;
        }

        return root == "/" ? Normalize("/" + name) : Normalize(root + "/" + name);
    }
}
