using MuscleCuties.Core.Data;
using MuscleCuties.App.Services.Security;

namespace MuscleCuties.App.Services;

public sealed class MauiDbPathProvider : IDbPathProvider
{
    private const string DatabaseFileName = "musclecuties.db3";

    public string GetDatabasePath()
    {
        Directory.CreateDirectory(FileSystem.AppDataDirectory);
        var path = Path.Combine(FileSystem.AppDataDirectory, DatabaseFileName);
        DatabaseBackupProtection.ExcludeFromBackup(path);
        return path;
    }
}
