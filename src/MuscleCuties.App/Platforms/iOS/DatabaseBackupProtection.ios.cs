using Foundation;
using MuscleCuties.App.Services.Security;

namespace MuscleCuties.App.Services.Security;

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
        url.SetResource(NSUrl.IsExcludedFromBackupKey, NSObject.FromObject(true), out _);
    }
}
