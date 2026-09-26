namespace MuscleCuties.App.Services.Security;

public static partial class DatabaseBackupProtection
{
    public static void ExcludeFromBackup(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        ExcludeFromBackupPlatform(path);
    }

    static partial void ExcludeFromBackupPlatform(string path);
}
