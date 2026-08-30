using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Repositories.Workout;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.Core.Services.Workout.Planning;

namespace MuscleCuties.Core.Tests.Services.Workout;

public class WorkoutServiceTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public WorkoutServiceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private WorkoutService CreateService() =>
        new(
            new WorkoutRepository(_fixture.Db),
            new UserRepository(_fixture.Db),
            new WorkoutPlanner());

    private async Task<User> SeedUserWithProfileAsync(
        string email,
        UserGoal goal = UserGoal.MuscleTone,
        TrainingExperienceLevel experienceLevel = TrainingExperienceLevel.Intermediate,
        int workoutDaysPerWeek = 4)
    {
        var user = new User { Email = email, PasswordHash = "hash", CreatedAt = DateTime.UtcNow };
        await _fixture.Db.Users.AddAsync(user);
        await _fixture.Db.SaveChangesAsync();

        await _fixture.Db.UserProfiles.AddAsync(new UserProfile
        {
            UserId = user.Id,
            Name = "Test",
            DateOfBirth = DateTime.Today.AddYears(-25),
            Goal = goal,
            TrainingExperienceLevel = experienceLevel,
            WorkoutDaysPerWeek = workoutDaysPerWeek,
            CycleLength = 28,
            UpdatedAt = DateTime.UtcNow
        });
        await SeedExerciseLibraryAsync();
        await _fixture.Db.SaveChangesAsync();

        return user;
    }

    [Fact]
    public async Task GetPlanSummaryAsync_WithProfileAndNoActivePlan_GeneratesPlan()
    {
        var user = await SeedUserWithProfileAsync("workout_svc1@test.com", workoutDaysPerWeek: 4);
        var service = CreateService();

        var summary = await service.GetPlanSummaryAsync(user.Id, CyclePhase.Follicular);

        Assert.NotNull(summary.ActivePlan);
        Assert.Equal("Full body hypertrophy training", summary.ActivePlan!.Name);
        Assert.Equal(CyclePhase.Follicular, summary.ActivePlan.CyclePhaseTarget);
        Assert.Equal(7, summary.WorkoutDays.Count);
        Assert.Equal(7, summary.Workouts.Count);
        Assert.Contains(summary.Workouts, workout => workout.IsRestDay && workout.Title == "Living happy life");
        Assert.Equal(4, summary.WorkoutDays.Count(day => day.WorkoutType != WorkoutType.Rest));
        Assert.All(summary.WorkoutDays.Where(day => day.WorkoutType != WorkoutType.Rest), day => Assert.NotEmpty(day.WorkoutDayExercises));
    }

    [Fact]
    public async Task GetPlanSummaryAsync_WhenGeneratedPlanNoLongerMatchesProfile_ReplacesIt()
    {
        var user = await SeedUserWithProfileAsync("workout_svc2@test.com", workoutDaysPerWeek: 5);
        var service = CreateService();
        var firstSummary = await service.GetPlanSummaryAsync(user.Id, CyclePhase.Follicular);

        var profile = await _fixture.Db.UserProfiles.SingleAsync(profile => profile.UserId == user.Id);
        profile.WorkoutDaysPerWeek = 2;
        profile.UpdatedAt = DateTime.UtcNow;
        await _fixture.Db.SaveChangesAsync();

        var secondSummary = await service.GetPlanSummaryAsync(user.Id, CyclePhase.Follicular);
        var activePlanCount = await _fixture.Db.WorkoutPlans.CountAsync(plan => plan.UserId == user.Id && plan.IsActive);

        Assert.NotNull(firstSummary.ActivePlan);
        Assert.NotNull(secondSummary.ActivePlan);
        Assert.NotEqual(firstSummary.ActivePlan!.Id, secondSummary.ActivePlan!.Id);
        Assert.Equal(7, secondSummary.WorkoutDays.Count);
        Assert.Equal(2, secondSummary.WorkoutDays.Count(day => day.WorkoutType != WorkoutType.Rest));
        Assert.Equal(1, activePlanCount);
    }

    [Fact]
    public async Task GetWorkoutSessionDetailAsync_WithPreviousWeight_RecommendsProgression()
    {
        var user = await SeedUserWithProfileAsync("workout_svc_detail@test.com");
        var service = CreateService();
        var (day, dayExercise) = await SeedWorkoutDayAsync(user.Id, "Goblet Squat", 3, 10);

        await _fixture.Db.WorkoutLogs.AddAsync(new WorkoutLog
        {
            UserId = user.Id,
            WorkoutDayId = day.Id,
            Date = DateTime.Today.AddDays(-7),
            CompletionPercent = 100,
            CreatedAt = DateTime.UtcNow.AddDays(-7),
            ExerciseLogs =
            [
                new WorkoutExerciseLog
                {
                    WorkoutDayExerciseId = dayExercise.Id,
                    ExerciseId = dayExercise.ExerciseId,
                    CompletedSets = 3,
                    CompletedReps = 10,
                    WeightKg = 20f,
                    CreatedAt = DateTime.UtcNow.AddDays(-7)
                }
            ]
        });
        await _fixture.Db.SaveChangesAsync();

        var detail = await service.GetWorkoutSessionDetailAsync(user.Id, day.Id);
        var exercise = detail.Exercises.Single();

        Assert.Equal("Lower body strength", detail.Title);
        Assert.Equal("3 sets x 10 reps", exercise.TargetText);
        Assert.Equal("Last 20 kg with 3 x 10", exercise.PreviousText);
        Assert.Equal("Try 22.5 kg", exercise.RecommendationText);
        Assert.Equal("22.5", exercise.LoggedWeightText);
    }

    [Fact]
    public async Task GetWorkoutSessionDetailAsync_ForCardio_UsesEnduranceMetrics()
    {
        var user = await SeedUserWithProfileAsync("workout_svc_cardio_detail@test.com");
        var service = CreateService();
        var (day, dayExercise) = await SeedWorkoutDayAsync(
            user.Id,
            "Zone 2 Ride",
            0,
            0,
            WorkoutType.Cardio,
            1800);

        await _fixture.Db.WorkoutLogs.AddAsync(new WorkoutLog
        {
            UserId = user.Id,
            WorkoutDayId = day.Id,
            Date = DateTime.Today.AddDays(-7),
            CompletionPercent = 100,
            CreatedAt = DateTime.UtcNow.AddDays(-7),
            ExerciseLogs =
            [
                new WorkoutExerciseLog
                {
                    WorkoutDayExerciseId = dayExercise.Id,
                    ExerciseId = dayExercise.ExerciseId,
                    CompletedDurationSeconds = 1800,
                    DistanceKm = 5f,
                    PowerWatts = 160,
                    CadenceRpm = 88,
                    AverageHeartRateBpm = 145,
                    EffortRating = 6,
                    CreatedAt = DateTime.UtcNow.AddDays(-7)
                }
            ]
        });
        await _fixture.Db.SaveChangesAsync();

        var detail = await service.GetWorkoutSessionDetailAsync(user.Id, day.Id);
        var exercise = detail.Exercises.Single();

        Assert.Equal("Steady aerobic work at a repeatable, conversational pace.", detail.Subtitle);
        Assert.True(exercise.UsesEnduranceMetrics);
        Assert.False(exercise.UsesStrengthMetrics);
        Assert.True(exercise.UsesPowerMetric);
        Assert.True(exercise.UsesCadenceMetric);
        Assert.False(exercise.UsesPaceMetric);
        Assert.Equal("30 min - watts, rpm, HR", exercise.TargetText);
        Assert.Equal("Last 30 min with 160 W with 88 rpm with 5 km with 145 bpm with effort 6/10", exercise.PreviousText);
        Assert.Equal("Start near 160 W, keep cadence around 80-95 rpm, and let heart rate confirm the effort.", exercise.RecommendationText);
        Assert.Equal("30", exercise.LoggedDurationMinutesText);
        Assert.Equal("5", exercise.LoggedDistanceKmText);
        Assert.Equal("160", exercise.LoggedPowerWattsText);
        Assert.Equal("88", exercise.LoggedCadenceRpmText);
        Assert.Equal("145", exercise.LoggedHeartRateText);
        Assert.Equal("6", exercise.LoggedEffortText);
    }

    [Fact]
    public async Task LogWorkoutSessionAsync_SavesPerExerciseWeights()
    {
        var user = await SeedUserWithProfileAsync("workout_svc_log@test.com");
        var service = CreateService();
        var (day, dayExercise) = await SeedWorkoutDayAsync(user.Id, "Hip Thrust", 4, 8);

        await service.LogWorkoutSessionAsync(
            user.Id,
            day.Id,
            [new WorkoutExerciseLogInput(dayExercise.Id, dayExercise.ExerciseId, 4, 8, 45f)],
            DateTime.Today);

        var log = await _fixture.Db.WorkoutLogs
            .Include(workoutLog => workoutLog.ExerciseLogs)
            .SingleAsync(workoutLog => workoutLog.UserId == user.Id && workoutLog.WorkoutDayId == day.Id);

        var exerciseLog = Assert.Single(log.ExerciseLogs);
        Assert.Equal(100, log.CompletionPercent);
        Assert.Equal(dayExercise.Id, exerciseLog.WorkoutDayExerciseId);
        Assert.Equal(4, exerciseLog.CompletedSets);
        Assert.Equal(8, exerciseLog.CompletedReps);
        Assert.Equal(45f, exerciseLog.WeightKg);
    }

    [Fact]
    public async Task LogWorkoutSessionAsync_SavesCardioMetrics()
    {
        var user = await SeedUserWithProfileAsync("workout_svc_cardio_log@test.com");
        var service = CreateService();
        var (day, dayExercise) = await SeedWorkoutDayAsync(
            user.Id,
            "Zone 2 Ride",
            0,
            0,
            WorkoutType.Cardio,
            1800);

        await service.LogWorkoutSessionAsync(
            user.Id,
            day.Id,
            [new WorkoutExerciseLogInput(dayExercise.Id, dayExercise.ExerciseId, 0, 0, null, 1800, 5.5f, 148, 327)],
            DateTime.Today);

        var log = await _fixture.Db.WorkoutLogs
            .Include(workoutLog => workoutLog.ExerciseLogs)
            .SingleAsync(workoutLog => workoutLog.UserId == user.Id && workoutLog.WorkoutDayId == day.Id);

        var exerciseLog = Assert.Single(log.ExerciseLogs);
        Assert.Equal(100, log.CompletionPercent);
        Assert.Equal(0, exerciseLog.CompletedSets);
        Assert.Equal(0, exerciseLog.CompletedReps);
        Assert.Null(exerciseLog.WeightKg);
        Assert.Equal(1800, exerciseLog.CompletedDurationSeconds);
        Assert.Equal(5.5f, exerciseLog.DistanceKm);
        Assert.Equal(148, exerciseLog.AverageHeartRateBpm);
        Assert.Equal(327, exerciseLog.PaceSecondsPerKm);
    }

    [Fact]
    public async Task LogWorkoutSessionAsync_MergesSeparateExerciseLogsForSameDay()
    {
        var user = await SeedUserWithProfileAsync("workout_svc_merge_log@test.com");
        var service = CreateService();
        var plan = new WorkoutPlan
        {
            UserId = user.Id,
            Name = $"Merge plan {Guid.NewGuid()}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var exerciseA = await _fixture.Db.Exercises.FirstAsync(e => e.Name == "Hip Thrust");
        var exerciseB = await _fixture.Db.Exercises.FirstAsync(e => e.Name == "Romanian Deadlift");
        var day = new WorkoutDay
        {
            DayOfWeek = 1,
            WorkoutType = WorkoutType.Strength,
            Name = "Lower body strength"
        };
        var dayExerciseA = new WorkoutDayExercise { ExerciseId = exerciseA.Id, Sets = 4, Reps = 8 };
        var dayExerciseB = new WorkoutDayExercise { ExerciseId = exerciseB.Id, Sets = 3, Reps = 10 };
        day.WorkoutDayExercises.Add(dayExerciseA);
        day.WorkoutDayExercises.Add(dayExerciseB);
        plan.WorkoutDays.Add(day);
        await _fixture.Db.WorkoutPlans.AddAsync(plan);
        await _fixture.Db.SaveChangesAsync();

        await service.LogWorkoutSessionAsync(
            user.Id,
            day.Id,
            [new WorkoutExerciseLogInput(dayExerciseA.Id, exerciseA.Id, 4, 8, 50f)],
            DateTime.Today);
        await service.LogWorkoutSessionAsync(
            user.Id,
            day.Id,
            [new WorkoutExerciseLogInput(dayExerciseB.Id, exerciseB.Id, 3, 10, 35f)],
            DateTime.Today);

        var log = await _fixture.Db.WorkoutLogs
            .Include(workoutLog => workoutLog.ExerciseLogs)
            .SingleAsync(workoutLog => workoutLog.UserId == user.Id && workoutLog.WorkoutDayId == day.Id);

        Assert.Equal(100, log.CompletionPercent);
        Assert.Equal(2, log.ExerciseLogs.Count);
        Assert.Contains(log.ExerciseLogs, exerciseLog => exerciseLog.WorkoutDayExerciseId == dayExerciseA.Id);
        Assert.Contains(log.ExerciseLogs, exerciseLog => exerciseLog.WorkoutDayExerciseId == dayExerciseB.Id);
    }

    [Fact]
    public async Task LogWorkoutSessionAsync_ForRestDay_SavesCompletedRestWithoutExercises()
    {
        var user = await SeedUserWithProfileAsync("workout_svc_rest_log@test.com");
        var service = CreateService();
        var plan = new WorkoutPlan
        {
            UserId = user.Id,
            Name = $"Manual rest plan {Guid.NewGuid()}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var restDay = new WorkoutDay
        {
            DayOfWeek = 2,
            WorkoutType = WorkoutType.Rest,
            Name = "Living happy life"
        };
        plan.WorkoutDays.Add(restDay);
        await _fixture.Db.WorkoutPlans.AddAsync(plan);
        await _fixture.Db.SaveChangesAsync();

        await service.LogWorkoutSessionAsync(user.Id, restDay.Id, [], DateTime.Today);

        var log = await _fixture.Db.WorkoutLogs
            .Include(workoutLog => workoutLog.ExerciseLogs)
            .SingleAsync(workoutLog => workoutLog.UserId == user.Id && workoutLog.WorkoutDayId == restDay.Id);

        Assert.Equal(100, log.CompletionPercent);
        Assert.Empty(log.ExerciseLogs);
    }

    private async Task SeedExerciseLibraryAsync()
    {
        var existingNames = await _fixture.Db.Exercises
            .Select(exercise => exercise.Name)
            .ToListAsync();
        var existing = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var exercises = ExerciseNames
            .Where(name => !existing.Contains(name))
            .Select((name, index) => new Exercise
            {
                Name = name,
                Description = name,
                PrimaryMuscle = MuscleGroup.Abs,
                Id = 0
            })
            .ToList();

        if (exercises.Count == 0)
            return;

        await _fixture.Db.Exercises.AddRangeAsync(exercises);
    }

    private async Task<(WorkoutDay Day, WorkoutDayExercise DayExercise)> SeedWorkoutDayAsync(
        int userId,
        string exerciseName,
        int sets,
        int reps,
        WorkoutType workoutType = WorkoutType.Strength,
        int? durationSeconds = null)
    {
        var exercise = await _fixture.Db.Exercises.FirstAsync(e => e.Name == exerciseName);
        var plan = new WorkoutPlan
        {
            UserId = userId,
            Name = $"Manual plan {Guid.NewGuid()}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var day = new WorkoutDay
        {
            DayOfWeek = 1,
            WorkoutType = workoutType,
            Name = workoutType is WorkoutType.Cardio ? "Smooth Ride" : "Lower body strength"
        };
        var dayExercise = new WorkoutDayExercise
        {
            ExerciseId = exercise.Id,
            Sets = sets,
            Reps = reps,
            DurationSeconds = durationSeconds
        };
        day.WorkoutDayExercises.Add(dayExercise);
        plan.WorkoutDays.Add(day);

        await _fixture.Db.WorkoutPlans.AddAsync(plan);
        await _fixture.Db.SaveChangesAsync();

        return (day, dayExercise);
    }

    private static readonly string[] ExerciseNames =
    [
        "Goblet Squat",
        "Hip Thrust",
        "Romanian Deadlift",
        "Step-Up",
        "Reverse Lunge",
        "Glute Bridge",
        "Calf Raise",
        "Incline Push-Up",
        "Incline Dumbbell Press",
        "Dumbbell Row",
        "Seated Cable Row",
        "Lat Pulldown",
        "Overhead Press",
        "Lateral Raise",
        "Face Pull",
        "Biceps Curl",
        "Triceps Pressdown",
        "Dead Bug",
        "Side Plank",
        "Pallof Press",
        "Bird Dog",
        "Plank",
        "Bike Intervals",
        "Zone 2 Ride",
        "Easy Walk",
        "Mobility Flow",
        "Yoga Flow",
        "Power Yoga",
        "Yin Yoga",
        "Restorative Yoga",
        "Pilates Flow",
        "Active Recovery Flow",
        "Breathing Reset",
        "Rock Climbing",
        "Swimming",
        "Dance Cardio"
    ];
}
