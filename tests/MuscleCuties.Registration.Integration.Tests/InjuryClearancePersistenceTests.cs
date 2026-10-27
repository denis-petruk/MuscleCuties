using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.CheckInAndInjury;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.CheckInAndInjury;
using Npgsql;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class InjuryClearancePersistenceTests
{
    private const string ConnectionVariable = "MC_REGISTRATION_WRITE_TEST_POSTGRES";
    private const string LocalHost = "127.0.0.1";
    private const string SyntheticDatabasePrefix = "mc_test_";
    private const string AccountTimeZone = "America/New_York";
    private const int SyntheticInjurySite = 1;
    private const int SyntheticPain = 1;
    private static readonly DateOnly LoggedDate = new(2026, 1, 1);
    private static readonly DateOnly FirstClearanceDate = new(2026, 2, 1);
    private static readonly DateOnly SecondClearanceDate = new(2026, 2, 6);
    private static readonly DateTimeOffset FirstClearanceInstant = new(2026, 2, 2, 2, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LaterInstant = new(2026, 2, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SecondClearanceInstant = new(2026, 2, 7, 2, 0, 0, TimeSpan.Zero);

    [Xunit.Fact]
    public async Task ClearanceUsesAccountDateAndPreservesItUntilReinjury()
    {
        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(RequireLocalTestConnectionString()).Options;
        await using var database = new PostgresDbContext(options);
        var userId = Guid.NewGuid();
        var injuryId = Guid.NewGuid();
        var clock = new MutableTimeProvider(FirstClearanceInstant);
        await SyntheticTenantSession.SetAsync(database, userId);
        database.Users.Add(new UserRecord
        {
            Id = userId, Email = $"injury-{userId:N}@example.invalid",
            Username = $"inj_{userId.ToString("N")[..10]}", CredentialType = "Password",
            PasswordHash = "synthetic-hash", CreatedAt = FirstClearanceInstant.UtcDateTime,
            UpdatedAt = FirstClearanceInstant.UtcDateTime, IsOnboardingComplete = true
        });
        database.WorkoutAccountPlanningStates.Add(new WorkoutAccountPlanningStateRecord
        {
            Id = Guid.NewGuid(), UserId = userId, TimeZoneId = AccountTimeZone
        });
        await database.SaveChangesAsync();
        try
        {
            var store = new PostgresCheckInAndInjuryStore(database, clock);
            SaveInjuryObservationRequest Request(string status) => new(userId, injuryId, SyntheticInjurySite,
                status, LoggedDate, SyntheticPain, LoggedDate);
            async Task<DateOnly?> PersistedClearanceAsync() => await database.WorkoutInjuryLogs.AsNoTracking()
                .Where(row => row.UserId == userId && row.Id == injuryId)
                .Select(row => row.ClearedDate).SingleAsync();

            await store.SaveInjuryAsync(Request("Acute"), CancellationToken.None);
            await store.SaveInjuryAsync(Request("Cleared"), CancellationToken.None);
            Xunit.Assert.Equal(FirstClearanceDate, await PersistedClearanceAsync());

            clock.UtcNow = LaterInstant;
            await store.SaveInjuryAsync(Request("Cleared"), CancellationToken.None);
            Xunit.Assert.Equal(FirstClearanceDate, await PersistedClearanceAsync());

            await store.SaveInjuryAsync(Request("Acute"), CancellationToken.None);
            Xunit.Assert.Null(await PersistedClearanceAsync());
            clock.UtcNow = SecondClearanceInstant;
            await store.SaveInjuryAsync(Request("Cleared"), CancellationToken.None);
            Xunit.Assert.Equal(SecondClearanceDate, await PersistedClearanceAsync());
            var episodes = await database.WorkoutInjuryEpisodes.AsNoTracking()
                .Where(row => row.UserId == userId && row.SourceInjuryId == injuryId)
                .OrderBy(row => row.RecordedAtUtc).ToArrayAsync();
            Xunit.Assert.Equal(2, episodes.Length);
            Xunit.Assert.NotEqual(episodes[0].Id, episodes[1].Id);
            Xunit.Assert.Equal(FirstClearanceDate, episodes[0].ClearedDate);
            Xunit.Assert.Equal(SecondClearanceDate, episodes[1].ClearedDate);

            Xunit.Assert.True(await store.DeleteInjuryAsync(userId, injuryId, CancellationToken.None));
            var preserved = await database.WorkoutInjuryEpisodes.AsNoTracking()
                .Where(row => row.UserId == userId && row.SourceInjuryId == injuryId).ToArrayAsync();
            Xunit.Assert.Equal(2, preserved.Length);
            Xunit.Assert.All(preserved, episode => Xunit.Assert.True(episode.SourceDeleted));
        }
        finally
        {
            await database.Users.Where(user => user.Id == userId).ExecuteDeleteAsync();
        }
    }

    private static string RequireLocalTestConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Xunit.Assert.False(string.IsNullOrWhiteSpace(connectionString),
            $"Set {ConnectionVariable} to a disposable local PostgreSQL test database.");
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        Xunit.Assert.Equal(LocalHost, builder.Host);
        Xunit.Assert.StartsWith(SyntheticDatabasePrefix, builder.Database, StringComparison.Ordinal);
        return connectionString!;
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
