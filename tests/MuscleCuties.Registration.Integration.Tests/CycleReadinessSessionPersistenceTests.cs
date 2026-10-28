using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.ActiveSession;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Session;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.CheckInAndInjury;
using MuscleCuties.Infrastructure.PostgreSql.Cycle.PhaseLog;
using MuscleCuties.Infrastructure.PostgreSql.Account.Deletion;
using MuscleCuties.UseCases.Features.CheckInAndInjury;
using MuscleCuties.UseCases.Features.Cycle.PhaseLog;
using MuscleCuties.UseCases.Features.Cycle.Contracts;
using MuscleCuties.UseCases.Features.Workout.ActiveSession;
using MuscleCuties.UseCases.Features.Workout.Session.Contracts;
using Npgsql;
using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class CycleReadinessSessionPersistenceTests
{
    private const string ConnectionVariable = "MC_REGISTRATION_WRITE_TEST_POSTGRES";
    private const int OriginalSets = 3;
    private const int OriginalOptionalSets = 2;
    private const int ReducedSets = 2;
    private const int ReducedOptionalSets = 1;
    private const int ChangedBaselineSets = 4;
    private const int Repetitions = 8;
    private const int CycleLengthDays = 28;
    private const int HistoricalCycleCountForReliableEstimate = 3;
    private const int SyntheticUsernameIdentifierLength = 10;
    private const int PreviewMinutes = 5;
    private const int FutureDateOffsetDays = 1;
    private const int ExpectedCurrentAndFutureOccurrences = 2;
    private const int ReportedPain = 1;
    private const int NoPain = 0;

    [Fact]
    public async Task CheckInAndPhaseWritesPersistPendingWhilePreviewDefersRetry()
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(connection));
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        Assert.Equal("127.0.0.1", parsed.Host);
        Assert.StartsWith("mc_test_", parsed.Database, StringComparison.Ordinal);
        await using var db = new PostgresDbContext(new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connection).Options);
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        await SyntheticTenantSession.SetAsync(db, userId);
        db.Users.Add(new UserRecord { Id = userId, Email = $"cycle-write-{userId:N}@example.invalid",
            Username = $"cw_{userId.ToString("N")[..SyntheticUsernameIdentifierLength]}",
            CredentialType = "Password", PasswordHash = "synthetic-hash", CreatedAt = now, UpdatedAt = now });
        db.WorkoutAccountPlanningStates.Add(new WorkoutAccountPlanningStateRecord
            { Id = Guid.NewGuid(), UserId = userId, TimeZoneId = "UTC" });
        var proposalId = Guid.NewGuid();
        db.WorkoutPlanProposalsV02.Add(new WorkoutPlanProposalV02Record { Id = proposalId, UserId = userId,
            PlanId = Guid.NewGuid(), Status = "Proposed", RulesetVersion = "synthetic", TrainingPolicyVersion = "synthetic",
            LeaseId = Guid.NewGuid(), LeaseExpiresAtUtc = now.AddMinutes(PreviewMinutes), CreatedAtUtc = now });
        await db.SaveChangesAsync();
        try
        {
            var checkInStore = new PostgresCheckInAndInjuryStore(db, TimeProvider.System);
            var checkIn = new SaveDailyCheckInRequest(userId, Guid.NewGuid(), today, null, null, null, null, null,
                null, null, null, null, PlanningTimeZoneId: "UTC");
            var saved = await checkInStore.SaveCheckInAsync(checkIn, CancellationToken.None);
            var replay = await checkInStore.SaveCheckInAsync(checkIn, CancellationToken.None);
            Assert.Equal(saved.PlanningRevision, replay.PlanningRevision);
            Assert.True(await db.WorkoutAccountPlanningStates.AsNoTracking().Where(x => x.UserId == userId)
                .Select(x => x.CycleReadinessPending).SingleAsync());
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                Assert.False(await PostgresCycleReadinessReconciler.ReconcileAsync(db, userId, new DateTimeOffset(now), CancellationToken.None));
                await tx.CommitAsync();
            }
            var state = await db.WorkoutAccountPlanningStates.SingleAsync(x => x.UserId == userId);
            Assert.True(state.CycleReadinessPending);
            state.CycleReadinessPending = false;
            await db.SaveChangesAsync();
            var result = await new PostgresCyclePhaseLogStore(db, TimeProvider.System).UpsertAsync(
                new SaveCyclePhaseLogInput(userId.ToString(), today, CyclePhaseDto.Menstrual), CancellationToken.None);
            Assert.Equal(SaveCyclePhaseLogOutcomeKind.Saved, result.Outcome);
            Assert.True(await db.WorkoutAccountPlanningStates.AsNoTracking().Where(x => x.UserId == userId)
                .Select(x => x.CycleReadinessPending).SingleAsync());
            var preview = await db.WorkoutPlanProposalsV02.SingleAsync(x => x.UserId == userId && x.Id == proposalId);
            preview.Status = "Expired"; preview.DecidedAtUtc = now;
            await db.SaveChangesAsync();
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                Assert.True(await PostgresCycleReadinessReconciler.ReconcileAsync(db, userId, new DateTimeOffset(now), CancellationToken.None));
                await tx.CommitAsync();
            }
            Assert.False(await db.WorkoutAccountPlanningStates.AsNoTracking().Where(x => x.UserId == userId)
                .Select(x => x.CycleReadinessPending).SingleAsync());

            var planId = Guid.NewGuid();
            var dayId = Guid.NewGuid();
            var exerciseId = await db.WorkoutExerciseDefinitions.AsNoTracking().Select(x => x.Id).FirstAsync();
            db.UserProfiles.Add(new UserProfileRecord { Id = Guid.NewGuid(), UserId = userId, Name = "Synthetic",
                ReproductiveContextJson = ReproductiveContextCodec.Serialize(new ReproductiveContext(PregnancyStatus.No, ContraceptionStatus.No)),
                PhaseBaselinesJson = "{\"Menstrual\":{\"Pain\":5,\"Energy\":1},\"Follicular\":{\"Pain\":1,\"Energy\":5},\"Ovulatory\":{\"Pain\":1,\"Energy\":5},\"Luteal\":{\"Pain\":1,\"Energy\":5}}",
                UpdatedAt = now });
            db.WorkoutPlans.Add(new WorkoutPlanRecord { Id = planId, UserId = userId, Name = "Synthetic",
                IsActive = true, CyclePhaseTarget = "Menstrual", CreatedAt = now });
            db.WorkoutDays.Add(new WorkoutDayRecord { Id = dayId, UserId = userId, WorkoutPlanId = planId,
                Date = today, DayOfWeek = (int)today.DayOfWeek, WorkoutType = "Strength", Name = "Synthetic" });
            db.WorkoutDayExercises.Add(new WorkoutDayExerciseRecord { Id = Guid.NewGuid(), UserId = userId,
                WorkoutDayId = dayId, ExerciseId = exerciseId, PlanningSlotId = "synthetic-accessory",
                Sets = OriginalSets, OptionalSets = OriginalOptionalSets, Reps = Repetitions, IsOptional = true });
            await db.SaveChangesAsync();
            var immediatePhase = await new PostgresCyclePhaseLogStore(db, TimeProvider.System).UpsertAsync(
                new SaveCyclePhaseLogInput(userId.ToString(), today, CyclePhaseDto.Menstrual), CancellationToken.None);
            Assert.Equal(SaveCyclePhaseLogOutcomeKind.Saved, immediatePhase.Outcome);
            var immediateOccurrence = await db.WorkoutCycleReadinessOccurrences.AsNoTracking()
                .SingleAsync(row => row.UserId == userId && row.WorkoutDayId == dayId);
            Assert.Equal(ReducedSets,
                PlannedPrescriptionSnapshot.Deserialize(immediateOccurrence.PrescriptionJson)!.Exercises[0].Sets);
            var sessionId = Guid.NewGuid();
            var detail = await new PostgresWorkoutPlanAndSessionStore(db).GetDetailAsync(userId,
                PostgresWorkoutPlanAndSessionStore.ToStableWorkoutIntId(dayId), CancellationToken.None);
            Assert.Contains($"{ReducedSets} sets", Assert.Single(detail.Exercises).TargetText);
            Assert.Equal(WorkoutDoseAdjustmentReason.ProvisionalPhaseFeeling, detail.DoseAdjustmentReason);
            Assert.Equal(WorkoutDoseAdjustmentConfidence.Provisional, detail.DoseAdjustmentConfidence);
            var command = new StartWorkoutSessionCommand(sessionId.ToString("N"), sessionId, "Synthetic", now,
                PlannedWorkoutDayId: PostgresWorkoutPlanAndSessionStore.ToStableWorkoutIntId(dayId));
            var sessions = new PostgresWorkoutActiveSessionStore(db, new PostgresIdempotencyExecutor(), TimeProvider.System);
            await checkInStore.SaveCheckInAsync(checkIn with { Pain = ReportedPain }, CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() => sessions.StartSessionAsync(userId, command, CancellationToken.None));
            await checkInStore.SaveCheckInAsync(checkIn with { Pain = NoPain }, CancellationToken.None);
            var symptomId = Guid.NewGuid();
            var cycleId = await db.CycleLogs.AsNoTracking().Where(x => x.UserId == userId && x.StartDate == today)
                .Select(x => x.Id).SingleAsync();
            db.SymptomLogs.Add(new SymptomLogRecord { Id = symptomId, UserId = userId, Date = today,
                CycleLogId = cycleId, SymptomType = "synthetic", Severity = ReportedPain, CreatedAt = now });
            await db.SaveChangesAsync();
            var cardio = new PlannedPrescriptionSnapshot(dayId, "Synthetic cardio", "Cardio", null, null, []);
            var cardioResult = await PostgresCycleReadinessPrescription.ApplyAsync(db, userId, today, "UTC",
                new DateTimeOffset(now), cardio, CancellationToken.None);
            Assert.Equal(MuscleCuties.Core.Domain.Workout.Planning.CycleReadinessPrescriptionRule.SafetyReviewRequired,
                cardioResult.DoseAdjustmentReasonCode);
            await Assert.ThrowsAsync<InvalidOperationException>(() => sessions.StartSessionAsync(userId, command, CancellationToken.None));
            var symptom = await db.SymptomLogs.SingleAsync(x => x.UserId == userId && x.Id == symptomId);
            db.SymptomLogs.Remove(symptom);
            db.WorkoutInjuryLogs.Add(new WorkoutInjuryLogRecord { Id = Guid.NewGuid(), UserId = userId,
                Status = "Acute", SiteFlag = ReportedPain, Since = today, Date = today, Pain = ReportedPain, CreatedAt = now });
            await db.SaveChangesAsync();
            await sessions.StartSessionAsync(userId, command, CancellationToken.None);
            await sessions.StartSessionAsync(userId, command, CancellationToken.None);
            var session = await db.WorkoutSessionLogs.AsNoTracking().SingleAsync(x => x.UserId == userId && x.Id == sessionId);
            Assert.Equal(ReducedSets, PlannedPrescriptionSnapshot.Deserialize(session.PlannedPrescriptionSnapshotJson)!.Exercises[0].Sets);
            var activeDetail = await new PostgresWorkoutPlanAndSessionStore(db).GetDetailAsync(userId,
                PostgresWorkoutPlanAndSessionStore.ToStableWorkoutIntId(dayId), CancellationToken.None);
            Assert.Equal(detail.DoseAdjustmentReason, activeDetail.DoseAdjustmentReason);
            Assert.Single(await db.WorkoutSessionLogs.AsNoTracking().Where(x => x.UserId == userId).ToListAsync());
            var completed = await db.WorkoutSessionLogs.SingleAsync(x => x.UserId == userId && x.Id == sessionId);
            completed.CompletedAt = now;
            var baseline = await db.WorkoutDayExercises.SingleAsync(x => x.UserId == userId && x.WorkoutDayId == dayId);
            baseline.Sets = ChangedBaselineSets;
            await db.SaveChangesAsync();
            var completedDetail = await new PostgresWorkoutPlanAndSessionStore(db).GetDetailAsync(userId,
                PostgresWorkoutPlanAndSessionStore.ToStableWorkoutIntId(dayId), CancellationToken.None);
            Assert.Contains($"{ReducedSets} sets", Assert.Single(completedDetail.Exercises).TargetText);
        }
        finally
        {
            db.ChangeTracker.Clear();
            await new PostgresAccountPurgeStore(db).PurgeAccountDataAsync(userId);
        }
    }

    [Fact]
    public async Task OnlyExplicitLinkedPhaseCanAdjustSnapshotAndOptionalSetsStayValid()
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(connection));
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        Assert.Equal("127.0.0.1", parsed.Host);
        Assert.StartsWith("mc_test_", parsed.Database, StringComparison.Ordinal);
        await using var db = new PostgresDbContext(new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(connection).Options);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var dayId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var date = DateOnly.FromDateTime(now);
        var futureDate = date.AddDays(FutureDateOffsetDays);
        var futureDayId = Guid.NewGuid();
        await SyntheticTenantSession.SetAsync(db, userId);
        db.Users.Add(new UserRecord { Id = userId, Email = $"cycle-{userId:N}@example.invalid",
            Username = $"cycle_{userId.ToString("N")[..SyntheticUsernameIdentifierLength]}", CredentialType = "Password", PasswordHash = "synthetic-hash",
            CreatedAt = now, UpdatedAt = now });
        db.UserProfiles.Add(new UserProfileRecord { Id = Guid.NewGuid(), UserId = userId, Name = "Synthetic",
            ReproductiveContextJson = ReproductiveContextCodec.Serialize(new ReproductiveContext(PregnancyStatus.No, ContraceptionStatus.No)),
            PhaseBaselinesJson = "{\"Menstrual\":{\"Pain\":5,\"Energy\":1},\"Follicular\":{\"Pain\":1,\"Energy\":5},\"Ovulatory\":{\"Pain\":1,\"Energy\":5},\"Luteal\":{\"Pain\":1,\"Energy\":5}}",
            UpdatedAt = now });
        db.WorkoutPlans.Add(new WorkoutPlanRecord { Id = planId, UserId = userId, Name = "Synthetic",
            IsActive = true, CyclePhaseTarget = "Menstrual", CreatedAt = now });
        var state = new WorkoutAccountPlanningStateRecord { Id = Guid.NewGuid(), UserId = userId,
            TimeZoneId = "UTC", CycleReadinessPending = true };
        db.WorkoutAccountPlanningStates.Add(state);
        db.WorkoutDays.Add(new WorkoutDayRecord { Id = dayId, UserId = userId, WorkoutPlanId = planId,
            Date = date, DayOfWeek = (int)date.DayOfWeek, WorkoutType = "Strength", Name = "Synthetic" });
        db.WorkoutDays.Add(new WorkoutDayRecord { Id = futureDayId, UserId = userId, WorkoutPlanId = planId,
            Date = futureDate, DayOfWeek = (int)futureDate.DayOfWeek, WorkoutType = "Strength", Name = "Future synthetic" });
        await db.SaveChangesAsync();
        var exerciseId = await db.WorkoutExerciseDefinitions.AsNoTracking().Select(row => row.Id).FirstAsync();
        var exercise = new WorkoutDayExerciseRecord { Id = Guid.NewGuid(), UserId = userId,
            WorkoutDayId = dayId, ExerciseId = exerciseId, PlanningSlotId = "synthetic-accessory",
            Sets = OriginalSets, OptionalSets = OriginalOptionalSets, Reps = Repetitions, IsOptional = true };
        var phase = new CyclePhaseLogRecord { Id = Guid.NewGuid(), UserId = userId, LocalDate = date,
            Phase = "Menstrual", LoggedAt = now, CreatedAt = now, Note = "Set from onboarding quiz" };
        db.WorkoutDayExercises.Add(exercise);
        db.WorkoutDayExercises.Add(new WorkoutDayExerciseRecord { Id = Guid.NewGuid(), UserId = userId,
            WorkoutDayId = futureDayId, ExerciseId = exerciseId, PlanningSlotId = "future-synthetic-accessory",
            Sets = OriginalSets, OptionalSets = OriginalOptionalSets, Reps = Repetitions, IsOptional = true });
        db.CyclePhaseLogs.Add(phase);
        await db.SaveChangesAsync();
        var snapshot = new PlannedPrescriptionSnapshot(dayId, "Synthetic", "Strength", null, null,
            [PlannedExerciseSnapshot.FromRecord(exercise)]);
        var onboarding = await PostgresCycleReadinessPrescription.ApplyAsync(db, userId, date, "UTC",
            new DateTimeOffset(now), snapshot, CancellationToken.None);
        Assert.Equal(OriginalSets, onboarding.Exercises[0].Sets);

        var cycle = new CycleLogRecord { Id = Guid.NewGuid(), UserId = userId, StartDate = date,
            CycleLength = CycleLengthDays, CreatedAt = now };
        db.CycleLogs.Add(cycle);
        phase.CycleLogId = cycle.Id;
        phase.Note = null;
        var historicalCycles = Enumerable.Range(1, HistoricalCycleCountForReliableEstimate)
            .Select(index => new CycleLogRecord { Id = Guid.NewGuid(), UserId = userId,
                StartDate = date.AddDays(-CycleLengthDays * index),
                EndDate = date.AddDays(-CycleLengthDays * (index - 1)),
                CycleLength = CycleLengthDays, CreatedAt = now }).ToArray();
        var historicalAnchors = historicalCycles.Select(previous => new CyclePhaseLogRecord
        {
            Id = Guid.NewGuid(), UserId = userId, CycleLogId = previous.Id,
            LocalDate = previous.StartDate, Phase = "Menstrual", LoggedAt = now, CreatedAt = now
        }).ToArray();
        db.CycleLogs.AddRange(historicalCycles);
        db.CyclePhaseLogs.AddRange(historicalAnchors);
        await db.SaveChangesAsync();
        var confirmed = await PostgresCycleReadinessPrescription.ApplyAsync(db, userId, date, "UTC",
            new DateTimeOffset(now), snapshot, CancellationToken.None);
        Assert.Equal(ReducedSets, confirmed.Exercises[0].Sets);
        Assert.Equal(ReducedOptionalSets, confirmed.Exercises[0].OptionalSets);
        Assert.Equal(OriginalSets, (await db.WorkoutDayExercises.AsNoTracking().SingleAsync(row => row.Id == exercise.Id)).Sets);

        Assert.True(await PostgresCycleReadinessReconciler.ReconcileAsync(db, userId, new DateTimeOffset(now), CancellationToken.None));
        var occurrence = await db.WorkoutCycleReadinessOccurrences.SingleAsync(row => row.UserId == userId && row.WorkoutDayId == dayId);
        Assert.Equal(ReducedSets, PlannedPrescriptionSnapshot.Deserialize(occurrence.PrescriptionJson)!.Exercises[0].Sets);
        var futureOccurrence = await db.WorkoutCycleReadinessOccurrences.SingleAsync(row => row.UserId == userId && row.WorkoutDayId == futureDayId);
        Assert.Equal(futureDate, futureOccurrence.LocalDate);
        Assert.Equal(ReducedSets, PlannedPrescriptionSnapshot.Deserialize(futureOccurrence.PrescriptionJson)!.Exercises[0].Sets);
        Assert.Equal(nameof(PhaseReviewProvenance.Estimated),
            PlannedPrescriptionSnapshot.Deserialize(futureOccurrence.PrescriptionJson)!.DoseAdjustmentPhaseProvenance);
        var firstUpdated = occurrence.UpdatedAtUtc;
        var futureFirstUpdated = futureOccurrence.UpdatedAtUtc;
        Assert.True(await PostgresCycleReadinessReconciler.ReconcileAsync(db, userId, new DateTimeOffset(now).AddMinutes(1), CancellationToken.None));
        Assert.Equal(firstUpdated, occurrence.UpdatedAtUtc);
        Assert.Equal(futureFirstUpdated, futureOccurrence.UpdatedAtUtc);
        Assert.Equal(ExpectedCurrentAndFutureOccurrences,
            await db.WorkoutCycleReadinessOccurrences.CountAsync(row => row.UserId == userId));

        historicalAnchors[0].CycleLogId = null;
        await PostgresCycleReadinessReconciler.MarkPendingAsync(db, userId, CancellationToken.None);
        await db.SaveChangesAsync();
        Assert.True(await PostgresCycleReadinessReconciler.ReconcileAsync(db, userId, new DateTimeOffset(now), CancellationToken.None));
        Assert.Equal(OriginalSets, PlannedPrescriptionSnapshot.Deserialize(futureOccurrence.PrescriptionJson)!.Exercises[0].Sets);
        Assert.Equal(ReducedSets, PlannedPrescriptionSnapshot.Deserialize(occurrence.PrescriptionJson)!.Exercises[0].Sets);

        state.CycleReadinessPending = true;
        var preview = new WorkoutPlanProposalV02Record { Id = Guid.NewGuid(), UserId = userId, PlanId = planId,
            Status = "Proposed", RulesetVersion = "synthetic", TrainingPolicyVersion = "synthetic",
            LeaseId = Guid.NewGuid(), LeaseExpiresAtUtc = now.AddMinutes(PreviewMinutes), CreatedAtUtc = now };
        db.WorkoutPlanProposalsV02.Add(preview);
        await db.SaveChangesAsync();
        Assert.False(await PostgresCycleReadinessReconciler.ReconcileAsync(db, userId, new DateTimeOffset(now), CancellationToken.None));
        Assert.True(state.CycleReadinessPending);
        preview.Status = "Expired"; preview.DecidedAtUtc = now;
        await db.SaveChangesAsync();
        Assert.True(await PostgresCycleReadinessReconciler.ReconcileAsync(db, userId, new DateTimeOffset(now), CancellationToken.None));
        Assert.False(state.CycleReadinessPending);
        await transaction.RollbackAsync();
    }
}
