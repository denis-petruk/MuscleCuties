using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Repositories;

namespace MuscleCuties.Core.Tests.Repositories;

public class WorkoutRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public WorkoutRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<User> SeedUserAsync(string email)
    {
        var user = new User { Email = email, PasswordHash = "hash", CreatedAt = DateTime.UtcNow };
        await _fixture.Db.Users.AddAsync(user);
        await _fixture.Db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task GetActivePlanAsync_IsActiveTrue_ReturnsPlan()
    {
        var user = await SeedUserAsync("wo1@test.com");
        var repo = new WorkoutRepository(_fixture.Db);

        var plan = new WorkoutPlan { UserId = user.Id, Name = "Plan A", IsActive = true, CreatedAt = DateTime.UtcNow };
        await repo.AddAsync(plan);

        var result = await repo.GetActivePlanAsync(user.Id);

        Assert.NotNull(result);
        Assert.Equal("Plan A", result.Name);
    }

    [Fact]
    public async Task AddWorkoutLogAsync_ValidLog_PersistedWithId()
    {
        var user = await SeedUserAsync("wo2@test.com");
        var repo = new WorkoutRepository(_fixture.Db);

        var plan = new WorkoutPlan { UserId = user.Id, Name = "Plan B", IsActive = true, CreatedAt = DateTime.UtcNow };
        await repo.AddAsync(plan);

        var day = new WorkoutDay { WorkoutPlanId = plan.Id, DayOfWeek = 0, Name = "Leg Day", WorkoutType = WorkoutType.Strength, DurationMinutes = 45 };
        await _fixture.Db.WorkoutDays.AddAsync(day);
        await _fixture.Db.SaveChangesAsync();

        var log = new WorkoutLog
        {
            UserId = user.Id,
            WorkoutDayId = day.Id,
            Date = DateTime.UtcNow.Date,
            CompletionPercent = 100,
            CreatedAt = DateTime.UtcNow
        };
        await repo.AddWorkoutLogAsync(log);

        Assert.True(log.Id > 0);
    }

    [Fact]
    public async Task GetWorkoutLogsByDateAsync_OnDate_ReturnsLogs()
    {
        var user = await SeedUserAsync("wo3@test.com");
        var repo = new WorkoutRepository(_fixture.Db);

        var plan = new WorkoutPlan { UserId = user.Id, Name = "Plan C", IsActive = true, CreatedAt = DateTime.UtcNow };
        await repo.AddAsync(plan);

        var day = new WorkoutDay { WorkoutPlanId = plan.Id, DayOfWeek = 1, Name = "Upper Body", WorkoutType = WorkoutType.Strength, DurationMinutes = 45 };
        await _fixture.Db.WorkoutDays.AddAsync(day);
        await _fixture.Db.SaveChangesAsync();

        var today = DateTime.UtcNow.Date;
        await repo.AddWorkoutLogAsync(new WorkoutLog { UserId = user.Id, WorkoutDayId = day.Id, Date = today, CompletionPercent = 75, CreatedAt = DateTime.UtcNow });

        var results = await repo.GetWorkoutLogsByDateAsync(user.Id, today);
        Assert.Single(results);
    }

    [Fact]
    public async Task GetPlanByPhaseAsync_MatchingPlan_ReturnsPlan()
    {
        var user = await SeedUserAsync("wo4@test.com");
        var repo = new WorkoutRepository(_fixture.Db);

        var plan = new WorkoutPlan
        {
            UserId = user.Id,
            Name = "Follicular Strength",
            IsActive = false,
            CyclePhaseTarget = CyclePhase.Follicular,
            CreatedAt = DateTime.UtcNow
        };
        await repo.AddAsync(plan);

        var result = await repo.GetPlanByPhaseAsync(user.Id, CyclePhase.Follicular);

        Assert.NotNull(result);
        Assert.Equal(CyclePhase.Follicular, result!.CyclePhaseTarget);
    }

    [Fact]
    public async Task GetExerciseByCodeAsync_ExistingCode_ReturnsExercise()
    {
        var repo = new WorkoutRepository(_fixture.Db);

        var exercise = new Exercise
        {
            Code = "SQUAT_TEST",
            Name = "Test Squat",
            Description = "A squat.",
            PrimaryMuscle = MuscleGroup.Quads
        };
        await _fixture.Db.Exercises.AddAsync(exercise);
        await _fixture.Db.SaveChangesAsync();

        var result = await repo.GetExerciseByCodeAsync("SQUAT_TEST");

        Assert.NotNull(result);
        Assert.Equal("SQUAT_TEST", result!.Code);
    }

    [Fact]
    public async Task SeedExercises_ThenGetByCode_ReturnsExercise()
    {
        await _fixture.Db.SeedExercisesAsync();
        var repo = new WorkoutRepository(_fixture.Db);

        var plank = await repo.GetExerciseByCodeAsync("PLANK");
        var childsPose = await repo.GetExerciseByCodeAsync("CHILDS_POSE");

        Assert.NotNull(plank);
        Assert.Equal(MuscleGroup.Abs, plank!.PrimaryMuscle);
        Assert.NotNull(childsPose);
        Assert.Equal(MuscleGroup.LowerBack, childsPose!.PrimaryMuscle);
    }

    [Fact]
    public async Task GetWorkoutDayWithExercisesAsync_DayExists_ReturnsWithExercises()
    {
        var user = await SeedUserAsync("wo5@test.com");
        var repo = new WorkoutRepository(_fixture.Db);

        var plan = new WorkoutPlan { UserId = user.Id, Name = "Test Plan", IsActive = true, CreatedAt = DateTime.UtcNow };
        await repo.AddAsync(plan);

        var day = new WorkoutDay { WorkoutPlanId = plan.Id, DayOfWeek = 1, Name = "Push Day", WorkoutType = WorkoutType.Strength, DurationMinutes = 45 };
        await _fixture.Db.WorkoutDays.AddAsync(day);
        await _fixture.Db.SaveChangesAsync();

        var result = await repo.GetWorkoutDayWithExercisesAsync(day.Id);

        Assert.NotNull(result);
        Assert.Equal("Push Day", result!.Name);
    }
}