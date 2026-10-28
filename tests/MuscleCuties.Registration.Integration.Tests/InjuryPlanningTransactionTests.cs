using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.CheckInAndInjury;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.CheckInAndInjury;
using Npgsql;
using System.Text.Json;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class InjuryPlanningTransactionTests
{
    private const string ConnectionVariable = "MC_REGISTRATION_WRITE_TEST_POSTGRES";
    private const int SyntheticSets = 3;
    private const int SyntheticOptionalSets = 1;
    private const int SyntheticRepetitions = 10;

    // This exercises transactional provenance, not clinical eligibility or review metadata.
    [Xunit.Fact]
    public async Task OverlappingInjuriesFencePreviewPreservePrescriptionAndAnchorClearance()
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionVariable);
        Xunit.Assert.False(string.IsNullOrWhiteSpace(connection));
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        Xunit.Assert.Equal("127.0.0.1", parsed.Host);
        Xunit.Assert.StartsWith("mc_test_", parsed.Database, StringComparison.Ordinal);
        var options = new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connection).Options;
        await using var db = new PostgresDbContext(options);
        var user = Guid.NewGuid();
        var plan = Guid.NewGuid();
        var day = Guid.NewGuid();
        var exercise = Guid.NewGuid();
        var proposal = Guid.NewGuid();
        var acceptedProposal = Guid.NewGuid();
        var firstInjury = Guid.NewGuid();
        var secondInjury = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var clock = new MutableTimeProvider(now);
        await SyntheticTenantSession.SetAsync(db, user);
        try
        {
            var exerciseId = await db.Exercises.AsNoTracking().OrderBy(row => row.Id).Select(row => row.Id).FirstAsync();
            db.Users.Add(new UserRecord { Id = user, Email = $"injury-{user:N}@example.invalid",
                Username = $"inj_{user.ToString("N")[..10]}", CredentialType = "Password", PasswordHash = "synthetic-hash",
                CreatedAt = now, UpdatedAt = now, IsOnboardingComplete = true });
            db.WorkoutAccountPlanningStates.Add(new WorkoutAccountPlanningStateRecord { Id = Guid.NewGuid(), UserId = user,
                TimeZoneId = TimeZoneInfo.Utc.Id, BaselineExperience = TrainingExperienceLevel.Beginner,
                EffectiveExperience = TrainingExperienceLevel.Beginner });
            db.WorkoutPlans.Add(new WorkoutPlanRecord { Id = plan, UserId = user, Name = "Synthetic plan",
                IsActive = true, CyclePhaseTarget = "Follicular", CreatedAt = now });
            db.WorkoutDays.Add(new WorkoutDayRecord { Id = day, UserId = user, WorkoutPlanId = plan,
                Date = today.AddDays(1), DayOfWeek = (int)today.AddDays(1).DayOfWeek,
                WorkoutType = nameof(WorkoutType.Strength), Name = "Synthetic strength" });
            db.WorkoutDayExercises.Add(new WorkoutDayExerciseRecord { Id = exercise, UserId = user, WorkoutDayId = day,
                ExerciseId = exerciseId, PlanningSlotId = "synthetic-slot", Sets = SyntheticSets,
                OptionalSets = SyntheticOptionalSets, Reps = SyntheticRepetitions });
            db.WorkoutPlanProposalsV02.Add(new WorkoutPlanProposalV02Record { Id = proposal, UserId = user,
                PlanId = Guid.NewGuid(), Status = "Proposed", RulesetVersion = "synthetic", TrainingPolicyVersion = "synthetic",
                CreatedAtUtc = now, LeaseId = Guid.NewGuid(), LeaseFence = 1, LeaseExpiresAtUtc = now.AddMinutes(5) });
            db.WorkoutPlanProposalsV02.Add(new WorkoutPlanProposalV02Record { Id = acceptedProposal, UserId = user,
                PlanId = plan, Status = "Accepted", RulesetVersion = "synthetic", TrainingPolicyVersion = "synthetic", CreatedAtUtc = now });
            db.WorkoutPlanRevisionsV02.Add(new WorkoutPlanRevisionV02Record { Id = Guid.NewGuid(), UserId = user,
                PlanId = plan, SourceProposalId = acceptedProposal, RevisionNumber = 1, RulesetVersion = "synthetic",
                TrainingPolicyVersion = "synthetic", AcceptedAtUtc = now,
                PlanJson = JsonSerializer.Serialize(new { proposal = new { sessions = new[] { new { sessionIndex = 0,
                    slots = new[] { new { exerciseId } } } } }, weekdayAssignments = new[] { new { sessionIndex = 0,
                    weekday = (int)today.AddDays(1).DayOfWeek } } }) });
            await db.SaveChangesAsync();
            var store = new PostgresCheckInAndInjuryStore(db, clock);
            var first = new SaveInjuryObservationRequest(user, firstInjury, (int)InjuryFlag.Knee, "Acute", today, 3, today);
            await store.SaveInjuryAsync(first, CancellationToken.None);
            Xunit.Assert.True((await db.WorkoutDays.AsNoTracking().SingleAsync(row => row.Id == day)).IsRemovedBySafety);
            Xunit.Assert.True((await db.WorkoutAccountPlanningStates.AsNoTracking().SingleAsync(row => row.UserId == user)).SafetyRegenerationPending);
            Xunit.Assert.Equal("Expired", (await db.WorkoutPlanProposalsV02.AsNoTracking().SingleAsync(row => row.Id == proposal)).Status);
            await store.SaveInjuryAsync(new SaveInjuryObservationRequest(user, secondInjury, (int)InjuryFlag.Shoulder,
                "Acute", today, 2, today), CancellationToken.None);
            var associations = await db.WorkoutInjuryPrescriptions.AsNoTracking().Where(row => row.UserId == user).ToListAsync();
            // An unchanged original can be captured for each episode without asserting
            // that either injury affects it; associations are established separately.
            Xunit.Assert.Equal(2, associations.Count);
            Xunit.Assert.Equal(2, associations.Select(row => row.EpisodeId).Distinct().Count());
            Xunit.Assert.All(associations, row => { Xunit.Assert.Equal(exercise, row.OriginalWorkoutDayExerciseId);
                Xunit.Assert.Equal(SyntheticOptionalSets, row.OriginalOptionalSets); Xunit.Assert.Equal("synthetic-slot", row.OriginalPlanningSlotId); });
            await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveInjuryAsync(
                first with { SiteFlag = (int)InjuryFlag.Shoulder }, CancellationToken.None));
            Xunit.Assert.Equal(2, await db.WorkoutInjuryEpisodes.AsNoTracking().CountAsync(row => row.UserId == user));
            Xunit.Assert.Equal(2, await db.WorkoutInjuryPrescriptions.AsNoTracking().CountAsync(row => row.UserId == user));
            Xunit.Assert.Equal((int)InjuryFlag.Knee, (await db.WorkoutInjuryLogs.AsNoTracking()
                .SingleAsync(row => row.Id == firstInjury)).SiteFlag);
            Xunit.Assert.True((await db.WorkoutDays.AsNoTracking().SingleAsync(row => row.Id == day)).IsRemovedBySafety);
            clock.UtcNow = now.AddDays(2);
            await store.SaveInjuryAsync(first with { Status = "Cleared", Date = today }, CancellationToken.None);
            var cleared = await db.WorkoutInjuryEpisodes.AsNoTracking().SingleAsync(row => row.InjuryLogId == firstInjury);
            Xunit.Assert.Equal(today.AddDays(2), cleared.ClearedOn);
            Xunit.Assert.Equal(TimeZoneInfo.Utc.Id, cleared.ClearanceTimeZoneId);
            clock.UtcNow = now.AddDays(5);
            await store.SaveInjuryAsync(first with { Status = "Cleared", Date = today }, CancellationToken.None);
            Xunit.Assert.Equal(today.AddDays(2), (await db.WorkoutInjuryEpisodes.AsNoTracking()
                .SingleAsync(row => row.InjuryLogId == firstInjury)).ClearedOn);
            Xunit.Assert.Null((await db.WorkoutInjuryEpisodes.AsNoTracking().SingleAsync(row => row.InjuryLogId == secondInjury)).ClearedOn);
            Xunit.Assert.True(await db.WorkoutDayExercises.AsNoTracking().AnyAsync(row => row.Id == exercise));
            await store.SaveInjuryAsync(first with { Status = "Acute", Date = DateOnly.FromDateTime(clock.UtcNow) }, CancellationToken.None);
            var recurringEpisodes = await db.WorkoutInjuryEpisodes.AsNoTracking()
                .Where(row => row.InjuryLogId == firstInjury).ToListAsync();
            Xunit.Assert.Equal(2, recurringEpisodes.Count);
            Xunit.Assert.Single(recurringEpisodes, episode => episode.ClearedOn == today.AddDays(2));
            Xunit.Assert.Single(recurringEpisodes, episode => episode.ClearedOn is null);
            Xunit.Assert.Equal(2, await db.WorkoutInjuryPrescriptions.AsNoTracking().CountAsync(row => row.UserId == user));
        }
        finally { await db.Users.Where(row => row.Id == user).ExecuteDeleteAsync(); }
    }

    private sealed class MutableTimeProvider(DateTime utc) : TimeProvider
    {
        public DateTime UtcNow { get; set; } = utc;
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(UtcNow, DateTimeKind.Utc));
    }
}
