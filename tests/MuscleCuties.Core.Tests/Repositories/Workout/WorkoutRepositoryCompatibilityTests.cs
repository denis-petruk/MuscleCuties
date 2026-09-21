using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Repositories.Workout;
using MuscleCuties.Core.Repositories.Workout.Planning;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.Core.Services.Workout.Planning;
using MuscleCuties.Core.ViewModels.Workout;
using NSubstitute;

namespace MuscleCuties.Core.Tests.Repositories.Workout;

public class WorkoutRepositoryCompatibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExerciseReads_PreservePlanAndExerciseDetails_WithCurrentOrLegacySchema(bool legacy)
    {
        using var fixture = new DatabaseFixture();
        var day = await SeedWorkoutAsync(fixture);
        if (legacy)
            await fixture.Db.Database.ExecuteSqlRawAsync("ALTER TABLE Exercises DROP COLUMN IsInjuryFriendly");

        var repository = new WorkoutRepository(fixture.Db);
        var plan = await repository.GetPlanWithDaysAsync(day.WorkoutPlanId);
        var singleDay = await repository.GetWorkoutDayWithExercisesAsync(day.Id);
        var days = await repository.GetWorkoutDaysByPlanAsync(day.WorkoutPlanId);
        var dayExercises = await repository.GetExercisesByDayAsync(day.Id);
        var catalog = await repository.GetAllExercisesAsync();

        Assert.NotNull(plan);
        Assert.NotNull(singleDay);
        foreach (var loadedDay in new[] { Assert.Single(plan.WorkoutDays), singleDay, Assert.Single(days) })
        {
            Assert.Equal(day.DayOfWeek, loadedDay.DayOfWeek);
            Assert.Equal(day.Name, loadedDay.Name);
            Assert.Equal(day.WorkoutType, loadedDay.WorkoutType);
            var entry = Assert.Single(loadedDay.WorkoutDayExercises);
            Assert.Equal(3, entry.Sets);
            Assert.Equal(12, entry.Reps);
            Assert.Equal(90, entry.DurationSeconds);
            AssertExercise(entry.Exercise, !legacy);
        }

        AssertExercise(Assert.Single(dayExercises), !legacy);
        AssertExercise(Assert.Single(catalog), !legacy);
        Assert.Empty(fixture.Db.ChangeTracker.Entries());

        var userId = await fixture.Db.Users.Select(user => user.Id).FirstAsync();
        var auth = Substitute.For<IAuthService>();
        auth.GetCurrentUserIdAsync().Returns(userId);
        var cycle = Substitute.For<ICycleService>();
        cycle.GetCurrentPhaseAsync(userId).Returns(CyclePhase.Follicular);
        using var services = new ServiceCollection()
            .AddSingleton(auth)
            .AddSingleton(cycle)
            .AddSingleton<IWorkoutService>(CreateWorkoutService(fixture))
            .AddSingleton<IWorkoutInjuryRepository>(new WorkoutInjuryRepository(fixture.Db))
            .BuildServiceProvider();
        var viewModel = new WorkoutViewModel(services.GetRequiredService<IServiceScopeFactory>());

        await viewModel.LoadDataCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsLoadError);
        Assert.False(viewModel.IsBusy);
        Assert.Equal(day.Id, Assert.Single(viewModel.WorkoutDays).Id);
        Assert.True(viewModel.HasWorkouts);
        if (legacy)
        {
            // Compatibility reads must not alter the store or hide unrelated query failures.
            await Assert.ThrowsAsync<SqliteException>(() => fixture.Db.Exercises.AsNoTracking().ToListAsync());
        }
    }

    [Fact]
    public async Task ExerciseReads_NullLegacyValues_UseConservativeDefaults()
    {
        using var fixture = new DatabaseFixture();
        var day = await SeedWorkoutAsync(fixture);
        await fixture.Db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF");
        await fixture.Db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE ExerciseCopy AS SELECT * FROM Exercises;
            DROP TABLE Exercises;
            ALTER TABLE ExerciseCopy RENAME TO Exercises;
            UPDATE Exercises SET Code = NULL, Name = NULL, Description = NULL,
                JointAreas = NULL, IsInjuryFriendly = NULL;
            """);
        await fixture.Db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON");

        var result = await new WorkoutRepository(fixture.Db).GetWorkoutDayWithExercisesAsync(day.Id);

        var exercise = Assert.Single(result!.WorkoutDayExercises).Exercise;
        Assert.NotNull(exercise);
        Assert.Equal("Exercise", exercise.Name);
        Assert.Equal(string.Empty, exercise.Code);
        Assert.Equal(string.Empty, exercise.Description);
        Assert.Equal(string.Empty, exercise.JointAreas);
        Assert.False(exercise.IsInjuryFriendly);
    }

    [Fact]
    public async Task ExerciseReads_MissingRelatedExercise_KeepScheduledEntryAndOmitNullCatalogItem()
    {
        using var fixture = new DatabaseFixture();
        var day = await SeedWorkoutAsync(fixture);
        await fixture.Db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF");
        await fixture.Db.Database.ExecuteSqlRawAsync("DELETE FROM Exercises");
        await fixture.Db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON");
        var repository = new WorkoutRepository(fixture.Db);

        var result = await repository.GetWorkoutDayWithExercisesAsync(day.Id);

        Assert.Null(Assert.Single(result!.WorkoutDayExercises).Exercise);
        Assert.Empty(await repository.GetExercisesByDayAsync(day.Id));

        var userId = await fixture.Db.Users.Select(user => user.Id).FirstAsync();
        var detail = await CreateWorkoutService(fixture).GetWorkoutSessionDetailAsync(userId, day.Id);
        var item = Assert.Single(detail.Exercises);
        Assert.Equal("Exercise", item.Name);
        Assert.Equal(string.Empty, item.ImageUrl);
        Assert.Equal(string.Empty, item.VideoUrl);
    }

    [Fact]
    public async Task ExerciseReads_UnrelatedSchemaFailure_RemainsAnError()
    {
        using var fixture = new DatabaseFixture();
        await SeedWorkoutAsync(fixture);
        await fixture.Db.Database.ExecuteSqlRawAsync("ALTER TABLE Exercises DROP COLUMN Description");

        await Assert.ThrowsAsync<SqliteException>(() => new WorkoutRepository(fixture.Db).GetAllExercisesAsync());
    }

    private static async Task<WorkoutDay> SeedWorkoutAsync(DatabaseFixture fixture)
    {
        var day = new WorkoutDay
        {
            Name = "Strength day",
            DayOfWeek = 2,
            WorkoutType = WorkoutType.Strength,
            WorkoutDayExercises =
            [
                new WorkoutDayExercise
                {
                    Sets = 3,
                    Reps = 12,
                    DurationSeconds = 90,
                    Exercise = new Exercise
                    {
                        Code = "SQUAT",
                        Name = "Squat",
                        Description = "Controlled squat",
                        JointAreas = "Knee",
                        IsInjuryFriendly = true,
                        ImageUrl = "squat.png",
                        TechniqueNotes = "Keep control"
                    }
                }
            ]
        };
        fixture.Db.WorkoutPlans.Add(new WorkoutPlan
        {
            UserId = await fixture.Db.Users.Select(user => user.Id).FirstAsync(),
            Name = "Existing plan",
            IsActive = true,
            WorkoutDays = [day]
        });
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        return day;
    }

    private static void AssertExercise(Exercise? exercise, bool isInjuryFriendly)
    {
        Assert.NotNull(exercise);
        Assert.Equal("SQUAT", exercise.Code);
        Assert.Equal("Squat", exercise.Name);
        Assert.Equal("Controlled squat", exercise.Description);
        Assert.Equal("Knee", exercise.JointAreas);
        Assert.Equal("squat.png", exercise.ImageUrl);
        Assert.Equal("Keep control", exercise.TechniqueNotes);
        Assert.Equal(isInjuryFriendly, exercise.IsInjuryFriendly);
    }

    private static WorkoutService CreateWorkoutService(DatabaseFixture fixture) => new(
        fixture.Db,
        new ContributionLookup(fixture.Db),
        new WorkoutRepository(fixture.Db),
        new UserRepository(fixture.Db),
        new WorkoutInjuryRepository(fixture.Db),
        Substitute.For<IWorkoutPlanGenerator>(),
        new WorkoutPlanner());
}
