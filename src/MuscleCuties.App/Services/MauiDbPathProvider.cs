using MuscleCuties.Core.Data;

namespace MuscleCuties.App.Services;

public class MauiDbPathProvider : IDbPathProvider
{
    public string GetDatabasePath() =>
        Path.Combine(FileSystem.AppDataDirectory, "musclecuties.db");
}
