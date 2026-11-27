using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Session;
using Npgsql;
using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class SnapshotProgressionHistoryIntegrationTests
{
    [PostgresFact]
    public async Task V2_history_withholds_wrong_catalog_name_and_does_not_fall_back_to_older_load()
    {
        var connection = PostgresTestEnvironment.RequireConnection("MC_REGISTRATION_WRITE_TEST_POSTGRES");
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        Assert.Equal("127.0.0.1", parsed.Host);
        Assert.StartsWith("mc_test_", parsed.Database, StringComparison.Ordinal);
        await using var db = new PostgresDbContext(new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connection).Options);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = Guid.NewGuid();
        await SyntheticTenantSession.SetAsync(db, user);
        var now = DateTime.UtcNow;
        db.Users.Add(new UserRecord
        {
            Id = user,
            Email = $"catalog-history-{user:N}@example.invalid",
            Username = $"cat_{user:N}",
            CredentialType = "Password",
            PasswordHash = "synthetic-hash",
            CreatedAt = now,
            UpdatedAt = now
        });
        var definitions = await db.WorkoutExerciseDefinitions.AsNoTracking()
            .Where(row => row.SourceExerciseId != null).OrderBy(row => row.Id).Take(2).ToListAsync();
        Assert.Equal(2, definitions.Count);
        var exercise = definitions[0];
        var plan = Guid.NewGuid();
        db.WorkoutPlans.Add(new WorkoutPlanRecord
        {
            Id = plan,
            UserId = user,
            Name = "Synthetic",
            IsActive = true,
            CyclePhaseTarget = "Follicular",
            CreatedAt = now
        });

        foreach (var (age, savedName) in new[] { (2, exercise.Name), (1, definitions[1].Name) })
        {
            var began = now.AddDays(-age);
            var date = DateOnly.FromDateTime(began);
            var day = Guid.NewGuid();
            var session = Guid.NewGuid();
            db.WorkoutDays.Add(new WorkoutDayRecord
            {
                Id = day,
                UserId = user,
                WorkoutPlanId = plan,
                Date = date,
                DayOfWeek = (int)date.DayOfWeek,
                WorkoutType = "Strength",
                Name = "Synthetic"
            });
            var slot = new PlannedExerciseSnapshot(Guid.NewGuid(), exercise.Id, 2, 0, 8, null, false)
            { PlanningSlotId = "v2-synthetic" };
            db.WorkoutSessionLogs.Add(new WorkoutSessionLogRecord
            {
                Id = session,
                UserId = user,
                PlannedWorkoutDayId = day,
                WorkoutName = "Synthetic",
                StartedAt = began,
                CompletedAt = began.AddMinutes(30),
                CreatedAt = began,
                UpdatedAt = began,
                PlannedPrescriptionSnapshotJson = new PlannedPrescriptionSnapshot(day, "Synthetic", "Strength",
                    null, null, [slot]).Serialize()
            });
            for (var number = 1; number <= 2; number++)
            {
                db.ExerciseSetLogs.Add(new ExerciseSetLogRecord
                {
                    Id = Guid.NewGuid(),
                    UserId = user,
                    WorkoutSessionLogId = session,
                    ExerciseId = exercise.Id.ToString(),
                    ExerciseName = savedName,
                    SetNumber = number,
                    WeightKg = 50m,
                    RepsCompleted = 8,
                    IsObserved = true,
                    CreatedAt = began,
                    UpdatedAt = began
                });
            }
        }
        await db.SaveChangesAsync();

        Assert.Empty(await V2ObservedHistory.LoadAsync(db, user, "UTC", CancellationToken.None));
        var snapshots = await PostgresSnapshotProgressionHistory.LoadAsync(db, user, [exercise.Id], null, CancellationToken.None);
        Assert.Empty(Assert.Single(snapshots[exercise.Id]).Sets);
        Assert.Equal(4, await db.ExerciseSetLogs.CountAsync(row => row.UserId == user));
    }

    [PostgresFact]
    public async Task Completed_snapshots_allow_optional_rir_but_partial_history_blocks_increase()
    {
        var connection = Environment.GetEnvironmentVariable("MC_REGISTRATION_WRITE_TEST_POSTGRES");
        Assert.False(string.IsNullOrWhiteSpace(connection));
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        Assert.Equal("127.0.0.1", parsed.Host);
        Assert.StartsWith("mc_test_", parsed.Database, StringComparison.Ordinal);
        await using var db = new PostgresDbContext(new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connection).Options);
        await using var tx = await db.Database.BeginTransactionAsync();
        var user = Guid.NewGuid();
        await SyntheticTenantSession.SetAsync(db, user);
        var now = DateTime.UtcNow;
        db.Users.Add(new UserRecord { Id = user, Email = $"snapshot-{user:N}@example.invalid", Username = $"snap_{user.ToString("N")[..10]}", CredentialType = "Password", PasswordHash = "synthetic-hash", CreatedAt = now, UpdatedAt = now });
        var plan = Guid.NewGuid();
        db.WorkoutPlans.Add(new WorkoutPlanRecord { Id = plan, UserId = user, Name = "Synthetic", IsActive = true, CyclePhaseTarget = "Follicular", CreatedAt = now });
        await db.SaveChangesAsync();
        var catalog = await db.Exercises.AsNoTracking().OrderBy(e => e.Id).ToListAsync();
        var exercise = catalog.First(e => !new[] { "plank", "bike", "run", "jog", "swim", "walk", "cycle", "interval", "yoga", "mobility" }.Any(token => e.Name.Contains(token, StringComparison.OrdinalIgnoreCase))).Id;
        async Task AddStrength(int daysAgo, int setCount, bool completed)
        {
            var dayId = Guid.NewGuid();
            var day = DateOnly.FromDateTime(now.AddDays(-daysAgo));
            db.WorkoutDays.Add(new WorkoutDayRecord { Id = dayId, UserId = user, WorkoutPlanId = plan, Date = day, DayOfWeek = (int)day.DayOfWeek, WorkoutType = "Strength", Name = "Synthetic" });
            var slot = new WorkoutDayExerciseRecord { Id = Guid.NewGuid(), UserId = user, WorkoutDayId = dayId, ExerciseId = exercise, Sets = 2, Reps = 8 };
            db.WorkoutDayExercises.Add(slot);
            var id = Guid.NewGuid();
            var started = now.AddDays(-daysAgo);
            var snapshot = new PlannedPrescriptionSnapshot(dayId, "Synthetic", "Strength", null, null, [PlannedExerciseSnapshot.FromRecord(slot)]).Serialize();
            db.WorkoutSessionLogs.Add(new WorkoutSessionLogRecord { Id = id, UserId = user, PlannedWorkoutDayId = dayId, WorkoutName = "Synthetic", StartedAt = started, CompletedAt = completed ? started.AddMinutes(30) : null, CreatedAt = started, UpdatedAt = started, PlannedPrescriptionSnapshotJson = snapshot });
            for (var i = 1; i <= setCount; i++) db.ExerciseSetLogs.Add(new ExerciseSetLogRecord { Id = Guid.NewGuid(), UserId = user, WorkoutSessionLogId = id, ExerciseId = exercise.ToString(), ExerciseName = "Synthetic", SetNumber = i, WeightKg = 60m, RepsCompleted = 8, RirActual = null, IsObserved = true, CreatedAt = started, UpdatedAt = started });
            await db.SaveChangesAsync();
        }
        await AddStrength(4, 2, true);
        await AddStrength(3, 2, true);
        await AddStrength(1, 1, false);
        db.WorkoutSessionLogs.Add(new WorkoutSessionLogRecord { Id = Guid.NewGuid(), UserId = user, UnplannedActivityType = (int)WorkoutActivityType.Running, WorkoutName = "Synthetic run", StartedAt = now.AddDays(-2), CompletedAt = now.AddDays(-2).AddMinutes(10), CreatedAt = now.AddDays(-2), UpdatedAt = now.AddDays(-2) });
        await db.SaveChangesAsync();
        var history = await PostgresSnapshotProgressionHistory.LoadAsync(db, user, [exercise], null, CancellationToken.None);
        var advice = SnapshotProgressionRule.Resolve(MajorMuscleGroup.Quads, EffectiveExperienceTier.Tier2IntermediateOrEstablished, exercise, 8, 60m, history[exercise]);
        Assert.True(advice.LoadIncreased);
        // A performed substitute absent from the immutable snapshot is an unknown exposure,
        // not permission to use two older successes.
        var unknownDay = await db.WorkoutDays.Where(d => d.UserId == user).FirstAsync();
        var otherExercise = catalog.First(e => e.Id != exercise).Id;
        var unknownSession = new WorkoutSessionLogRecord
        {
            Id = Guid.NewGuid(),
            UserId = user,
            PlannedWorkoutDayId = unknownDay.Id,
            WorkoutName = "Synthetic substitute",
            StartedAt = now.AddHours(-6),
            CompletedAt = now.AddHours(-5),
            CreatedAt = now.AddHours(-6),
            UpdatedAt = now.AddHours(-5),
            PlannedPrescriptionSnapshotJson = new PlannedPrescriptionSnapshot(unknownDay.Id, "Synthetic", "Strength", null, null,
                [new PlannedExerciseSnapshot(Guid.NewGuid(), otherExercise, 2, 0, 8, null, false)]).Serialize()
        };
        db.WorkoutSessionLogs.Add(unknownSession);
        db.ExerciseSetLogs.Add(new ExerciseSetLogRecord
        {
            Id = Guid.NewGuid(),
            UserId = user,
            WorkoutSessionLogId = unknownSession.Id,
            ExerciseId = exercise.ToString(),
            ExerciseName = "Synthetic substitute",
            SetNumber = 1,
            WeightKg = 60m,
            RepsCompleted = 4,
            IsObserved = true,
            CreatedAt = now.AddHours(-6),
            UpdatedAt = now.AddHours(-5)
        });
        await db.SaveChangesAsync();
        history = await PostgresSnapshotProgressionHistory.LoadAsync(db, user, [exercise], null, CancellationToken.None);
        advice = SnapshotProgressionRule.Resolve(MajorMuscleGroup.Quads, EffectiveExperienceTier.Tier2IntermediateOrEstablished, exercise, 8, 60m, history[exercise]);
        Assert.False(advice.LoadIncreased);
        unknownSession.IsDeleted = true;
        await db.SaveChangesAsync();
        await AddStrength(1, 1, true);
        history = await PostgresSnapshotProgressionHistory.LoadAsync(db, user, [exercise], null, CancellationToken.None);
        advice = SnapshotProgressionRule.Resolve(MajorMuscleGroup.Quads, EffectiveExperienceTier.Tier2IntermediateOrEstablished, exercise, 8, 60m, history[exercise]);
        Assert.False(advice.LoadIncreased);
        // Exercise the session-detail route with a real prior workout log, not only the reader.
        var partial = await db.WorkoutSessionLogs.SingleAsync(s => s.UserId == user && !s.IsDeleted && s.CompletedAt != null && s.PlannedWorkoutDayId != null && s.StartedAt > now.AddDays(-2));
        partial.IsDeleted = true;
        var prior = await db.WorkoutSessionLogs.Where(s => s.UserId == user && !s.IsDeleted && s.CompletedAt != null && s.PlannedWorkoutDayId != null && s.Id != partial.Id)
            .OrderByDescending(s => s.CompletedAt).FirstAsync();
        var priorSnapshot = PlannedPrescriptionSnapshot.Deserialize(prior.PlannedPrescriptionSnapshotJson)!;
        var priorSlot = priorSnapshot.Exercises.Single();
        var previousLogId = Guid.NewGuid();
        db.WorkoutLogs.Add(new WorkoutLogRecord
        {
            Id = previousLogId,
            UserId = user,
            WorkoutDayId = priorSnapshot.DayId,
            PlannedSessionId = prior.Id,
            Date = DateOnly.FromDateTime(prior.StartedAt),
            CreatedAt = prior.StartedAt
        });
        db.WorkoutExerciseLogs.Add(new WorkoutExerciseLogRecord
        {
            Id = Guid.NewGuid(),
            UserId = user,
            WorkoutLogId = previousLogId,
            WorkoutDayExerciseId = priorSlot.Id,
            ExerciseId = exercise,
            CompletedSets = 2,
            CompletedReps = 8,
            WeightKg = 60m,
            CreatedAt = prior.StartedAt
        });
        var currentDayId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(now);
        db.WorkoutDays.Add(new WorkoutDayRecord
        {
            Id = currentDayId,
            UserId = user,
            WorkoutPlanId = plan,
            Date = today,
            DayOfWeek = (int)today.DayOfWeek,
            WorkoutType = "Strength",
            Name = "Synthetic current"
        });
        db.WorkoutDayExercises.Add(new WorkoutDayExerciseRecord
        {
            Id = Guid.NewGuid(),
            UserId = user,
            WorkoutDayId = currentDayId,
            ExerciseId = exercise,
            Sets = 2,
            Reps = 8
        });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        try
        {
            var store = new PostgresWorkoutPlanAndSessionStore(db);
            var detail = await store.GetDetailAsync(user, PostgresWorkoutPlanAndSessionStore.ToStableWorkoutIntId(currentDayId));
            Assert.Equal(StrengthProgressionAdviceRule.EarnedCode, Assert.Single(detail.Exercises).RecommendationCode);
            db.ChangeTracker.Clear();
            var restoredPartial = await db.WorkoutSessionLogs.SingleAsync(s => s.Id == partial.Id && s.UserId == user);
            restoredPartial.IsDeleted = false;
            await db.SaveChangesAsync();
            detail = await store.GetDetailAsync(user, PostgresWorkoutPlanAndSessionStore.ToStableWorkoutIntId(currentDayId));
            Assert.NotEqual(StrengthProgressionAdviceRule.EarnedCode, Assert.Single(detail.Exercises).RecommendationCode);
        }
        finally
        {
            await db.Users.Where(u => u.Id == user).ExecuteDeleteAsync();
        }
    }
}
