using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Services.Workout.Planning;

namespace MuscleCuties.Core.Tests.Services.Workout.Planning;

public class WorkoutPlanningDbFixture : IAsyncLifetime
{
    public AppDatabase Db { get; private set; } = null!;
    public ContributionLookup Contributions { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDatabase>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        Db = new AppDatabase(options);
        Db.Database.OpenConnection();
        await Db.Database.EnsureCreatedAsync();
        await Db.SeedWorkoutPlanningDataAsync();

        Contributions = new ContributionLookup(Db);
        await Contributions.LoadAsync();
    }

    public Task DisposeAsync()
    {
        Db.Database.CloseConnection();
        Db.Dispose();
        return Task.CompletedTask;
    }
}
