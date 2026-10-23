using Foundation;

namespace MuscleCuties.UI.Services.Security;

public static partial class DatabaseBackupProtection
{
    static partial void ExcludeFromBackupPlatform(string path)
    {
        ApplySkipBackup(path);
        ApplySkipBackup(path + "-wal");
        ApplySkipBackup(path + "-shm");

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            ApplySkipBackup(directory);
    }

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
