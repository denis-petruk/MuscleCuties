using Foundation;

namespace MuscleCuties.UI.Services.Security;

/// <summary>iOS half of the backup exclusion: the database file, its write-ahead and shared-memory</summary>
/// companions, and the directory that holds them.</summary>
public static partial class DatabaseBackupProtection
{
    /// <summary>Keeps the local database out of iCloud backups.</summary>
    static partial void ExcludeFromBackupPlatform(string path)
    {
        ApplySkipBackup(path);
        ApplySkipBackup(path + "-wal");
        ApplySkipBackup(path + "-shm");

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            ApplySkipBackup(directory);
    }

    /// <summary>Sets the iCloud backup exclusion flag. A path that does not exist is skipped; a failed flag</summary>
    /// raises, because silently leaving health data backed up is the worse outcome.</summary>
    private static void ApplySkipBackup(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
            return;

        using var url = NSUrl.FromFilename(path);
        using var excluded = NSObject.FromObject(true);
        if (!url.SetResource(NSUrl.IsExcludedFromBackupKey, excluded, out var error))
            throw new IOException($"Unable to exclude local data from backup: {error?.LocalizedDescription}");
    }
}
