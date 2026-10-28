using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.ActiveSession;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Session;
using Npgsql;
using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class CyclePersonalizationEvidencePersistenceTests
{
    private const string ConnectionVariable = "MC_REGISTRATION_WRITE_TEST_POSTGRES";
    private const int CycleSpacingDays = 30;
    private const int ObservationOffsetDays = 10;
    private const int HistoricalCycleCount = 3;
    private const int SessionStartHour = 14;
    private const int CheckInLeadHours = 1;
    private const int LowReadinessValue = 1;
    private const int HighSorenessAndStress = 5;
    private const int InitialRevision = 1;
    private const int IdentifierLength = 10;
    private const int SyntheticSets = 2;
    private const int SyntheticRepetitions = 8;

    [Fact]
    public async Task Loader_requires_distinct_explicit_cycles_and_check_in_before_completed_session()
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(connection));
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        Assert.Equal("127.0.0.1", parsed.Host);
        Assert.StartsWith("mc_test_", parsed.Database, StringComparison.Ordinal);
        await using var db = new PostgresDbContext(new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connection).Options);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        await SyntheticTenantSession.SetAsync(db, userId);
        db.Users.Add(new UserRecord { Id = userId, Email = $"pattern-{userId:N}@example.invalid",
            Username = $"pt_{userId.ToString("N")[..IdentifierLength]}", CredentialType = "Password",
            PasswordHash = "synthetic-hash", CreatedAt = now, UpdatedAt = now });
        var planId = Guid.NewGuid();
        db.WorkoutPlans.Add(new WorkoutPlanRecord { Id = planId, UserId = userId, Name = "Synthetic",
            IsActive = true, CyclePhaseTarget = "Luteal", CreatedAt = now });
        await db.SaveChangesAsync();
        var anchors = new List<CyclePhaseLogRecord>();
        var checks = new List<DailyReadinessLogRecord>();
        for (var index = HistoricalCycleCount; index > 0; index--)
        {
            var cycleStart = today.AddDays(-index * CycleSpacingDays);
            var observationDate = cycleStart.AddDays(ObservationOffsetDays);
            var sessionStarted = DateTime.SpecifyKind(observationDate.ToDateTime(new TimeOnly(SessionStartHour, 0)), DateTimeKind.Utc);
            var cycleId = Guid.NewGuid();
            var dayId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            db.CycleLogs.Add(new CycleLogRecord { Id = cycleId, UserId = userId, StartDate = cycleStart, CreatedAt = sessionStarted.AddDays(-ObservationOffsetDays) });
            var anchor = new CyclePhaseLogRecord { Id = Guid.NewGuid(), UserId = userId, CycleLogId = cycleId,
                LocalDate = cycleStart, Phase = "Menstrual", LoggedAt = sessionStarted.AddDays(-ObservationOffsetDays), CreatedAt = sessionStarted.AddDays(-ObservationOffsetDays) };
            anchors.Add(anchor);
            db.CyclePhaseLogs.Add(anchor);
            db.CyclePhaseLogs.Add(new CyclePhaseLogRecord { Id = Guid.NewGuid(), UserId = userId, CycleLogId = cycleId,
                LocalDate = observationDate, Phase = "Luteal", LoggedAt = sessionStarted.AddHours(-CheckInLeadHours), CreatedAt = sessionStarted.AddHours(-CheckInLeadHours) });
            var check = new DailyReadinessLogRecord { Id = Guid.NewGuid(), UserId = userId, Date = observationDate,
                PlanningTimeZoneId = "UTC", PlanningRevision = InitialRevision, PlanningRecordedAtUtc = sessionStarted.AddHours(-CheckInLeadHours),
                PlanningSleepQuality = LowReadinessValue, Energy = LowReadinessValue, PlanningSoreness = HighSorenessAndStress,
                PlanningStress = HighSorenessAndStress, CreatedAt = sessionStarted.AddHours(-CheckInLeadHours) };
            checks.Add(check);
            db.DailyReadinessLogs.Add(check);
            db.WorkoutDays.Add(new WorkoutDayRecord { Id = dayId, UserId = userId, WorkoutPlanId = planId,
                Date = observationDate, DayOfWeek = (int)observationDate.DayOfWeek, WorkoutType = "Strength", Name = "Synthetic" });
            var snapshot = new PlannedPrescriptionSnapshot(dayId, "Synthetic", "Strength", null, null,
                [new PlannedExerciseSnapshot(Guid.NewGuid(), InitialRevision, SyntheticSets, 0, SyntheticRepetitions, null, false)]);
            db.WorkoutSessionLogs.Add(new WorkoutSessionLogRecord { Id = sessionId, UserId = userId, PlannedWorkoutDayId = dayId,
                PlannedLocalDate = observationDate, PlannedTimeZoneId = "UTC", StartedAt = sessionStarted,
                CompletedAt = sessionStarted.AddHours(CheckInLeadHours), PlannedPrescriptionSnapshotJson = snapshot.Serialize(),
                WorkoutName = "Synthetic", CreatedAt = sessionStarted, UpdatedAt = sessionStarted.AddHours(CheckInLeadHours) });
            db.WorkoutCompletionDayCredits.Add(new WorkoutCompletionDayCreditRecord { Id = Guid.NewGuid(), UserId = userId,
                LocalDate = observationDate, SourceSessionId = sessionId, TimeZoneId = "UTC", TimeZoneVersion = InitialRevision,
                AcceptedAtUtc = sessionStarted.AddHours(CheckInLeadHours) });
        }
        await db.SaveChangesAsync();
        var result = await PostgresCyclePersonalizationEvidence.LoadAsync(db, userId, today, "UTC", CancellationToken.None);
        Assert.Equal(HistoricalCycleCount, result.Count);
        Assert.All(result, item => Assert.Equal(CyclePhase.Luteal, item.Phase));
        var decision = CycleReadinessPrescriptionRule.Evaluate(new(CyclePhase.Luteal, PhaseReviewProvenance.UserConfirmed,
            null, null, false, [new CycleReadinessExercise(Guid.NewGuid(), SyntheticSets, 0, true, true)], result, today));
        Assert.Equal(CycleReadinessConfidence.Repeated, decision.Confidence);
        anchors[0].Phase = "Follicular";
        checks[1].PlanningRecordedAtUtc = checks[1].PlanningRecordedAtUtc!.Value.AddHours(CheckInLeadHours + InitialRevision);
        await db.SaveChangesAsync();
        var invalid = await PostgresCyclePersonalizationEvidence.LoadAsync(db, userId, today, "UTC", CancellationToken.None);
        Assert.Single(invalid);
    }
}
