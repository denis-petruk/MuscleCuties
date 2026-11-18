using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Archive;
using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class WorkoutArchiveCalendarTests
{
    [PostgresFact]
    public async Task History_uses_account_local_month_and_date_at_a_utc_month_boundary()
    {
        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(PostgresTestEnvironment.RequireConnection("MC_REGISTRATION_WRITE_TEST_POSTGRES"))
            .Options;
        await using var database = new PostgresDbContext(options);
        var userId = Guid.CreateVersion7();
        var sessionId = Guid.CreateVersion7();
        var startedUtc = new DateTime(2026, 11, 1, 1, 0, 0, DateTimeKind.Utc);
        await SyntheticTenantSession.SetAsync(database, userId);
        database.Users.Add(new UserRecord
        {
            Id = userId,
            Email = $"archive-calendar-{userId:N}@example.invalid",
            Username = $"arc_{userId.ToString("N")[..10]}",
            CredentialType = "Password",
            PasswordHash = "synthetic-hash",
            CreatedAt = startedUtc,
            UpdatedAt = startedUtc
        });
        database.WorkoutAccountPlanningStates.Add(new WorkoutAccountPlanningStateRecord
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            TimeZoneId = "America/Toronto"
        });
        database.WorkoutSessionLogs.Add(new WorkoutSessionLogRecord
        {
            Id = sessionId,
            UserId = userId,
            WorkoutName = "Synthetic boundary workout",
            StartedAt = startedUtc,
            CompletedAt = startedUtc.AddMinutes(20),
            CreatedAt = startedUtc,
            UpdatedAt = startedUtc
        });
        await database.SaveChangesAsync();

        try
        {
            var archive = new PostgresWorkoutArchiveStore(database);
            var october = await archive.GetWorkoutHistoryByMonthAsync(userId, 2026, 10);
            var summary = Assert.Single(october);
            Assert.Equal(sessionId, summary.Id);
            Assert.Equal(1, await archive.GetWorkoutHistoryCountByMonthAsync(userId, 2026, 10));
            Assert.Empty(await archive.GetWorkoutHistoryByMonthAsync(userId, 2026, 11));
            Assert.Equal(0, await archive.GetWorkoutHistoryCountByMonthAsync(userId, 2026, 11));
            var month = Assert.Single(await archive.GetAvailableHistoryMonthsAsync(userId));
            Assert.Equal((2026, 10), (month.Year, month.Month));

            var detail = await archive.GetWorkoutSessionDetailAsync(userId, sessionId);
            Assert.NotNull(detail);
            Assert.Equal(startedUtc, detail.StartedAt);
            Assert.Equal(startedUtc, summary.StartedAt);
            var expectedLocal = new DateTime(2026, 10, 31, 21, 0, 0);
            Assert.Equal(expectedLocal, summary.AccountLocalStartedAt);
            Assert.Equal(expectedLocal, detail.AccountLocalStartedAt);
            Assert.Equal(expectedLocal, Assert.Single(await archive.GetWorkoutHistoryAsync(userId, 10, 0))
                .AccountLocalStartedAt);
        }
        finally
        {
            await SyntheticTenantSession.SetAsync(database, userId);
            await database.WorkoutSessionLogs.Where(row => row.UserId == userId).ExecuteDeleteAsync();
            await database.WorkoutAccountPlanningStates.Where(row => row.UserId == userId).ExecuteDeleteAsync();
            await database.Users.Where(row => row.Id == userId).ExecuteDeleteAsync();
        }
    }
}
