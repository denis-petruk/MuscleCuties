namespace MuscleCuties.Core.Services.ExerciseMedia;

/// <summary>
///     Canonical local paths and URI helpers for exercise media (vector SVGs and Lottie JSON).
///     Enforces offline-first local file references and guards against path traversal.
/// </summary>
public static class ExerciseMediaPaths
{
    public const string SvgFolder = "svg";
    public const string LottieFolder = "lottie";

    public static string Normalize(string path)
    {
        if (!Path.IsPathFullyQualified(path))
            return path;

        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath)!;
        var parts = fullPath[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        var resolved = root;
        foreach (var part in parts)
        {
            resolved = Path.Combine(resolved, part);
            var info = new FileInfo(resolved);
            if (info.LinkTarget is not null)
                resolved = info.ResolveLinkTarget(true)?.FullName
                           ?? throw new IOException("An exercise media path contains an unresolved link.");
        }

        return resolved;
    }

    public static bool IsWithinRoot(string path, string? rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory) || !Path.IsPathFullyQualified(path))
            return false;
        var root = Normalize(rootDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalized = Normalize(path);
        return normalized.StartsWith(root + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    public static bool IsLocalMedia(string? pathOrUri)
    {
        if (string.IsNullOrWhiteSpace(pathOrUri))
            return true;

        var trimmed = pathOrUri.Trim();
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return false;

        if (trimmed.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("Resources/Raw/", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("Resources\\Raw\\", StringComparison.OrdinalIgnoreCase))
            return true;

        return Path.IsPathFullyQualified(trimmed);
    }

    public static string ToLocalFileUri(string localFilePath)
    {
        if (string.IsNullOrWhiteSpace(localFilePath))
            return string.Empty;

        var normalized = Normalize(localFilePath);
        var uri = new Uri(normalized);
        return uri.AbsoluteUri;
    }

    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "unnamed";

        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = fileName.Trim()
            .Select(c => invalidChars.Contains(c) || char.IsWhiteSpace(c) ? '_' : char.ToLowerInvariant(c))
            .ToArray();

        var sanitized = new string(chars);
        return string.IsNullOrWhiteSpace(sanitized) ? "media" : sanitized;
    }
}
