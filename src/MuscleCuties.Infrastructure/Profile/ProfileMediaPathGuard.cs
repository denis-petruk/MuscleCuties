namespace MuscleCuties.Infrastructure.Profile;

internal static class ProfileMediaPathGuard
{
    public static bool IsProtectedLocalPath(string imagePath, string databasePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || string.IsNullOrWhiteSpace(databasePath) ||
            databasePath == ":memory:" || !Path.IsPathFullyQualified(imagePath))
            return false;

        var fullImagePath = Path.GetFullPath(imagePath);
        var fullDatabasePath = Path.GetFullPath(databasePath);
        var root = Path.GetDirectoryName(fullDatabasePath);
        if (string.IsNullOrWhiteSpace(root))
            return false;
        var relative = Path.GetRelativePath(root, fullImagePath);
        return relative is not ".." &&
               !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
               !Path.IsPathRooted(relative) &&
               !new[] { "", "-wal", "-shm", "-journal" }.Any(suffix =>
                   string.Equals(fullImagePath, fullDatabasePath + suffix, StringComparison.OrdinalIgnoreCase)) &&
               File.Exists(fullImagePath);
    }
}
