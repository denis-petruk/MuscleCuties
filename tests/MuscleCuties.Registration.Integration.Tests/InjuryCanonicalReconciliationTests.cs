using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.CheckInAndInjury;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using MuscleCuties.UseCases.Features.CheckInAndInjury;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;
using Npgsql;
using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

[Collection(SharedCatalogMutationCollection.Name)]
public sealed class InjuryCanonicalReconciliationTests
{
    private const string ConnectionVariable = "MC_REGISTRATION_WRITE_TEST_POSTGRES";
    private const int StrengthMinutes = 60;
    private const int WarmupMinutes = 5;
    private const int CooldownMinutes = 5;
    private const int SyntheticHeight = 170;
    private const int SyntheticWeight = 64;
    private const int CycleLength = 28;
    private const int WorkoutDays = 3;
    private const int SecondWorkoutDayOffset = 2;
    private const int ThirdWorkoutDayOffset = 4;
    private const int PainLevel = 2;
    private const int CompletePercent = 100;
    private const int CompletedSets = 2;
    private const int CompletedRepetitions = 8;
    private const int UsernameSuffixLength = 10;
    private static readonly DateOnly BirthDate = new(1990, 1, 1);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcceptedConfirmationsPermitClearanceOnlyWithUnchangedCatalog(bool catalogChanged)
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(connection));
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        Assert.Equal("127.0.0.1", parsed.Host);
        Assert.StartsWith("mc_test_", parsed.Database, StringComparison.Ordinal);
        await using var db = new PostgresDbContext(new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connection).Options);
        var user = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var selectedDays = new[] { (int)today.DayOfWeek, (int)today.AddDays(SecondWorkoutDayOffset).DayOfWeek, (int)today.AddDays(ThirdWorkoutDayOffset).DayOfWeek };
        var strength = (int)WorkoutActivityType.GeneralStrength;
        var draft = new WorkoutPreferencesDto([strength], selectedDays,
            new Dictionary<int, int> { [strength] = StrengthMinutes }, "FullGym");
        int? changedExercise = null;
        string? originalEvidence = null;
        await SyntheticTenantSession.SetAsync(db, user);
        try
        {
            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
                db.Users.Add(new UserRecord { Id = user, Email = $"injury-canonical-{user:N}@example.invalid",
                    Username = $"ic_{user.ToString("N")[..UsernameSuffixLength]}", CredentialType = "Password",
                    PasswordHash = "synthetic-hash", CreatedAt = now, UpdatedAt = now, IsOnboardingComplete = true });
                db.UserProfiles.Add(new UserProfileRecord { Id = Guid.NewGuid(), UserId = user, Name = "Synthetic",
                    DateOfBirth = BirthDate, Height = SyntheticHeight, Weight = SyntheticWeight, Goal = "BuildMuscle",
                    WeightGoalPace = "Maintain", TrainingExperienceLevel = "Beginner", WorkoutDaysPerWeek = WorkoutDays,
                    CycleLength = CycleLength, EquipmentLevel = "FullGym", ConcurrencyVersion = 1, UpdatedAt = now,
                    PreferredWorkoutActivityTypes = "GeneralStrength", AvailableWorkoutWeekdaysJson = JsonSerializer.Serialize(selectedDays),
                    ActivitySessionDurationsJson = JsonSerializer.Serialize(new Dictionary<string, int> { ["GeneralStrength"] = StrengthMinutes }),
                    RequestedActivitySessionsPerWeekJson = "{}", ExcludedStrengthExerciseIdsJson = "[]" });
                db.WorkoutAccountPlanningStates.Add(new WorkoutAccountPlanningStateRecord { Id = Guid.NewGuid(), UserId = user,
                    TimeZoneId = TimeZoneInfo.Utc.Id, BaselineExperience = TrainingExperienceLevel.Beginner,
                    EffectiveExperience = TrainingExperienceLevel.Beginner });
                await db.SaveChangesAsync();
                var definitions = await db.WorkoutExerciseDefinitions.AsNoTracking().ToListAsync();
                var acknowledgedIds = definitions.Where(row =>
                    WorkoutExerciseV020CatalogMapper.Map(row).MetadataGaps.Count == 0).Select(row => row.Id).ToHashSet();
                Assert.NotEmpty(acknowledgedIds);
                var request = new StrengthPlanPreviewRequestV020(WorkoutActivityDto.GeneralStrength, today, null,
                    AcknowledgedExerciseConditionIds: acknowledgedIds,
                    WarmupMinutes: WarmupMinutes, CooldownMinutes: CooldownMinutes) { DraftPreferences = draft };
                var source = await new PostgresStrengthPlanPreviewSourceV020(db).LoadInCurrentTransactionAsync(user, CancellationToken.None);
                var preview = PreviewStrengthPlanV020UseCase.EvaluateLoadedSource(user, request, source);
                Assert.True(preview.Status == "Feasible", JsonSerializer.Serialize(preview));
                Assert.Contains(preview.Sessions.SelectMany(session => session.Slots), slot => slot.ExerciseId.HasValue);
                await transaction.CommitAsync();
                var proposal = await new PostgresStrengthPlanProposalStoreV020(db).SaveAsync(user, request, preview, CancellationToken.None);
                var lease = Assert.IsType<PlanningLeaseDto>(proposal.Lease);
                var command = new AcceptStrengthPlanProposalRequestV020(proposal.ProposalId, lease.LeaseId, lease.Fence,
                    proposal.ProposalVersion, ReplacementConfirmed: true, ReducedVolumeAcknowledged: true);
                var store = new PostgresAcceptStrengthPlanProposalStoreV020(db);
                var accepted = await store.AcceptAsync(user, command, CancellationToken.None);
                Assert.True(accepted.Status == "Accepted", JsonSerializer.Serialize(accepted));
                Assert.Equal(accepted, await store.AcceptAsync(user, command, CancellationToken.None));
                var revision = await db.WorkoutPlanRevisionsV02.AsNoTracking().SingleAsync(row => row.UserId == user);
                Assert.Equal(accepted.RevisionId, revision.Id);
                Assert.Equal(proposal.ProposalId, revision.SourceProposalId);
            }
            var initialPlan = await db.WorkoutPlans.AsNoTracking().SingleAsync(row => row.UserId == user && row.IsActive);
            var prescribed = await db.WorkoutDayExercises.AsNoTracking().Where(row => row.UserId == user).ToListAsync();
            var prescribedIds = prescribed.Select(row => row.ExerciseId).ToHashSet();
            var restricted = (await db.WorkoutExerciseDefinitions.AsNoTracking().ToListAsync())
                .First(row => prescribedIds.Contains(row.Id) && row.Contraindications != InjuryFlag.None);
            var site = Enum.GetValues<InjuryFlag>().First(flag => flag != InjuryFlag.None &&
                (restricted.Contraindications & flag) == flag && ((int)flag & ((int)flag - 1)) == 0);
            var injuryStore = new PostgresCheckInAndInjuryStore(db, TimeProvider.System);
            var injuryId = Guid.NewGuid();
            var injury = new SaveInjuryObservationRequest(user, injuryId, (int)site, "Acute", today, PainLevel, today);
            await injuryStore.SaveInjuryAsync(injury, CancellationToken.None);
            var outcome = await new PostgresWorkoutPlanReconciler(db).ReconcileAsync(user);
            // Current activity policy blocks all automatic strength planning during injury.
            Assert.Equal(WorkoutReconciliationOutcome.NeedsReview, outcome);
            Assert.True(await db.WorkoutDays.AsNoTracking().AnyAsync(row => row.UserId == user && row.IsRemovedBySafety));
            var captured = await db.Set<WorkoutInjuryPrescriptionRecord>().AsNoTracking()
                .Where(row => row.UserId == user).ToListAsync();
            Assert.NotEmpty(captured);
            var episode = await db.WorkoutInjuryEpisodes.AsNoTracking().SingleAsync(row => row.UserId == user);
            Assert.Equal(injuryId, episode.InjuryLogId);
            Assert.All(captured, item =>
            {
                Assert.Equal(episode.Id, item.EpisodeId);
                var original = Assert.Single(prescribed, row => row.Id == item.OriginalWorkoutDayExerciseId);
                Assert.Equal((original.ExerciseId, original.Sets, original.Reps),
                    (item.OriginalExerciseId, item.OriginalSets, item.OriginalReps));
            });
            var injuryPlan = await db.WorkoutPlans.AsNoTracking().SingleAsync(row => row.UserId == user && row.IsActive);
            Assert.Equal(initialPlan.Id, injuryPlan.Id);
            await injuryStore.SaveInjuryAsync(injury with { Status = "Cleared" }, CancellationToken.None);
            if (catalogChanged)
            {
                changedExercise = restricted.Id;
                originalEvidence = restricted.ReviewEvidenceV020Json;
                var changedEvidence = JsonNode.Parse(originalEvidence!)!;
                changedEvidence["exercise"]!["conditionsAndExclusions"] = "Synthetic changed review condition; requires fresh confirmation.";
                var changedJson = changedEvidence.ToJsonString();
                await db.WorkoutExerciseDefinitions.Where(row => row.Id == restricted.Id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.ReviewEvidenceV020Json, changedJson));
            }
            Assert.Equal(catalogChanged ? WorkoutReconciliationOutcome.NeedsReview : WorkoutReconciliationOutcome.Completed,
                await new PostgresWorkoutPlanReconciler(db).ReconcileAsync(user));
            var clearedPlan = await db.WorkoutPlans.AsNoTracking().SingleAsync(row => row.UserId == user && row.IsActive);
            Assert.Equal(catalogChanged, injuryPlan.Id == clearedPlan.Id);
            const int InitialRevisionCount = 1;
            const int ClearedRevisionCount = 2;
            Assert.Equal(catalogChanged ? InitialRevisionCount : ClearedRevisionCount,
                await db.WorkoutPlanRevisionsV02.AsNoTracking().CountAsync(row => row.UserId == user));
            var originalIds = prescribed.Select(item => item.Id).ToHashSet();
            var unchanged = await db.WorkoutDayExercises.AsNoTracking()
                .Where(row => row.UserId == user && originalIds.Contains(row.Id)).ToListAsync();
            Assert.Equal(prescribed.OrderBy(row => row.Id).Select(row => (row.Id, row.ExerciseId, row.Sets, row.Reps)),
                unchanged.OrderBy(row => row.Id).Select(row => (row.Id, row.ExerciseId, row.Sets, row.Reps)));
            Assert.Equal(catalogChanged, (await db.WorkoutAccountPlanningStates.AsNoTracking().SingleAsync(row => row.UserId == user)).SafetyRegenerationPending);
        }
        finally
        {
            db.ChangeTracker.Clear();
            if (changedExercise is { } id)
                await db.WorkoutExerciseDefinitions.Where(row => row.Id == id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.ReviewEvidenceV020Json, originalEvidence));
            await db.Users.Where(row => row.Id == user).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task UnacknowledgedBeginnerInjuryDefersReconciliationAndPreservesHistory()
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(connection));
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        Assert.Equal("127.0.0.1", parsed.Host);
        Assert.StartsWith("mc_test_", parsed.Database, StringComparison.Ordinal);
        await using var db = new PostgresDbContext(new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connection).Options);
        var user = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var selectedDays = new[] { (int)today.DayOfWeek, (int)today.AddDays(SecondWorkoutDayOffset).DayOfWeek, (int)today.AddDays(ThirdWorkoutDayOffset).DayOfWeek };
        var strength = (int)WorkoutActivityType.GeneralStrength;
        var draft = new WorkoutPreferencesDto([strength], selectedDays,
            new Dictionary<int, int> { [strength] = StrengthMinutes }, "FullGym");
        await SyntheticTenantSession.SetAsync(db, user);
        try
        {
            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
                db.Users.Add(new UserRecord { Id = user, Email = $"injury-canonical-{user:N}@example.invalid",
                    Username = $"ic_{user.ToString("N")[..UsernameSuffixLength]}", CredentialType = "Password",
                    PasswordHash = "synthetic-hash", CreatedAt = now, UpdatedAt = now, IsOnboardingComplete = true });
                db.UserProfiles.Add(new UserProfileRecord { Id = Guid.NewGuid(), UserId = user, Name = "Synthetic",
                    DateOfBirth = BirthDate, Height = SyntheticHeight, Weight = SyntheticWeight, Goal = "BuildMuscle",
                    WeightGoalPace = "Maintain", TrainingExperienceLevel = "Beginner", WorkoutDaysPerWeek = WorkoutDays,
                    CycleLength = CycleLength, EquipmentLevel = "FullGym", ConcurrencyVersion = 1, UpdatedAt = now,
                    PreferredWorkoutActivityTypes = "GeneralStrength", AvailableWorkoutWeekdaysJson = JsonSerializer.Serialize(selectedDays),
                    ActivitySessionDurationsJson = JsonSerializer.Serialize(new Dictionary<string, int> { ["GeneralStrength"] = StrengthMinutes }),
                    RequestedActivitySessionsPerWeekJson = "{}", ExcludedStrengthExerciseIdsJson = "[]" });
                db.WorkoutAccountPlanningStates.Add(new WorkoutAccountPlanningStateRecord { Id = Guid.NewGuid(), UserId = user,
                    TimeZoneId = TimeZoneInfo.Utc.Id, BaselineExperience = TrainingExperienceLevel.Beginner,
                    EffectiveExperience = TrainingExperienceLevel.Beginner });
                await db.SaveChangesAsync();
                var request = new StrengthPlanPreviewRequestV020(WorkoutActivityDto.GeneralStrength, today, null,
                    WarmupMinutes: WarmupMinutes, CooldownMinutes: CooldownMinutes) { DraftPreferences = draft };
                var source = await new PostgresStrengthPlanPreviewSourceV020(db).LoadInCurrentTransactionAsync(user, CancellationToken.None);
                var preview = PreviewStrengthPlanV020UseCase.EvaluateLoadedSource(user, request, source);
                // Every reviewed catalog exercise has a user-specific condition. This synthetic
                // beginner has acknowledged none, so a strength proposal must not be accepted.
                Assert.NotEqual("Feasible", preview.Status);
                Assert.DoesNotContain(preview.Sessions, session => session.Slots.Any(slot => slot.ExerciseId.HasValue));
                await transaction.CommitAsync();
            }
            db.ChangeTracker.Clear();
            Assert.False(await db.WorkoutPlans.AsNoTracking().AnyAsync(row => row.UserId == user));
            Assert.False(await db.WorkoutPlanProposalsV02.AsNoTracking().AnyAsync(row => row.UserId == user));

            // A legacy plan without a verified revision can still have completed history. Injury
            // handling must suppress future work and retain that history while review is pending.
            var definition = (await db.WorkoutExerciseDefinitions.AsNoTracking().ToListAsync())
                .First(row => row.Contraindications != InjuryFlag.None);
            var flags = definition.Contraindications;
            var site = Enum.GetValues<InjuryFlag>().First(flag => flag != InjuryFlag.None && (flags & flag) == flag &&
                ((int)flag & ((int)flag - 1)) == 0);
            var planId = Guid.NewGuid();
            var futureDay = Guid.NewGuid();
            db.WorkoutPlans.Add(new WorkoutPlanRecord { Id = planId, UserId = user, Name = "Synthetic legacy plan",
                IsActive = true, CyclePhaseTarget = "Follicular", CreatedAt = now });
            db.WorkoutDays.Add(new WorkoutDayRecord { Id = futureDay, UserId = user, WorkoutPlanId = planId,
                Date = today.AddDays(2), DayOfWeek = (int)today.AddDays(SecondWorkoutDayOffset).DayOfWeek,
                WorkoutType = nameof(WorkoutType.Strength), Name = "Synthetic future strength" });
            db.WorkoutDayExercises.Add(new WorkoutDayExerciseRecord { Id = Guid.NewGuid(), UserId = user,
                WorkoutDayId = futureDay, ExerciseId = definition.Id, Sets = CompletedSets, Reps = CompletedRepetitions });
            var completedDay = Guid.NewGuid();
            var logId = Guid.NewGuid();
            var completedExerciseId = Guid.NewGuid();
            var exerciseLogId = Guid.NewGuid();
            db.WorkoutDays.Add(new WorkoutDayRecord { Id = completedDay, UserId = user, WorkoutPlanId = planId,
                Date = today.AddDays(-1), DayOfWeek = (int)today.AddDays(-1).DayOfWeek,
                WorkoutType = nameof(WorkoutType.Strength), Name = "Synthetic completed" });
            db.WorkoutLogs.Add(new WorkoutLogRecord { Id = logId, UserId = user, WorkoutDayId = completedDay,
                Date = today.AddDays(-1), CompletionPercent = CompletePercent, CreatedAt = now });
            db.WorkoutDayExercises.Add(new WorkoutDayExerciseRecord { Id = completedExerciseId, UserId = user,
                WorkoutDayId = completedDay, ExerciseId = definition.Id, Sets = CompletedSets, Reps = CompletedRepetitions });
            db.WorkoutExerciseLogs.Add(new WorkoutExerciseLogRecord { Id = exerciseLogId, UserId = user, WorkoutLogId = logId,
                WorkoutDayExerciseId = completedExerciseId, ExerciseId = definition.Id,
                CompletedSets = CompletedSets, CompletedReps = CompletedRepetitions, CreatedAt = now });
            await db.SaveChangesAsync();
            var injuryId = Guid.NewGuid();
            var injuryStore = new PostgresCheckInAndInjuryStore(db, TimeProvider.System);
            var injury = new SaveInjuryObservationRequest(user, injuryId, (int)site, "Acute", today, PainLevel, today);
            await injuryStore.SaveInjuryAsync(injury, CancellationToken.None);
            Assert.True((await db.WorkoutDays.AsNoTracking().SingleAsync(row => row.Id == futureDay)).IsRemovedBySafety);
            Assert.True((await db.WorkoutAccountPlanningStates.AsNoTracking().SingleAsync(row => row.UserId == user)).SafetyRegenerationPending);
            Assert.Equal(WorkoutReconciliationOutcome.NeedsReview, await new PostgresWorkoutPlanReconciler(db).ReconcileAsync(user));
            await AssertHistoryAndPlanPreservedAsync();
            await injuryStore.SaveInjuryAsync(injury with { Status = "Cleared" }, CancellationToken.None);
            Assert.Equal(WorkoutReconciliationOutcome.NeedsReview, await new PostgresWorkoutPlanReconciler(db).ReconcileAsync(user));
            Assert.NotNull((await db.WorkoutInjuryLogs.AsNoTracking().SingleAsync(row => row.Id == injuryId)).ClearedDate);
            await AssertHistoryAndPlanPreservedAsync();

            async Task AssertHistoryAndPlanPreservedAsync()
            {
                Assert.True(await db.WorkoutLogs.AsNoTracking().AnyAsync(row => row.Id == logId && row.UserId == user));
                Assert.True(await db.WorkoutExerciseLogs.AsNoTracking().AnyAsync(row => row.Id == exerciseLogId &&
                    row.CompletedSets == CompletedSets && row.CompletedReps == CompletedRepetitions));
                Assert.Equal(planId, (await db.WorkoutPlans.AsNoTracking().SingleAsync(row => row.UserId == user && row.IsActive)).Id);
                Assert.False(await db.WorkoutPlanRevisionsV02.AsNoTracking().AnyAsync(row => row.UserId == user));
                Assert.False(await db.Set<WorkoutInjuryOmittedSlotRecord>().AsNoTracking().AnyAsync(row => row.UserId == user));
                Assert.True((await db.WorkoutAccountPlanningStates.AsNoTracking().SingleAsync(row => row.UserId == user)).SafetyRegenerationPending);
            }
        }
        finally { db.ChangeTracker.Clear(); await db.Users.Where(row => row.Id == user).ExecuteDeleteAsync(); }
    }
}
