using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Cycle;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Repositories.Cycle;

namespace MuscleCuties.Core.Tests;

public class CyclePhaseNeighborQueryTests
{
    [Fact]
    public async Task NeighborQueries_IgnoreSameDayAndOtherUsers()
    {
        SQLitePCL.Batteries_V2.Init();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDatabase>()
            .UseSqlite(connection)
            .Options;
        await using var database = new AppDatabase(options);
        await database.Database.EnsureCreatedAsync();

        var firstUser = new User { Email = "first@example.test", PasswordHash = "test" };
        var otherUser = new User { Email = "other@example.test", PasswordHash = "test" };
        await database.Users.AddRangeAsync(firstUser, otherUser);
        await database.SaveChangesAsync();

        var targetDate = new DateTime(2026, 9, 28);
        var previous = new CyclePhaseLog
        {
            UserId = firstUser.Id,
            Phase = CyclePhase.Follicular,
            LoggedAt = targetDate.AddDays(-1).AddHours(20)
        };
        var next = new CyclePhaseLog
        {
            UserId = firstUser.Id,
            Phase = CyclePhase.Luteal,
            LoggedAt = targetDate.AddDays(1).AddHours(9)
        };
        await database.CyclePhaseLogs.AddRangeAsync(
            previous,
            new CyclePhaseLog { UserId = firstUser.Id, Phase = CyclePhase.Ovulatory, LoggedAt = targetDate.AddHours(8) },
            next,
            new CyclePhaseLog { UserId = otherUser.Id, Phase = CyclePhase.Menstrual, LoggedAt = targetDate.AddDays(-2) },
            new CyclePhaseLog { UserId = otherUser.Id, Phase = CyclePhase.Menstrual, LoggedAt = targetDate.AddDays(2) });
        await database.SaveChangesAsync();

        var repository = new CycleRepository(database);
        var actualPrevious = await repository.GetLatestPhaseLogBeforeDateAsync(firstUser.Id, targetDate);
        var actualNext = await repository.GetNextPhaseLogAfterDateAsync(firstUser.Id, targetDate);

        Assert.Equal(previous.Id, actualPrevious?.Id);
        Assert.Equal(next.Id, actualNext?.Id);
    }
}
