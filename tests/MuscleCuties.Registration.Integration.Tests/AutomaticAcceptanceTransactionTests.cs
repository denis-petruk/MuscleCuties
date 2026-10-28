using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;
using Npgsql;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class AutomaticAcceptanceTransactionTests
{
    private const string ConnectionVariable = "MC_REGISTRATION_WRITE_TEST_POSTGRES";
    private const string DatabasePrefix = "mc_test_";
    private const int ProfileVersion = 1;
    private const int OneDay = 1;
    private const int StrengthMinutes = 60;
    private const decimal WalkingMinutes = 30m;
    private const int WorkoutDays = 2;
    private const int CycleDays = 28;
    private const int SyntheticInjurySiteFlag = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Xunit.Fact]
    public async Task AutomaticAcceptanceUsesNextScheduledBoundaryAndRollsBackWithCaller()
    {
        var options = Options();
        await using var db = new PostgresDbContext(options);
        await using var tx = await db.Database.BeginTransactionAsync();
        var userId = Guid.NewGuid();
        var priorPlanId = Guid.NewGuid();
        var newPlanId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var start = today.AddDays(OneDay);
        var walking = (int)WorkoutActivityType.Walking;
        var strength = (int)WorkoutActivityType.GeneralStrength;
        await SyntheticTenantSession.SetAsync(db, userId);
        db.Users.Add(new UserRecord { Id = userId, Email = $"auto-accept-{userId:N}@example.invalid",
            Username = $"aa_{userId.ToString("N")[..10]}", CredentialType = "Password",
            PasswordHash = "synthetic-hash", CreatedAt = now, UpdatedAt = now, IsOnboardingComplete = true });
        db.UserProfiles.Add(new UserProfileRecord { Id = Guid.NewGuid(), UserId = userId, Name = "Synthetic",
            Height = 170m, Weight = 64m, Goal = "BuildMuscle", WeightGoalPace = "Maintain",
            TrainingExperienceLevel = "Beginner", WorkoutDaysPerWeek = WorkoutDays, CycleLength = CycleDays,
            EquipmentLevel = "FullGym", ConcurrencyVersion = ProfileVersion, UpdatedAt = now });
        db.WorkoutAccountPlanningStates.Add(new WorkoutAccountPlanningStateRecord
            { Id = Guid.NewGuid(), UserId = userId, TimeZoneId = "UTC", PromotionPending = true });
        db.WorkoutPlans.Add(new WorkoutPlanRecord { Id = priorPlanId, UserId = userId, Name = "Synthetic previous",
            IsActive = true, CyclePhaseTarget = "Follicular", CreatedAt = now });
        db.WorkoutDays.Add(new WorkoutDayRecord { Id = Guid.NewGuid(), UserId = userId, WorkoutPlanId = priorPlanId,
            Date = start, DayOfWeek = (int)start.DayOfWeek, WorkoutType = "Cardio", Name = "Synthetic walking",
            ActivityTypeId = walking });
        await db.SaveChangesAsync();
        var draft = new WorkoutPreferencesDto([strength, walking], [(int)today.DayOfWeek, (int)start.DayOfWeek],
            new Dictionary<int, int> { [strength] = StrengthMinutes }, "FullGym");
        var request = new StrengthPlanPreviewRequestV020(WorkoutActivityDto.GeneralStrength, today, null)
            { DraftPreferences = draft };
        var input = new { Request = request, ProfileVersion, BaselineActivePlanId = priorPlanId,
            BaselineRevisionId = (Guid?)null, AutomaticOrigin = true, AutomaticStartDate = start };
        var schedule = new MixedActivityScheduleV020(
            [new MixedActivityScheduleSessionV020(walking, start.DayOfWeek, WalkingMinutes, false)],
            [new MixedActivityScheduleDecisionV020(walking, "Scheduled", null, null, null, 1, [])], []);
        var proposalSnapshot = new { PolicyId = StrengthPlanPreviewRule.PolicyId,
            PolicyVersion = StrengthPlanPreviewRule.Version, RulesetVersion = StrengthPlanPreviewRule.RulesetVersion,
            Status = "Feasible", Sessions = Array.Empty<StrengthPlanPreviewSessionV020>(), MixedActivitySchedule = schedule,
            SourceHistoryFingerprint = PlanningInputFingerprint.Hash(Array.Empty<object>()),
            SourceInjuryFingerprint = PlanningInputFingerprint.Injuries(Array.Empty<WorkoutInjuryLogRecord>()),
            SourceCatalogFingerprint = await PlanningCatalogFingerprint.LoadAsync(db, userId, CancellationToken.None),
            SourceTimeZoneVersion = 0, SourceExperienceVersion = 0, SourceAnswerVersion = 0 };
        db.WorkoutPlanProposalsV02.Add(new WorkoutPlanProposalV02Record { Id = proposalId, UserId = userId,
            PlanId = newPlanId, ExpectedRevisionId = Guid.Empty, RulesetVersion = StrengthPlanPreviewRule.RulesetVersion,
            TrainingPolicyVersion = StrengthPlanPreviewRule.Version, Status = "Proposed", CreatedAtUtc = now,
            InputSnapshotJson = JsonSerializer.Serialize(input, JsonOptions),
            ProposalJson = JsonSerializer.Serialize(proposalSnapshot, JsonOptions) });
        await db.SaveChangesAsync();
        var store = new PostgresAcceptStrengthPlanProposalStoreV020(db);
        var state = await db.WorkoutAccountPlanningStates.SingleAsync(row => row.UserId == userId);
        state.SafetyRegenerationPending = true;
        var episode = new WorkoutInjuryEpisodeRecord
        {
            Id = Guid.NewGuid(), UserId = userId, InjuryLogId = Guid.NewGuid(),
            SiteFlag = SyntheticInjurySiteFlag, StartedOn = today
        };
        db.WorkoutInjuryEpisodes.Add(episode);
        await db.SaveChangesAsync();
        var blocked = await store.AcceptInCurrentTransactionAsync(userId, new AcceptStrengthPlanProposalRequestV020(proposalId),
            PostgresAcceptStrengthPlanProposalStoreV020.AcceptanceOrigin.Automatic, CancellationToken.None);
        Xunit.Assert.Equal("Stale", blocked.Status);
        Xunit.Assert.True(await db.WorkoutPlans.Where(row => row.Id == priorPlanId).Select(row => row.IsActive).SingleAsync());
        Xunit.Assert.False(await db.WorkoutPlans.AnyAsync(row => row.Id == newPlanId));
        db.WorkoutInjuryEpisodes.Remove(episode);
        state.SafetyRegenerationPending = false;
        await db.SaveChangesAsync();
        var result = await store.AcceptInCurrentTransactionAsync(userId, new AcceptStrengthPlanProposalRequestV020(proposalId),
            PostgresAcceptStrengthPlanProposalStoreV020.AcceptanceOrigin.Automatic, CancellationToken.None);
        Xunit.Assert.Equal("Accepted", result.Status);
        Xunit.Assert.False(await db.WorkoutPlans.Where(row => row.Id == priorPlanId).Select(row => row.IsActive).SingleAsync());
        Xunit.Assert.True(await db.WorkoutPlans.Where(row => row.Id == newPlanId).Select(row => row.IsActive).SingleAsync());
        Xunit.Assert.Equal(start, await db.WorkoutDays.Where(row => row.WorkoutPlanId == newPlanId)
            .MinAsync(row => row.Date));
        // The reconciler, not the acceptance store, consumes this flag after acceptance.
        Xunit.Assert.True(await db.WorkoutAccountPlanningStates.Where(row => row.UserId == userId)
            .Select(row => row.PromotionPending).SingleAsync());
        await tx.RollbackAsync();
        db.ChangeTracker.Clear();
        Xunit.Assert.False(await db.WorkoutPlans.AnyAsync(row => row.UserId == userId));
        Xunit.Assert.False(await db.WorkoutPlanRevisionsV02.AnyAsync(row => row.UserId == userId));
    }

    private static DbContextOptions<PostgresDbContext> Options()
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionVariable);
        Xunit.Assert.False(string.IsNullOrWhiteSpace(connection), $"Set {ConnectionVariable} to a disposable local PostgreSQL test database.");
        var builder = new NpgsqlConnectionStringBuilder(connection);
        Xunit.Assert.Equal("127.0.0.1", builder.Host);
        Xunit.Assert.StartsWith(DatabasePrefix, builder.Database, StringComparison.Ordinal);
        return new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connection).Options;
    }
}
