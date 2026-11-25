using MuscleCuties.Infrastructure.Services.Media;

namespace MuscleCuties.Core.Services.ExerciseMedia;

/// <summary>
///     Canonical local paths and URI helpers for exercise media (vector SVGs and Lottie JSON).
///     Enforces offline-first local file references and guards against path traversal.
/// </summary>
public static class ExerciseMediaPaths
{
    public const string SvgFolder = "svg";
    public const string LottieFolder = "lottie";

    /// <summary>A media path in the form the app compares paths by, so the same file is recognised however it was written.</summary>
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

    /// <summary>Whether a path is a file on this device rather than a remote URL.</summary>
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

    /// <summary>A local path as a file URI, which is what the image views need.</summary>
    public static string ToLocalFileUri(string localFilePath)
    {
        if (string.IsNullOrWhiteSpace(localFilePath))
            return string.Empty;

        var normalized = Normalize(localFilePath);
        var uri = new Uri(normalized);
        return uri.AbsoluteUri;
    }

    /// <summary>A safe file name, because exercise names reach the file system and may contain separators.</summary>
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
