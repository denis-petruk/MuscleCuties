using System.Text.Json;

namespace MuscleCuties.Core.Services.Profile;

/// <summary>Canonical local paths shared by photo storage and account deletion.</summary>
public static class ProfileMediaPaths
{
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
            // Resolve actual filesystem aliases (such as macOS /var), without
            // assuming that removing a '/private' prefix preserves identity.
            var info = new FileInfo(resolved);
            if (info.LinkTarget is not null)
                resolved = info.ResolveLinkTarget(true)?.FullName
                           ?? throw new IOException("A photo path contains an unresolved link.");
        }

        return resolved;
    }

    public static string? SnapshotImagePath(string profileJson)
    {
        if (string.IsNullOrWhiteSpace(profileJson)) return null;
        using var document = JsonDocument.Parse(profileJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
        foreach (var property in document.RootElement.EnumerateObject())
            if (property.Name.Equals("ProfileImagePath", StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.String)
                return property.Value.GetString();
        return null;
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
}
