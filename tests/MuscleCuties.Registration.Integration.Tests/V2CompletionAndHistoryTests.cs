using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Dashboard;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Nutrition.Targets;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.ActiveSession;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Session;
using MuscleCuties.UseCases.Features.Workout.ActiveSession;
using MuscleCuties.UseCases.Features.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Overview.Contracts;
using MuscleCuties.UseCases.Features.Workout.Session.Contracts;
using Npgsql;
using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class V2CompletionAndHistoryTests
{
    [PostgresFact]
    public async Task S18_partial_observed_sets_resume_from_a_fresh_database_context()
    {
        await using var fixture = await Fixture.CreateAsync();
        var row = new ActiveExerciseSetInput(Guid.NewGuid(), fixture.ExerciseId.ToString(CultureInfo.InvariantCulture),
            "Synthetic observed movement", 1, 25m, 9, null, false, 90);
        await fixture.Sessions.UpsertSetAsync(fixture.UserId,
            new UpsertWorkoutSessionSetCommand("save-partial", fixture.SessionId, row), CancellationToken.None);
        await using var fresh = new PostgresDbContext(fixture.Options);
        await SyntheticTenantSession.SetAsync(fresh, fixture.UserId);
        var sessions = new PostgresWorkoutActiveSessionStore(fresh, new PostgresIdempotencyExecutor(), TimeProvider.System);
        var resumed = await sessions.GetSessionAsync(fixture.UserId, fixture.SessionId, CancellationToken.None);
        Assert.NotNull(resumed);
        Assert.False(resumed.IsCompleted);
        var restored = Assert.Single(resumed.Sets);
        Assert.Equal(row.SetId, restored.SetId);
        Assert.Equal(9, restored.RepsCompleted);
        Assert.Equal(25m, restored.WeightKg);
        Assert.Null(restored.Rpe);
        Assert.Empty(await fresh.WorkoutCompletionDayCredits.Where(item => item.UserId == fixture.UserId).ToListAsync());
    }

    [PostgresFact]
    public async Task S18_Today_resume_uses_the_actual_planned_day_identifier()
    {
        await using var fixture = await Fixture.CreateAsync();
        var today = await new PostgresDashboardOverviewStore(fixture.Db,
            new PostgresNutritionTargetProjectionStore(fixture.Db), TimeProvider.System)
            .LoadOverviewAsync(fixture.UserId, fixture.Today, "UTC");
        Assert.Equal("In Progress", today.WorkoutSubtitle);
        Assert.Equal(fixture.StableDayId, today.WorkoutDayId);
        Assert.NotNull(await fixture.Sessions.GetPlannedSessionAsync(fixture.UserId, today.WorkoutDayId, CancellationToken.None));
    }

    [PostgresFact]
    public async Task S19_duplicate_completion_has_one_log_one_day_credit_and_matching_Today_Train_terminal_state()
    {
        await using var fixture = await Fixture.CreateAsync();
        var workout = new PostgresWorkoutPlanAndSessionStore(fixture.Db);
        var log = new LogWorkoutSessionRequest(fixture.StableDayId, fixture.Today,
            [new WorkoutExerciseLogDto(PostgresWorkoutPlanAndSessionStore.ToStableWorkoutIntId(fixture.ExerciseRowId),
                fixture.ExerciseId, 2, 10, 25f, ObservedSets: [new(1, 10, 25m), new(2, 10, 25m)])],
            fixture.SessionId);
        await workout.LogAsync(fixture.UserId, log, CancellationToken.None);
        await workout.LogAsync(fixture.UserId, log, CancellationToken.None);
        var finish = new FinishWorkoutSessionCommand("finish", fixture.SessionId, fixture.Now, 60, null, null);
        var completed = await fixture.Sessions.FinishSessionAsync(fixture.UserId, finish, CancellationToken.None);
        var replay = await fixture.Sessions.FinishSessionAsync(fixture.UserId, finish, CancellationToken.None);
        var anotherKey = await fixture.Sessions.FinishSessionAsync(fixture.UserId,
            finish with { IdempotencyKey = "finish-again" }, CancellationToken.None);
        Assert.NotNull(completed);
        Assert.True(completed.IsCompleted);
        Assert.Equal(JsonSerializer.Serialize(completed with { Replayed = false }),
            JsonSerializer.Serialize(replay! with { Replayed = false }));
        Assert.Equal(completed.CompletedAtUtc, anotherKey!.CompletedAtUtc);
        Assert.True(anotherKey.IsCompleted);
        Assert.Single(await fixture.Db.WorkoutLogs.AsNoTracking().Where(item => item.UserId == fixture.UserId).ToListAsync());
        var credit = Assert.Single(await fixture.Db.WorkoutCompletionDayCredits.AsNoTracking()
            .Where(item => item.UserId == fixture.UserId).ToListAsync());
        Assert.Equal(fixture.Today, credit.LocalDate);
        var train = await workout.LoadOverviewAsync(fixture.UserId, new WorkoutOverviewRequest(fixture.Today));
        Assert.True(Assert.Single(train.Workouts, item => item.WorkoutDayId == fixture.StableDayId).IsCompleted);
        var today = await new PostgresDashboardOverviewStore(fixture.Db,
            new PostgresNutritionTargetProjectionStore(fixture.Db), TimeProvider.System)
            .LoadOverviewAsync(fixture.UserId, fixture.Today, "UTC");
        Assert.Equal("Completed Today", today.WorkoutSubtitle);
        Assert.Equal(fixture.StableDayId, today.WorkoutDayId);
        Assert.StartsWith("Completed (", today.SessionProgressText, StringComparison.Ordinal);
    }

    [PostgresFact]
    public async Task Durable_completed_days_promote_and_detect_a_29_day_break_without_creating_credits()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Db.WorkoutCompletionDayCredits.AddRange(Enumerable.Range(0, 20).Select(index =>
            new WorkoutCompletionDayCreditRecord { Id = Guid.NewGuid(), UserId = fixture.UserId,
                LocalDate = fixture.Today.AddDays(-29 - index), AcceptedAtUtc = fixture.Now.AddDays(-29 - index),
                TimeZoneId = "UTC" }));
        await fixture.Db.SaveChangesAsync();
        var source = await new PostgresStrengthPlanPreviewSourceV020(fixture.Db).LoadAsync(fixture.UserId, CancellationToken.None);
        Assert.NotNull(source);
        Assert.Equal(20, source.DistinctCompletedWorkoutDays);
        Assert.True(source.IsReturningAfterDetraining);
        Assert.Empty(source.PriorObservedSets);
        Assert.Equal(20, await fixture.Db.WorkoutCompletionDayCredits.CountAsync(item => item.UserId == fixture.UserId));
    }

    [PostgresTheory]
    [InlineData(28, false)]
    [InlineData(29, true)]
    public async Task Established_observed_history_detects_detraining_without_inventing_day_credits(int gapDays, bool expectedReturning)
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Db.UserProfiles.Where(row => row.UserId == fixture.UserId)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.TrainingExperienceLevel, "Advanced"));
        await fixture.Db.WorkoutAccountPlanningStates.Where(row => row.UserId == fixture.UserId)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.EffectiveExperience, TrainingExperienceLevel.Advanced));
        var completedAt = fixture.Now.AddDays(-gapDays);
        await fixture.Db.WorkoutSessionLogs.Where(row => row.Id == fixture.SessionId)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.CompletedAt, completedAt)
                .SetProperty(row => row.StartedAt, completedAt.AddMinutes(-30)));
        fixture.Db.ExerciseSetLogs.Add(new ExerciseSetLogRecord { Id = Guid.NewGuid(), UserId = fixture.UserId,
            WorkoutSessionLogId = fixture.SessionId, ExerciseId = fixture.ExerciseId.ToString(CultureInfo.InvariantCulture),
            ExerciseName = "Synthetic historical observation", IsObserved = true, SetNumber = 1,
            RepsCompleted = 10, WeightKg = 25m, CreatedAt = completedAt, UpdatedAt = completedAt });
        await fixture.Db.SaveChangesAsync();
        var source = await new PostgresStrengthPlanPreviewSourceV020(fixture.Db).LoadAsync(fixture.UserId, CancellationToken.None);
        Assert.NotNull(source);
        Assert.Equal(expectedReturning, source.IsReturningAfterDetraining);
        Assert.Equal(0, source.DistinctCompletedWorkoutDays);
        Assert.Single(source.PriorObservedSets);
        Assert.Empty(await fixture.Db.WorkoutCompletionDayCredits.Where(row => row.UserId == fixture.UserId).ToListAsync());
    }

    [PostgresFact]
    public async Task S04_real_durable_ledger_promotes_beginner_to_advanced_and_generates_five_days_from_reviewed_catalog()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Db.WorkoutCompletionDayCredits.AddRange(Enumerable.Range(0, 130).Select(index =>
            new WorkoutCompletionDayCreditRecord { Id = Guid.NewGuid(), UserId = fixture.UserId,
                LocalDate = fixture.Today.AddDays(-index), AcceptedAtUtc = fixture.Now.AddDays(-index), TimeZoneId = "UTC" }));
        await fixture.Db.SaveChangesAsync();
        var source = await new PostgresStrengthPlanPreviewSourceV020(fixture.Db).LoadAsync(fixture.UserId, CancellationToken.None);
        Assert.NotNull(source);
        var request = new StrengthPlanPreviewRequestV020(WorkoutActivityDto.GeneralStrength, fixture.Today, null)
        {
            AvailableWeekdays = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            ActivitySessionDurationsMinutes = new Dictionary<int, int> { [(int)WorkoutActivityType.GeneralStrength] = 90 }
        };
        var result = WorkoutGenerationV2Service.GenerateFromPreviewSource(request, source);
        Assert.Equal(130, source.DistinctCompletedWorkoutDays);
        Assert.False(source.IsReturningAfterDetraining);
        Assert.Contains(result.RuleTrace, trace => trace.StartsWith("Experience:Advanced;", StringComparison.Ordinal));
        Assert.Equal(5, result.AdaptedSessions.Count);
        Assert.All(result.AdaptedSessions, session => Assert.NotEmpty(session.Exercises));
    }

    [PostgresTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task V2_session_preserves_prescription_order_and_does_not_classify_crunch_as_running(bool verifyOrder)
    {
        await using var fixture = await Fixture.CreateAsync();
        var crunch = await fixture.Db.Exercises.AsNoTracking().FirstAsync(exercise => exercise.Name == "Cable Crunch");
        var hip = await fixture.Db.Exercises.AsNoTracking().SingleAsync(exercise => exercise.Id == fixture.ExerciseId);
        var crunchRow = Guid.NewGuid();
        fixture.Db.WorkoutDayExercises.Add(new WorkoutDayExerciseRecord { Id = crunchRow, UserId = fixture.UserId,
            WorkoutDayId = fixture.DayId, ExerciseId = crunch.Id, Sets = 2, Reps = 10 });
        var generation = new GenerationSessionV2(0, fixture.Today.DayOfWeek, fixture.Today,
            (int)WorkoutActivityType.GeneralStrength, 6, 30m,
            [new GenerationExerciseV2(0, hip.Id, hip.Name, 1, 2, 8, 12, 3, 120, "CalibrationRequired", null, null, false, "InitialLoadCalibrationRequired"),
             new GenerationExerciseV2(1, crunch.Id, crunch.Name, 0, 2, 10, 15, 3, 90, "CalibrationRequired", null, null, false, "InitialLoadCalibrationRequired")]);
        // The legacy row order differs from the accepted V2 order, as happens after unordered DB reads.
        var snapshot = new PlannedPrescriptionSnapshot(fixture.DayId, "Synthetic V2 session", "Strength",
            (int)WorkoutActivityType.GeneralStrength, null,
            [new PlannedExerciseSnapshot(crunchRow, crunch.Id, 2, 0, 10, null, false),
             new PlannedExerciseSnapshot(fixture.ExerciseRowId, hip.Id, 2, 0, 8, null, false)]) { GenerationV2 = generation };
        await fixture.Db.SaveChangesAsync();
        await fixture.Db.WorkoutSessionLogs.Where(row => row.Id == fixture.SessionId)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.PlannedPrescriptionSnapshotJson, snapshot.Serialize()));
        var detail = await new PostgresWorkoutPlanAndSessionStore(fixture.Db).GetDetailAsync(fixture.UserId, fixture.StableDayId);
        if (verifyOrder)
        {
            Assert.Equal(new[] { hip.Id, crunch.Id }, detail.Exercises.Select(exercise => exercise.ExerciseId));
            Assert.Contains(detail.Exercises[0].Sets, set => set.IsWarmup);
        }
        else
        {
            Assert.Equal("STRENGTH", Assert.Single(detail.Activities).Tag);
            var movement = Assert.Single(detail.Exercises, exercise => exercise.ExerciseId == crunch.Id);
            Assert.False(movement.UsesEnduranceMetrics);
            Assert.True(movement.UsesWeight);
            Assert.Equal(2, movement.Sets.Count);
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public required DbContextOptions<PostgresDbContext> Options { get; init; }
        public required PostgresDbContext Db { get; init; }
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid DayId { get; } = Guid.NewGuid();
        public Guid SessionId { get; } = Guid.NewGuid();
        public Guid ExerciseRowId { get; } = Guid.NewGuid();
        public int ExerciseId { get; private set; }
        public DateTime Now { get; } = DateTime.UtcNow;
        public DateOnly Today => DateOnly.FromDateTime(Now);
        public int StableDayId => PostgresWorkoutPlanAndSessionStore.ToStableWorkoutIntId(DayId);
        public PostgresWorkoutActiveSessionStore Sessions => new(Db, new PostgresIdempotencyExecutor(), TimeProvider.System);

        public static async Task<Fixture> CreateAsync()
        {
            var connection = Environment.GetEnvironmentVariable("MC_REGISTRATION_WRITE_TEST_POSTGRES");
            var parsed = new NpgsqlConnectionStringBuilder(connection);
            Assert.Equal("127.0.0.1", parsed.Host);
            Assert.StartsWith("mc_test_", parsed.Database, StringComparison.Ordinal);
            var options = new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connection).Options;
            var fixture = new Fixture { Options = options, Db = new PostgresDbContext(options) };
            var db = fixture.Db;
            var user = fixture.UserId;
            var now = fixture.Now;
            var plan = Guid.NewGuid();
            await SyntheticTenantSession.SetAsync(db, user);
            fixture.ExerciseId = await db.WorkoutExerciseDefinitions.OrderBy(item => item.Id).Select(item => item.Id).FirstAsync();
            db.Users.Add(new UserRecord { Id = user, Email = $"v2-terminal-{user:N}@example.invalid",
                Username = $"v2_{user:N}", CredentialType = "Password", PasswordHash = "synthetic-hash",
                CreatedAt = now, UpdatedAt = now, IsOnboardingComplete = true });
            db.UserProfiles.Add(new UserProfileRecord { Id = Guid.NewGuid(), UserId = user, Name = "Synthetic V2",
                DateOfBirth = new DateOnly(1990, 1, 1), Height = 170m, Weight = 65m, Goal = "Strength",
                EquipmentLevel = "FullGym", TrainingExperienceLevel = "Beginner", UpdatedAt = now });
            db.WorkoutAccountPlanningStates.Add(new WorkoutAccountPlanningStateRecord { Id = Guid.NewGuid(), UserId = user,
                TimeZoneId = "UTC", BaselineExperience = TrainingExperienceLevel.Beginner, EffectiveExperience = TrainingExperienceLevel.Beginner });
            db.WorkoutPlans.Add(new WorkoutPlanRecord { Id = plan, UserId = user, Name = "Synthetic V2 plan", IsActive = true, CreatedAt = now });
            db.WorkoutDays.Add(new WorkoutDayRecord { Id = fixture.DayId, UserId = user, WorkoutPlanId = plan,
                Date = fixture.Today, DayOfWeek = (int)fixture.Today.DayOfWeek, Name = "Synthetic V2 session",
                WorkoutType = "Strength", ActivityTypeId = (int)WorkoutActivityType.GeneralStrength });
            db.WorkoutDayExercises.Add(new WorkoutDayExerciseRecord { Id = fixture.ExerciseRowId, UserId = user,
                WorkoutDayId = fixture.DayId, ExerciseId = fixture.ExerciseId, Sets = 2, Reps = 10 });
            var snapshot = new PlannedPrescriptionSnapshot(fixture.DayId, "Synthetic V2 session", "Strength",
                (int)WorkoutActivityType.GeneralStrength, null,
                [new PlannedExerciseSnapshot(fixture.ExerciseRowId, fixture.ExerciseId, 2, 0, 10, null, false)]);
            db.WorkoutSessionLogs.Add(new WorkoutSessionLogRecord { Id = fixture.SessionId, UserId = user,
                PlannedWorkoutDayId = fixture.DayId, WorkoutName = "Synthetic V2 session", StartedAt = now.AddMinutes(-2),
                PlannedLocalDate = fixture.Today, PlannedTimeZoneId = "UTC", PlannedTimeZoneVersion = 0,
                ConfirmedActiveSeconds = 60, PlannedPrescriptionSnapshotJson = snapshot.Serialize(), CreatedAt = now, UpdatedAt = now });
            await db.SaveChangesAsync();
            return fixture;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.Users.Where(item => item.Id == UserId).ExecuteDeleteAsync();
            await Db.DisposeAsync();
        }
    }
}
