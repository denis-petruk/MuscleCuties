namespace MuscleCuties.Infrastructure.Services.Media;

/// <summary>
/// A media path is inside a root only when both are fully qualified and the normalized path sits under
/// the normalized root. Profile and exercise media both enforce this boundary.
/// </summary>
public static class MediaPathSafety
{
    /// <summary>Whether a path really stays inside a root directory, so a crafted name cannot read or delete outside it.</summary>
    public static bool IsWithinRoot(string path, string? rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory) || !Path.IsPathFullyQualified(path))
            return false;
        var root = Normalize(rootDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalized = Normalize(path);
        return normalized.StartsWith(root + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    /// <summary>Normalizes a path so two spellings of the same file compare equal.</summary>
    private static string Normalize(string path) =>
        Path.GetFullPath(path).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
}
