using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;

namespace MuscleCuties.Core.Tests;

public class DatabaseFixture : IDisposable
{
    public AppDatabase Db { get; }

    public DatabaseFixture()
    {
        var options = new DbContextOptionsBuilder<AppDatabase>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        Db = new AppDatabase(options);
        Db.Database.OpenConnection();
        Db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Db.Database.CloseConnection();
        Db.Dispose();
    }
}
