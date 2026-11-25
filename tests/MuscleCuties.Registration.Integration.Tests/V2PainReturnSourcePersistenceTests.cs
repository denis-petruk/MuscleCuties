using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning.V2;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Session;
using Npgsql;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class V2PainReturnSourcePersistenceTests
{
    private const string ConnectionVariable = "MC_REGISTRATION_WRITE_TEST_POSTGRES";
    private const string AccountTimeZone = "UTC";
    private const int PrescribedWorkingSets = 2;
    private const int PrescribedRepetitions = 8;
    private const decimal BaselineLoadKg = 80m;
    private const decimal ConservativeLoadKg = 70m;

    [PostgresFact]
    public async Task ConservativeReturnCountsOnlyDistinctCompletedObservedImmutableSessionsAndSurvivesReads()
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionVariable);
        Xunit.Assert.False(string.IsNullOrWhiteSpace(connection),
            $"Set {ConnectionVariable} to a disposable local PostgreSQL test database.");
        var builder = new NpgsqlConnectionStringBuilder(connection);
        Xunit.Assert.Equal("127.0.0.1", builder.Host);
        Xunit.Assert.StartsWith("mc_test_", builder.Database, StringComparison.Ordinal);

        var options = new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connection).Options;
        await using var db = new PostgresDbContext(options);
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var reportAt = now.AddHours(-18);
        var clearedAt = now.AddHours(-3);
        var reportDate = DateOnly.FromDateTime(reportAt);
        var clearDate = DateOnly.FromDateTime(clearedAt);
        var injuryId = Guid.NewGuid();
        var episodeId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        await SyntheticTenantSession.SetAsync(db, userId);

        db.Users.Add(new UserRecord
        {
            Id = userId,
            Email = $"pain-return-{userId:N}@example.invalid",
            Username = $"pain_{userId.ToString("N")[..10]}",
            CredentialType = "Password",
            PasswordHash = "synthetic-hash",
            CreatedAt = now,
            UpdatedAt = now,
            IsOnboardingComplete = true
        });
        db.WorkoutAccountPlanningStates.Add(new WorkoutAccountPlanningStateRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TimeZoneId = AccountTimeZone
        });
        db.WorkoutPlans.Add(new WorkoutPlanRecord
        {
            Id = planId,
            UserId = userId,
            Name = "Synthetic pain return plan",
            IsActive = true,
            CreatedAt = now
        });

        var definitions = await db.WorkoutExerciseDefinitions.AsNoTracking().ToListAsync();
        var target = definitions
            .Where(definition => (definition.Contraindications & InjuryFlag.Knee) != InjuryFlag.None)
            .OrderBy(definition => definition.Id)
            .First();
        var baselineSessionId = Guid.NewGuid();
        db.WorkoutSessionLogs.Add(new WorkoutSessionLogRecord
        {
            Id = baselineSessionId,
            UserId = userId,
            WorkoutName = "Synthetic pre-report baseline",
            StartedAt = reportAt.AddMinutes(-30),
            CompletedAt = reportAt.AddMinutes(-15),
            CreatedAt = reportAt.AddMinutes(-30),
            UpdatedAt = reportAt.AddMinutes(-15)
        });
        db.ExerciseSetLogs.Add(new ExerciseSetLogRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            WorkoutSessionLogId = baselineSessionId,
            ExerciseId = target.Id.ToString(CultureInfo.InvariantCulture),
            ExerciseName = target.Name,
            SetNumber = 1,
            RepsCompleted = PrescribedRepetitions,
            WeightKg = BaselineLoadKg,
            LeftSideReps = target.UnilateralDoublesTime ? PrescribedRepetitions : null,
            RightSideReps = target.UnilateralDoublesTime ? PrescribedRepetitions : null,
            IsObserved = true,
            CreatedAt = reportAt.AddMinutes(-15),
            UpdatedAt = reportAt.AddMinutes(-15)
        });
        db.WorkoutInjuryLogs.Add(new WorkoutInjuryLogRecord
        {
            Id = injuryId,
            UserId = userId,
            SiteFlag = (int)InjuryFlag.Knee,
            Status = "Cleared",
            Since = reportDate,
            Pain = 1,
            WorseningTrigger = nameof(MuscleCuties.Core.Domain.Workout.InjuryWorseningTrigger.NotAnswered),
            ClinicianAdvisedAvoid = true,
            Date = reportDate,
            ClearedDate = clearDate,
            ReportedAtUtc = reportAt,
            ClearedAtUtc = clearedAt,
            CreatedAt = reportAt
        });
        db.WorkoutInjuryEpisodes.Add(new WorkoutInjuryEpisodeRecord
        {
            Id = episodeId,
            UserId = userId,
            InjuryLogId = injuryId,
            SiteFlag = (int)InjuryFlag.Knee,
            StartedOn = reportDate,
            ClearedOn = clearDate,
            ClearanceTimeZoneId = AccountTimeZone,
            ClearanceTimeZoneVersion = 1
        });

        var firstDayId = Guid.NewGuid();
        var secondDayId = Guid.NewGuid();
        var duplicateDayId = firstDayId;
        var incompleteDayId = Guid.NewGuid();
        var unobservedDayId = Guid.NewGuid();
        var warmupDayId = Guid.NewGuid();
        var deletedDayId = Guid.NewGuid();
        var mismatchedDayId = Guid.NewGuid();
        var dayIds = new[] { firstDayId, secondDayId, incompleteDayId, unobservedDayId, warmupDayId,
            deletedDayId, mismatchedDayId };
        foreach (var dayId in dayIds)
        {
            var date = clearDate;
            db.WorkoutDays.Add(new WorkoutDayRecord
            {
                Id = dayId,
                UserId = userId,
                WorkoutPlanId = planId,
                Date = date,
                DayOfWeek = (int)date.DayOfWeek,
                WorkoutType = "Strength",
                Name = "Synthetic conservative return"
            });
        }
        await db.SaveChangesAsync();

        async Task<PainRestrictionV2> LoadReturnRestrictionAsync()
        {
            db.ChangeTracker.Clear();
            var observed = await V2ObservedHistory.LoadAsync(db, userId, AccountTimeZone, CancellationToken.None);
            var restrictions = await V2PainReturnSource.LoadAsync(db, userId, observed, CancellationToken.None);
            return Xunit.Assert.Single(restrictions, restriction => restriction.ExerciseId == target.Id);
        }

        async Task<int> CompletedConservativeSessionsAsync() =>
            (await LoadReturnRestrictionAsync()).ConservativeReturnSessionsCompleted;

        async Task AddSessionAsync(Guid dayId, DateTime completedAt, bool observed, bool warmup,
            bool deleted, int setCount, Guid? prescriptionDayId = null)
        {
            var sessionId = Guid.NewGuid();
            var snapshot = new PlannedPrescriptionSnapshot(prescriptionDayId ?? dayId,
                "Synthetic conservative return", "Strength",
                (int)WorkoutActivityType.GeneralStrength, null,
                [new PlannedExerciseSnapshot(Guid.NewGuid(), target.Id, PrescribedWorkingSets, 0,
                    PrescribedRepetitions, null, false)]);
            db.WorkoutSessionLogs.Add(new WorkoutSessionLogRecord
            {
                Id = sessionId,
                UserId = userId,
                PlannedWorkoutDayId = dayId,
                WorkoutName = "Synthetic conservative return",
                StartedAt = completedAt.AddMinutes(-30),
                CompletedAt = completedAt,
                IsDeleted = deleted,
                PlannedPrescriptionSnapshotJson = snapshot.Serialize(),
                CreatedAt = completedAt.AddMinutes(-30),
                UpdatedAt = completedAt
            });
            for (var setNumber = 1; setNumber <= setCount; setNumber++)
            {
                db.ExerciseSetLogs.Add(new ExerciseSetLogRecord
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    WorkoutSessionLogId = sessionId,
                    ExerciseId = target.Id.ToString(CultureInfo.InvariantCulture),
                    ExerciseName = target.Name,
                    SetNumber = setNumber,
                    RepsCompleted = PrescribedRepetitions,
                    WeightKg = ConservativeLoadKg,
                    LeftSideReps = target.UnilateralDoublesTime ? PrescribedRepetitions : null,
                    RightSideReps = target.UnilateralDoublesTime ? PrescribedRepetitions : null,
                    IsObserved = observed,
                    IsWarmup = warmup,
                    CreatedAt = completedAt,
                    UpdatedAt = completedAt
                });
            }
            await db.SaveChangesAsync();
        }

        try
        {
            var initialReturn = await LoadReturnRestrictionAsync();
            Xunit.Assert.Equal(0, initialReturn.ConservativeReturnSessionsCompleted);
            Xunit.Assert.Equal((decimal?)BaselineLoadKg, initialReturn.ReturnBaselineLoadKg);
            var immediateReturn = WorkoutProgressionAndRecoveryV2.EvaluatePainReturn(initialReturn,
                new DateTimeOffset(now, TimeSpan.Zero), BaselineLoadKg, 2.5m);
            Xunit.Assert.False(immediateReturn.ReassessmentDue);
            Xunit.Assert.True(immediateReturn.CanReturnWithConservativeLoad);
            Xunit.Assert.Equal((decimal?)ConservativeLoadKg, immediateReturn.ConservativeLoadKg);

            await AddSessionAsync(firstDayId, clearedAt.AddMinutes(10), observed: true, warmup: false,
                deleted: false, setCount: PrescribedWorkingSets);
            var oneCompletedReturn = await LoadReturnRestrictionAsync();
            Xunit.Assert.Equal(1, oneCompletedReturn.ConservativeReturnSessionsCompleted);
            Xunit.Assert.Equal(1, await CompletedConservativeSessionsAsync());

            // A second session ID for the same immutable planned day is a duplicate completion.
            await AddSessionAsync(duplicateDayId, clearedAt.AddMinutes(20), observed: true, warmup: false,
                deleted: false, setCount: PrescribedWorkingSets);
            await AddSessionAsync(incompleteDayId, clearedAt.AddMinutes(30), observed: true, warmup: false,
                deleted: false, setCount: 1);
            await AddSessionAsync(unobservedDayId, clearedAt.AddMinutes(40), observed: false, warmup: false,
                deleted: false, setCount: PrescribedWorkingSets);
            await AddSessionAsync(warmupDayId, clearedAt.AddMinutes(50), observed: true, warmup: true,
                deleted: false, setCount: PrescribedWorkingSets);
            await AddSessionAsync(deletedDayId, clearedAt.AddMinutes(60), observed: true, warmup: false,
                deleted: true, setCount: PrescribedWorkingSets);
            await AddSessionAsync(mismatchedDayId, clearedAt.AddMinutes(65), observed: true, warmup: false,
                deleted: false, setCount: PrescribedWorkingSets, prescriptionDayId: firstDayId);
            Xunit.Assert.Equal(1, await CompletedConservativeSessionsAsync());
            Xunit.Assert.Equal(1, await CompletedConservativeSessionsAsync());

            await AddSessionAsync(secondDayId, clearedAt.AddMinutes(70), observed: true, warmup: false,
                deleted: false, setCount: PrescribedWorkingSets);
            var twoCompletedReturns = await LoadReturnRestrictionAsync();
            Xunit.Assert.Equal(2, twoCompletedReturns.ConservativeReturnSessionsCompleted);
            Xunit.Assert.Equal(2, await CompletedConservativeSessionsAsync());
            var normalReturn = WorkoutProgressionAndRecoveryV2.EvaluatePainReturn(twoCompletedReturns,
                new DateTimeOffset(now, TimeSpan.Zero), BaselineLoadKg, 2.5m);
            Xunit.Assert.False(normalReturn.CanReturnWithConservativeLoad);
            Xunit.Assert.Equal(0, normalReturn.RemainingConservativeSessions);

            var activeInjuryId = Guid.NewGuid();
            db.WorkoutInjuryLogs.Add(new WorkoutInjuryLogRecord
            {
                Id = activeInjuryId,
                UserId = userId,
                SiteFlag = (int)InjuryFlag.Shoulder,
                Status = "Acute",
                Since = clearDate,
                Pain = 2,
                WorseningTrigger = nameof(MuscleCuties.Core.Domain.Workout.InjuryWorseningTrigger.NotAnswered),
                Date = clearDate,
                ReportedAtUtc = null,
                CreatedAt = now
            });
            await db.SaveChangesAsync();
            var observed = await V2ObservedHistory.LoadAsync(db, userId, AccountTimeZone, CancellationToken.None);
            var restrictions = await V2PainReturnSource.LoadAsync(db, userId, observed, CancellationToken.None);
            var noTimestamp = Xunit.Assert.Single(restrictions, restriction =>
                restriction.SiteFlag == InjuryFlag.Shoulder && !restriction.IsResolved);
            Xunit.Assert.Null(noTimestamp.ReportedAtUtc);
            Xunit.Assert.False(noTimestamp.IsResolved);
            var eligibility = WorkoutProgressionAndRecoveryV2.EvaluatePainReturn(noTimestamp,
                new DateTimeOffset(now, TimeSpan.Zero), BaselineLoadKg, 2.5m);
            Xunit.Assert.False(eligibility.ReassessmentDue);
            Xunit.Assert.False(eligibility.CanReturnWithConservativeLoad);
        }
        finally
        {
            await db.Users.Where(user => user.Id == userId).ExecuteDeleteAsync();
        }
    }
}
