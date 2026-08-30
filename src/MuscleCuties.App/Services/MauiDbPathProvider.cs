using MuscleCuties.Core.Data;

namespace MuscleCuties.App.Services;

public sealed class MauiDbPathProvider : IDbPathProvider
{
    private const string DatabaseFileName = "musclecuties.db3";

    public string GetDatabasePath() =>
        Path.Combine(FileSystem.AppDataDirectory, DatabaseFileName);
}
