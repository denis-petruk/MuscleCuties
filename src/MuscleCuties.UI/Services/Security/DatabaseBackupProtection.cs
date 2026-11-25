namespace MuscleCuties.UI.Services.Security;

/// <summary>Marks the local database file as not-for-backup, so a copy of it is never uploaded to</summary>
/// the platform's backup service. The platform half is supplied per OS.</summary>
public static partial class DatabaseBackupProtection
{
    /// <summary>Applies the exclusion for the given file; a blank path is ignored rather than thrown.</summary>
    public static void ExcludeFromBackup(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        ExcludeFromBackupPlatform(path);
    }

    /// <summary>Keeps the path out of backups on this platform.</summary>
    static partial void ExcludeFromBackupPlatform(string path);
}
