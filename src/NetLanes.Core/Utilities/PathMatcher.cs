using System.Text.RegularExpressions;

namespace NetLanes.Core.Utilities;

public static class PathMatcher
{
    private static readonly Regex LongPathPrefixRegex = new(@"^\\\\\?\\", RegexOptions.Compiled);

    public static string Normalize(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return string.Empty;
        }

        string path = rawPath.Trim();

        // Strip enclosing quotes if present
        if (path.Length >= 2 && path.StartsWith('\"') && path.EndsWith('\"'))
        {
            path = path[1..^1].Trim();
        }

        // Expand environment variables (e.g. %SystemRoot%, %ProgramFiles%)
        try
        {
            path = Environment.ExpandEnvironmentVariables(path);
        }
        catch
        {
            // Ignore environment expansion failures
        }

        // Strip \\?\ long path prefix if present
        path = LongPathPrefixRegex.Replace(path, string.Empty);

        // Normalize directory separators
        path = path.Replace('/', '\\');

        // Resolve relative paths if valid
        try
        {
            if (Path.IsPathRooted(path))
            {
                path = Path.GetFullPath(path);
            }
        }
        catch
        {
            // Retain formatted path if GetFullPath fails on special device namespaces
        }

        // Trim trailing directory separators unless root
        if (path.Length > 3 && path.EndsWith('\\'))
        {
            path = path.TrimEnd('\\');
        }

        // Windows paths are case-insensitive
        return path.ToLowerInvariant();
    }

    public static bool AreEqual(string? path1, string? path2)
    {
        string norm1 = Normalize(path1);
        string norm2 = Normalize(path2);

        if (string.IsNullOrEmpty(norm1) || string.IsNullOrEmpty(norm2))
        {
            return false;
        }

        return string.Equals(norm1, norm2, StringComparison.Ordinal);
    }

    public static string GetFileName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        try
        {
            return Path.GetFileName(path.Trim('\"'));
        }
        catch
        {
            int lastSlash = Math.Max(path.LastIndexOf('\\'), path.LastIndexOf('/'));
            return lastSlash >= 0 ? path[(lastSlash + 1)..] : path;
        }
    }
}
