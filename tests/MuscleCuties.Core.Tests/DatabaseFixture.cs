using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Cycle;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Entities.Workout;

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
        Db.Users.Add(new User { Email = "seed@test.com", PasswordHash = "x", CreatedAt = DateTime.UtcNow });
        Db.SaveChanges();
    }

    public void Dispose()
    {
        Db.Database.CloseConnection();
        Db.Dispose();
    }
}
