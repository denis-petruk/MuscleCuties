using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Planning;
using Npgsql;
using NpgsqlTypes;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class AutomaticPlanningPersistenceTests
{
    private const string ConnectionVariable = "MC_REGISTRATION_WRITE_TEST_POSTGRES";
    private const string LocalHost = "127.0.0.1";
    private const string SyntheticDatabasePrefix = "mc_test_";
    private const string SyntheticRuleset = "synthetic-ruleset";
    private const string SyntheticPolicy = "synthetic-policy";
    private const string ProposedStatus = "Proposed";
    private const string InvalidProposalStatus = "InvalidProposal";
    private const string OriginMismatchCode = "AutomaticProposalNotUserAcceptable";
    private const string UtcTimeZone = "UTC";
    private const int InitialVersion = 1;
    private const long InitialFence = 1;
    private const int SingleOwnerPage = 1;
    private const int NextDayOffset = 1;
    private const int FollowingDayOffset = 2;
    private const string SyntheticCardio = "Cardio";

    [Xunit.Fact]
    public async Task DiscoveryRolePagesOnlyPendingOwnersWithoutDirectTableAccess()
    {
        await using var database = CreateContext();
        await using var transaction = await database.Database.BeginTransactionAsync();
        var prefix = Guid.NewGuid().ToString("N")[..24];
        var before = Guid.ParseExact(prefix + "00000000", "N");
        var first = Guid.ParseExact(prefix + "00000001", "N");
        var second = Guid.ParseExact(prefix + "00000002", "N");
        var clean = Guid.ParseExact(prefix + "00000003", "N");
        foreach (var owner in new[] { first, second, clean })
        {
            await SyntheticTenantSession.SetAsync(database, owner);
            AddSyntheticUser(database, owner);
            database.WorkoutAccountPlanningStates.Add(new WorkoutAccountPlanningStateRecord
            {
                Id = Guid.NewGuid(), UserId = owner,
                PromotionPending = owner == first, SafetyRegenerationPending = owner == second
            });
            await database.SaveChangesAsync();
            database.ChangeTracker.Clear();
        }

        await database.Database.ExecuteSqlRawAsync("SET LOCAL ROLE musclecuties_workout_dispatch");
        var connection = (NpgsqlConnection)database.Database.GetDbConnection();
        await using var permission = new NpgsqlCommand(
            "SELECT has_column_privilege(current_user, 'app.workout_account_planning_states', 'user_id', 'SELECT')", connection);
        Xunit.Assert.False((bool)(await permission.ExecuteScalarAsync())!);

        var pageOne = await DiscoverAsync(connection, before, SingleOwnerPage);
        var pageTwo = await DiscoverAsync(connection, first, SingleOwnerPage);
        Xunit.Assert.Equal(new[] { first }, pageOne);
        Xunit.Assert.Equal(new[] { second }, pageTwo);
        var allAfterSecond = await DiscoverAsync(connection, second, SingleOwnerPage);
        Xunit.Assert.DoesNotContain(clean, allAfterSecond);
        await transaction.RollbackAsync();
    }

    private static async Task<List<Guid>> DiscoverAsync(NpgsqlConnection connection, Guid after, int limit)
    {
        await using var command = new NpgsqlCommand(
            "SELECT owner_id FROM app.discover_pending_workout_owners(@after, @limit)", connection);
        command.Parameters.AddWithValue("after", NpgsqlDbType.Uuid, after);
        command.Parameters.AddWithValue("limit", limit);
        var owners = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) owners.Add(reader.GetGuid(0));
        return owners;
    }

    [Xunit.Fact]
    public async Task AcceptanceRejectsProposalFromOtherOriginBeforeAnyPlanMutation()
    {
        await using var database = CreateContext();
        await using var transaction = await database.Database.BeginTransactionAsync();
        var userId = Guid.NewGuid();
        await SyntheticTenantSession.SetAsync(database, userId);
        AddSyntheticUser(database, userId);
        var automatic = AddProposal(database, userId, automaticOrigin: true);
        var manual = AddProposal(database, userId, automaticOrigin: false);
        await database.SaveChangesAsync();

        var store = new PostgresAcceptStrengthPlanProposalStoreV020(database);
        var userResult = await store.AcceptInCurrentTransactionAsync(userId,
            new AcceptStrengthPlanProposalRequestV020(automatic.Id),
            PostgresAcceptStrengthPlanProposalStoreV020.AcceptanceOrigin.User, CancellationToken.None);
        var automaticResult = await store.AcceptInCurrentTransactionAsync(userId,
            new AcceptStrengthPlanProposalRequestV020(manual.Id),
            PostgresAcceptStrengthPlanProposalStoreV020.AcceptanceOrigin.Automatic, CancellationToken.None);

        Xunit.Assert.Equal(InvalidProposalStatus, userResult.Status);
        Xunit.Assert.Equal(OriginMismatchCode, userResult.LimitationCode);
        Xunit.Assert.Equal(InvalidProposalStatus, automaticResult.Status);
        Xunit.Assert.Equal(OriginMismatchCode, automaticResult.LimitationCode);
        Xunit.Assert.False(await database.WorkoutPlans.AnyAsync(row => row.UserId == userId));
        Xunit.Assert.False(await database.WorkoutPlanRevisionsV02.AnyAsync(row => row.UserId == userId));
        Xunit.Assert.All(await database.WorkoutPlanProposalsV02.Where(row => row.UserId == userId).ToListAsync(),
            proposal => Xunit.Assert.Equal(ProposedStatus, proposal.Status));
        await transaction.RollbackAsync();
    }

    [Xunit.Fact]
    public async Task ActivePreviewDefersPendingUpdatesWithoutClearingEitherFlag()
    {
        await using var database = CreateContext();
        await using var transaction = await database.Database.BeginTransactionAsync();
        var userId = Guid.NewGuid();
        await SyntheticTenantSession.SetAsync(database, userId);
        AddSyntheticUser(database, userId);
        database.WorkoutAccountPlanningStates.Add(new WorkoutAccountPlanningStateRecord
        {
            Id = Guid.NewGuid(), UserId = userId, TimeZoneId = UtcTimeZone,
            PromotionPending = true, SafetyRegenerationPending = true
        });
        var proposal = AddProposal(database, userId, automaticOrigin: false);
        proposal.LeaseId = Guid.NewGuid();
        proposal.LeaseFence = InitialFence;
        proposal.LeaseExpiresAtUtc = DateTime.UtcNow.Add(PlanningLifecycleRule.FiveMinuteLeaseDuration);
        await database.SaveChangesAsync();

        await new PostgresWorkoutPlanReconciler(database).ReconcileAsync(userId);
        database.ChangeTracker.Clear();
        var persisted = await database.WorkoutAccountPlanningStates.AsNoTracking().SingleAsync(row => row.UserId == userId);
        Xunit.Assert.True(persisted.PromotionPending);
        Xunit.Assert.True(persisted.SafetyRegenerationPending);
        Xunit.Assert.Equal(ProposedStatus, (await database.WorkoutPlanProposalsV02.AsNoTracking()
            .SingleAsync(row => row.Id == proposal.Id)).Status);
        Xunit.Assert.False(await database.WorkoutPlans.AnyAsync(row => row.UserId == userId));
        await transaction.RollbackAsync();
    }

    [Xunit.Fact]
    public async Task ActiveSessionKeepsBothAutomaticUpdatesPending()
    {
        await using var database = CreateContext();
        await using var transaction = await database.Database.BeginTransactionAsync();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SyntheticTenantSession.SetAsync(database, userId);
        AddSyntheticUser(database, userId);
        database.WorkoutAccountPlanningStates.Add(new WorkoutAccountPlanningStateRecord
        {
            Id = Guid.NewGuid(), UserId = userId, TimeZoneId = UtcTimeZone,
            PromotionPending = true, SafetyRegenerationPending = true
        });
        database.WorkoutPlans.Add(new WorkoutPlanRecord
        {
            Id = Guid.NewGuid(), UserId = userId, Name = "Synthetic plan", IsActive = true,
            CyclePhaseTarget = "Follicular", CreatedAt = now
        });
        database.WorkoutSessionLogs.Add(new WorkoutSessionLogRecord
        {
            Id = Guid.NewGuid(), UserId = userId, WorkoutName = "Synthetic active session",
            StartedAt = now, CreatedAt = now, UpdatedAt = now
        });
        await database.SaveChangesAsync();

        await new PostgresWorkoutPlanReconciler(database).ReconcileAsync(userId);
        database.ChangeTracker.Clear();
        var state = await database.WorkoutAccountPlanningStates.AsNoTracking().SingleAsync(row => row.UserId == userId);
        Xunit.Assert.True(state.PromotionPending);
        Xunit.Assert.True(state.SafetyRegenerationPending);
        Xunit.Assert.False(await database.WorkoutPlanProposalsV02.AnyAsync(row => row.UserId == userId));
        Xunit.Assert.False(await database.WorkoutPlanRevisionsV02.AnyAsync(row => row.UserId == userId));
        await transaction.RollbackAsync();
    }

    [Xunit.Fact]
    public async Task NextScheduledDateUsesTodayOnlyWhenNoActivityCompletedAndSkipsSuppressedDays()
    {
        await using var database = CreateContext();
        await using var transaction = await database.Database.BeginTransactionAsync();
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var today = new DateOnly(2026, 12, 31);
        var tomorrow = today.AddDays(NextDayOffset);
        var following = today.AddDays(FollowingDayOffset);
        await SyntheticTenantSession.SetAsync(database, userId);
        AddSyntheticUser(database, userId);
        database.WorkoutPlans.Add(new WorkoutPlanRecord
        {
            Id = planId, UserId = userId, Name = "Synthetic plan", IsActive = true,
            CyclePhaseTarget = "Follicular", CreatedAt = now
        });
        foreach (var date in new[] { today, tomorrow, following })
            database.WorkoutDays.Add(new WorkoutDayRecord
            {
                Id = Guid.NewGuid(), UserId = userId, WorkoutPlanId = planId,
                Date = date, DayOfWeek = (int)date.DayOfWeek, WorkoutType = SyntheticCardio,
                Name = "Synthetic activity", IsRemovedBySafety = date == tomorrow
            });
        await database.SaveChangesAsync();
        var store = new PostgresAcceptStrengthPlanProposalStoreV020(database);
        Xunit.Assert.Equal(today, await store.FindNextScheduledDateAsync(userId, planId, today, false, CancellationToken.None));
        Xunit.Assert.Equal(following, await store.FindNextScheduledDateAsync(userId, planId, today, true, CancellationToken.None));
        await transaction.RollbackAsync();
    }

    private static PostgresDbContext CreateContext()
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionVariable);
        Xunit.Assert.False(string.IsNullOrWhiteSpace(connection),
            $"Set {ConnectionVariable} to a disposable local PostgreSQL test database.");
        var builder = new NpgsqlConnectionStringBuilder(connection);
        Xunit.Assert.Equal(LocalHost, builder.Host);
        Xunit.Assert.StartsWith(SyntheticDatabasePrefix, builder.Database, StringComparison.Ordinal);
        return new PostgresDbContext(new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connection).Options);
    }

    private static void AddSyntheticUser(PostgresDbContext database, Guid userId)
    {
        var now = DateTime.UtcNow;
        database.Users.Add(new UserRecord
        {
            Id = userId, Email = $"automatic-{userId:N}@example.invalid",
            Username = $"auto_{userId.ToString("N")[^10..]}", CredentialType = "Password",
            PasswordHash = "synthetic-hash", CreatedAt = now, UpdatedAt = now, IsOnboardingComplete = true
        });
    }

    private static WorkoutPlanProposalV02Record AddProposal(PostgresDbContext database, Guid userId, bool automaticOrigin)
    {
        var proposal = new WorkoutPlanProposalV02Record
        {
            Id = Guid.NewGuid(), UserId = userId, PlanId = Guid.NewGuid(), ExpectedRevisionId = Guid.NewGuid(),
            RulesetVersion = SyntheticRuleset, TrainingPolicyVersion = SyntheticPolicy, Status = ProposedStatus,
            InputSnapshotJson = automaticOrigin ? "{\"automaticOrigin\":true}" : "{\"automaticOrigin\":false}",
            CreatedAtUtc = DateTime.UtcNow, ConcurrencyVersion = InitialVersion
        };
        database.WorkoutPlanProposalsV02.Add(proposal);
        return proposal;
    }
}
