using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
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
    private const int PainLevel = 2;
    private const int CompletePercent = 100;
    private const int CompletedSets = 2;
    private const int CompletedRepetitions = 8;
    private const int UsernameSuffixLength = 10;
    private static readonly DateOnly BirthDate = new(1990, 1, 1);

    [Fact]
    public async Task CanonicalPlanInjuryAndClearancePreserveHistoryAndRestoreProvenSlot()
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
        var selectedDays = Enumerable.Range(0, WorkoutDays).Select(offset => (int)today.AddDays(offset).DayOfWeek).ToArray();
        var strength = (int)WorkoutActivityType.GeneralStrength;
        var walking = (int)WorkoutActivityType.Walking;
        var draft = new WorkoutPreferencesDto([strength, walking], selectedDays,
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
                    PreferredWorkoutActivityTypes = "GeneralStrength,Walking", AvailableWorkoutWeekdaysJson = JsonSerializer.Serialize(selectedDays),
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
                Assert.Equal("Feasible", preview.Status);
                Assert.NotEmpty(preview.Sessions);
                var proposal = await new PostgresStrengthPlanProposalStoreV020(db).SaveInCurrentTransactionAsync(user, request, preview, CancellationToken.None);
                Assert.True(proposal.IsPersisted);
                Assert.NotNull(proposal.Lease);
                var accepted = await new PostgresAcceptStrengthPlanProposalStoreV020(db).AcceptInCurrentTransactionAsync(user,
                    new AcceptStrengthPlanProposalRequestV020(proposal.ProposalId, proposal.Lease.LeaseId, proposal.Lease.Fence,
                        proposal.ProposalVersion, ReducedVolumeAcknowledged: true),
                    PostgresAcceptStrengthPlanProposalStoreV020.AcceptanceOrigin.User, CancellationToken.None);
                Assert.Equal("Accepted", accepted.Status);
                Assert.NotNull(accepted.PlanId);
                await transaction.CommitAsync();
            }
            db.ChangeTracker.Clear();
            var plan = await db.WorkoutPlans.AsNoTracking().SingleAsync(row => row.UserId == user && row.IsActive);
            var days = await db.WorkoutDays.AsNoTracking().Where(row => row.UserId == user && row.WorkoutPlanId == plan.Id &&
                row.WorkoutType == nameof(WorkoutType.Strength)).OrderBy(row => row.Date).ToListAsync();
            Assert.NotEmpty(days);
            var dayIds = days.Select(row => row.Id).ToArray();
            var exercises = await db.WorkoutDayExercises.AsNoTracking().Where(row => row.UserId == user && dayIds.Contains(row.WorkoutDayId)).ToListAsync();
            var definitions = await db.WorkoutExerciseDefinitions.AsNoTracking().Where(row => exercises.Select(e => e.ExerciseId).Contains(row.Id)).ToListAsync();
            var affected = exercises.First(row => definitions.Any(definition => definition.Id == row.ExerciseId && definition.Contraindications != InjuryFlag.None));
            var flags = definitions.Single(row => row.Id == affected.ExerciseId).Contraindications;
            var site = Enum.GetValues<InjuryFlag>().First(flag => flag != InjuryFlag.None && (flags & flag) == flag &&
                ((int)flag & ((int)flag - 1)) == 0);
            var completedDay = Guid.NewGuid();
            var logId = Guid.NewGuid();
            var completedExerciseId = Guid.NewGuid();
            var exerciseLogId = Guid.NewGuid();
            db.WorkoutDays.Add(new WorkoutDayRecord { Id = completedDay, UserId = user, WorkoutPlanId = plan.Id,
                Date = today.AddDays(-1), DayOfWeek = (int)today.AddDays(-1).DayOfWeek,
                WorkoutType = nameof(WorkoutType.Strength), Name = "Synthetic completed" });
            db.WorkoutLogs.Add(new WorkoutLogRecord { Id = logId, UserId = user, WorkoutDayId = completedDay,
                Date = today.AddDays(-1), CompletionPercent = CompletePercent, CreatedAt = now });
            db.WorkoutDayExercises.Add(new WorkoutDayExerciseRecord { Id = completedExerciseId, UserId = user,
                WorkoutDayId = completedDay, ExerciseId = affected.ExerciseId, Sets = CompletedSets, Reps = CompletedRepetitions });
            db.WorkoutExerciseLogs.Add(new WorkoutExerciseLogRecord { Id = exerciseLogId, UserId = user, WorkoutLogId = logId,
                WorkoutDayExerciseId = completedExerciseId, ExerciseId = affected.ExerciseId,
                CompletedSets = CompletedSets, CompletedReps = CompletedRepetitions, CreatedAt = now });
            await db.SaveChangesAsync();
            var injuryId = Guid.NewGuid();
            var injuryStore = new PostgresCheckInAndInjuryStore(db);
            var injury = new SaveInjuryObservationRequest(user, injuryId, (int)site, "Acute", today, PainLevel, today);
            await injuryStore.SaveInjuryAsync(injury, CancellationToken.None);
            Assert.Equal(WorkoutReconciliationOutcome.Completed, await new PostgresWorkoutPlanReconciler(db).ReconcileAsync(user));
            Assert.True(await db.WorkoutLogs.AsNoTracking().AnyAsync(row => row.Id == logId && row.UserId == user));
            Assert.True(await db.WorkoutExerciseLogs.AsNoTracking().AnyAsync(row => row.Id == exerciseLogId &&
                row.CompletedSets == CompletedSets && row.CompletedReps == CompletedRepetitions));
            var activePlan = await db.WorkoutPlans.AsNoTracking().SingleAsync(row => row.UserId == user && row.IsActive);
            var omitted = await db.Set<WorkoutInjuryOmittedSlotRecord>().AsNoTracking().Where(row => row.UserId == user && row.PlanId == activePlan.Id).ToListAsync();
            var links = await db.WorkoutInjuryPrescriptionLinks.AsNoTracking().Where(row => row.UserId == user).ToListAsync();
            Assert.True(omitted.Count > 0 || links.Count > 0);
            await injuryStore.SaveInjuryAsync(injury with { Status = "Cleared" }, CancellationToken.None);
            Assert.Equal(WorkoutReconciliationOutcome.Completed, await new PostgresWorkoutPlanReconciler(db).ReconcileAsync(user));
            Assert.True(await db.WorkoutLogs.AsNoTracking().AnyAsync(row => row.Id == logId && row.UserId == user));
            Assert.True(await db.WorkoutExerciseLogs.AsNoTracking().AnyAsync(row => row.Id == exerciseLogId &&
                row.CompletedSets == CompletedSets && row.CompletedReps == CompletedRepetitions));
            var dayOnePlan = await db.WorkoutPlans.AsNoTracking().SingleAsync(row => row.UserId == user && row.IsActive);
            var dayOneDayIds = await db.WorkoutDays.AsNoTracking().Where(row => row.UserId == user && row.WorkoutPlanId == dayOnePlan.Id &&
                row.Date >= today && row.Date < today.AddDays(WorkoutInjuryClearanceRule.OptionalCalendarDaysIncludingClearance))
                .Select(row => row.Id).ToArrayAsync();
            Assert.Contains(await db.WorkoutDayExercises.AsNoTracking().Where(row => row.UserId == user &&
                dayOneDayIds.Contains(row.WorkoutDayId) && row.InjuryOriginalExerciseId == affected.ExerciseId).ToListAsync(), row => row.IsOptional);
            var episode = await db.WorkoutInjuryEpisodes.SingleAsync(row => row.UserId == user && row.InjuryLogId == injuryId);
            var previousClearance = today.AddDays(-WorkoutInjuryClearanceRule.OptionalCalendarDaysIncludingClearance);
            episode.StartedOn = previousClearance;
            episode.ClearedOn = previousClearance;
            var log = await db.WorkoutInjuryLogs.SingleAsync(row => row.UserId == user && row.Id == injuryId);
            log.ClearedDate = previousClearance;
            var state = await db.WorkoutAccountPlanningStates.SingleAsync(row => row.UserId == user);
            state.SafetyRegenerationPending = true;
            await db.SaveChangesAsync();
            Assert.Equal(WorkoutReconciliationOutcome.Completed, await new PostgresWorkoutPlanReconciler(db).ReconcileAsync(user));
            Assert.True(await db.WorkoutLogs.AsNoTracking().AnyAsync(row => row.Id == logId && row.UserId == user));
            Assert.True(await db.WorkoutExerciseLogs.AsNoTracking().AnyAsync(row => row.Id == exerciseLogId &&
                row.CompletedSets == CompletedSets && row.CompletedReps == CompletedRepetitions));
            var dayEightPlan = await db.WorkoutPlans.AsNoTracking().SingleAsync(row => row.UserId == user && row.IsActive);
            var dayEightIds = await db.WorkoutDays.AsNoTracking().Where(row => row.UserId == user && row.WorkoutPlanId == dayEightPlan.Id && row.Date >= today)
                .Select(row => row.Id).ToArrayAsync();
            Assert.Contains(await db.WorkoutDayExercises.AsNoTracking().Where(row => row.UserId == user &&
                dayEightIds.Contains(row.WorkoutDayId) && row.InjuryOriginalExerciseId == affected.ExerciseId).ToListAsync(),
                row => row.ExerciseId == affected.ExerciseId && !row.IsOptional);
        }
        finally { db.ChangeTracker.Clear(); await db.Users.Where(row => row.Id == user).ExecuteDeleteAsync(); }
    }
}
