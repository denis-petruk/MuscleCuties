using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Profile.ServerProfile;
using MuscleCuties.Infrastructure.PostgreSql.Workout.ActiveSession;
using MuscleCuties.UseCases.Features.Profile.ServerProfile;
using Npgsql;
using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class PopulationSafetyPersistenceTests
{
    private const string ConnectionVariable = "MC_REGISTRATION_WRITE_TEST_POSTGRES";
    private const int InitialProfileVersion = 1;
    private const int IdentifierLength = 10;
    private static readonly DateOnly SyntheticBirthDate = new(1990, 1, 1);
    private static readonly TimeSpan LockObservationDelay = TimeSpan.FromMilliseconds(200);

    [Fact]
    public async Task Pregnancy_update_waits_for_planning_lock_and_suppresses_active_and_future_work()
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(connection));
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        Assert.Equal("127.0.0.1", parsed.Host);
        Assert.StartsWith("mc_test_", parsed.Database, StringComparison.Ordinal);
        var options = new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connection).Options;
        await using var setup = new PostgresDbContext(options);
        var user = Guid.NewGuid();
        var plan = Guid.NewGuid();
        var day = Guid.NewGuid();
        var session = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SyntheticTenantSession.SetAsync(setup, user);
        setup.Users.Add(new UserRecord { Id = user, Email = $"population-{user:N}@example.invalid",
            Username = $"pop_{user.ToString("N")[..IdentifierLength]}", CredentialType = "Password", PasswordHash = "synthetic-hash", CreatedAt = now, UpdatedAt = now });
        setup.UserProfiles.Add(new UserProfileRecord { Id = Guid.NewGuid(), UserId = user, Name = "Synthetic",
            DateOfBirth = SyntheticBirthDate, ConcurrencyVersion = InitialProfileVersion, UpdatedAt = now });
        setup.WorkoutAccountPlanningStates.Add(new WorkoutAccountPlanningStateRecord { Id = Guid.NewGuid(), UserId = user, TimeZoneId = "UTC" });
        setup.WorkoutPlans.Add(new WorkoutPlanRecord { Id = plan, UserId = user, Name = "Synthetic", IsActive = true,
            CyclePhaseTarget = "Unknown", CreatedAt = now });
        setup.WorkoutDays.Add(new WorkoutDayRecord { Id = day, UserId = user, WorkoutPlanId = plan,
            Date = DateOnly.FromDateTime(now), DayOfWeek = (int)now.DayOfWeek, WorkoutType = "Cardio", Name = "Synthetic" });
        setup.WorkoutSessionLogs.Add(new WorkoutSessionLogRecord { Id = session, UserId = user, PlannedWorkoutDayId = day,
            WorkoutName = "Synthetic", StartedAt = now, CreatedAt = now, UpdatedAt = now });
        await setup.SaveChangesAsync();
        try
        {
            await using var locker = new PostgresDbContext(options);
            await using var transaction = await locker.Database.BeginTransactionAsync();
            await SyntheticTenantSession.SetAsync(locker, user);
            await WorkoutSessionAccountLock.AcquireAsync(locker, user, CancellationToken.None);
            await using var writer = new PostgresDbContext(options);
            var request = new UpdateServerProfileRequest(user, InitialProfileVersion, null, null, false, null, null,
                null, null, null, null, null, null, "{\"Pregnancy\":2,\"Contraception\":0}");
            var pending = new PostgresProfileDataStore(writer, TimeProvider.System).UpdateProfileAsync(request, CancellationToken.None);
            await Task.Delay(LockObservationDelay);
            Assert.False(pending.IsCompleted);
            await transaction.CommitAsync();
            var saved = await pending;
            Assert.Equal(ServerProfileWriteStatus.Saved, saved.Status);
            Assert.True(await setup.WorkoutDays.AsNoTracking().Where(x => x.UserId == user && x.Id == day).Select(x => x.IsRemovedBySafety).SingleAsync());
            Assert.True(await setup.WorkoutAccountPlanningStates.AsNoTracking().Where(x => x.UserId == user).Select(x => x.SafetyRegenerationPending).SingleAsync());
            Assert.False(await setup.WorkoutSessionLogs.AsNoTracking().Where(x => x.UserId == user && x.Id == session).Select(x => x.IsDeleted).SingleAsync());
        }
        finally { await setup.Users.Where(row => row.Id == user).ExecuteDeleteAsync(); }
    }
}
