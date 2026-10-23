using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Nutrition.Overview;
using MuscleCuties.Infrastructure.PostgreSql.Nutrition.Targets;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Archive;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Session;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;
using MuscleCuties.UseCases.Features.Workout.Overview.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Session.Contracts;
using Npgsql;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class NutritionAndWorkoutContractsIntegrationTests
{
    private const string ConnectionVariable = "MC_REGISTRATION_WRITE_TEST_POSTGRES";
    private const string LocalHost = "127.0.0.1";
    private const string SyntheticDatabasePrefix = "mc_";

    [Xunit.Fact]
    public async Task GroupedMealsAndCustomFoodsRoundTripAgainstPostgreSql()
    {
        var connectionString = RequireLocalTestConnectionString();
        var options = new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connectionString).Options;
        await using var database = new PostgresDbContext(options);

        var userId = Guid.CreateVersion7();
        var nowUtc = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(nowUtc);
        await SyntheticTenantSession.SetAsync(database, userId);

        database.Users.Add(new UserRecord
        {
            Id = userId,
            Email = $"nutrition-{userId:N}@example.invalid",
            Username = $"nut_{userId.ToString("N")[..10]}",
            CredentialType = "Password",
            PasswordHash = "synthetic-hash",
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc,
            IsOnboardingComplete = true
        });
        database.UserProfiles.Add(new UserProfileRecord
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Name = "Nutrition Tester",
            Height = 168m,
            Weight = 62m,
            Goal = "Tone",
            WeightGoalPace = "Maintain",
            TrainingExperienceLevel = "Intermediate",
            CycleTrackingMode = "Regular",
            WorkoutDaysPerWeek = 3,
            CycleLength = 28,
            SessionDurationMinutes = 60,
            EquipmentLevel = "FullGym",
            UpdatedAt = nowUtc
        });
        await database.SaveChangesAsync();

        try
        {
            var targetStore = new PostgresNutritionTargetProjectionStore(database);
            var overviewStore = new PostgresNutritionOverviewStore(database, targetStore);

            var customFood = await overviewStore.CreateCustomFoodAsync(
                userId,
                new CreateCustomFoodRequest("Skyr Berry Bowl", 100f, "g", 120f, 15f, 10f, 2f),
                CancellationToken.None);

            Xunit.Assert.NotEqual(0, customFood.FoodItemId);
            Xunit.Assert.Equal("Skyr Berry Bowl", customFood.Name);

            await overviewStore.LogMealAsync(
                userId,
                new LogMealRequest(
                    today,
                    "Breakfast",
                    TimeSpan.FromHours(8),
                    [new MealIngredientInputDto(customFood.FoodItemId, "Skyr Berry Bowl", 200f, 200f, "g", 120f, 15f, 10f, 2f, "Custom")],
                    "Skyr Concept",
                    "Follicular"),
                CancellationToken.None);

            var overview = await overviewStore.GetOverviewAsync(
                userId,
                today,
                "Sweet",
                CancellationToken.None);

            var loggedMeal = Xunit.Assert.Single(overview.Meals, m => m.LoggedMealId > 0);
            Xunit.Assert.Equal("Breakfast", loggedMeal.MealType);
            Xunit.Assert.InRange(loggedMeal.Calories, 239f, 241f);

            var ingredients = await overviewStore.GetMealIngredientsAsync(
                userId,
                loggedMeal.LoggedMealId,
                CancellationToken.None);

            var singleIngredient = Xunit.Assert.Single(ingredients);
            Xunit.Assert.Equal(customFood.FoodItemId, singleIngredient.FoodItemId);
            Xunit.Assert.Equal(200f, singleIngredient.Grams);

            await overviewStore.UpdateMealAsync(
                userId,
                new UpdateMealRequest(
                    loggedMeal.LoggedMealId,
                    today,
                    "Lunch",
                    TimeSpan.FromHours(13),
                    [new MealIngredientInputDto(customFood.FoodItemId, "Skyr Berry Bowl", 150f, 150f, "g", 120f, 15f, 10f, 2f, "Custom")]),
                CancellationToken.None);

            var updatedIngredients = await overviewStore.GetMealIngredientsAsync(
                userId,
                loggedMeal.LoggedMealId,
                CancellationToken.None);
            var updatedSingle = Xunit.Assert.Single(updatedIngredients);
            Xunit.Assert.Equal(150f, updatedSingle.Grams);
        }
        finally
        {
            await database.Users.Where(u => u.Id == userId).ExecuteDeleteAsync();
        }
    }

    [Xunit.Fact]
    public async Task WorkoutOverviewAndSessionActionsRoundTripAgainstPostgreSql()
    {
        var connectionString = RequireLocalTestConnectionString();
        var options = new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connectionString).Options;
        await using var database = new PostgresDbContext(options);

        var userId = Guid.CreateVersion7();
        var nowUtc = DateTime.UtcNow;
        await SyntheticTenantSession.SetAsync(database, userId);
        var today = DateOnly.FromDateTime(nowUtc);

        database.Users.Add(new UserRecord
        {
            Id = userId,
            Email = $"workout-{userId:N}@example.invalid",
            Username = $"wrk_{userId.ToString("N")[..10]}",
            CredentialType = "Password",
            PasswordHash = "synthetic-hash",
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc,
            IsOnboardingComplete = true
        });
        database.UserProfiles.Add(new UserProfileRecord
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Name = "Workout Tester",
            Height = 170m,
            Weight = 64m,
            Goal = "BuildMuscle",
            WeightGoalPace = "Maintain",
            TrainingExperienceLevel = "Intermediate",
            CycleTrackingMode = "Regular",
            WorkoutDaysPerWeek = 3,
            CycleLength = 28,
            SessionDurationMinutes = 60,
            EquipmentLevel = "FullGym",
            PreferredWorkoutActivityTypes = "HighVolumeStrength",
            UpdatedAt = nowUtc
        });
        await database.SaveChangesAsync();

        try
        {
            var planner = new GenerateWorkoutPlanUseCase(new PostgresWorkoutPlanningReferenceDataSource(database));
            var workoutStore = new PostgresWorkoutPlanAndSessionStore(database, planner);
            var archiveStore = new PostgresWorkoutArchiveStore(database);

            var overview = await workoutStore.LoadOverviewAsync(
                userId,
                new WorkoutOverviewRequest(today),
                CancellationToken.None);

            Xunit.Assert.False(string.IsNullOrWhiteSpace(overview.PlanName));
            var activeWorkout = overview.Workouts.First(w => !w.IsRestDay && w.WorkoutDayId > 0);

            var detail = await workoutStore.GetDetailAsync(userId, activeWorkout.WorkoutDayId, CancellationToken.None);
            Xunit.Assert.False(detail.IsRestDay);
            Xunit.Assert.NotEmpty(detail.Exercises);

            var firstExercise = detail.Exercises[0];
            await workoutStore.SaveMovementNoteAsync(
                userId,
                new SaveWorkoutMovementNoteRequest(firstExercise.WorkoutDayExerciseId, "Keep ribcage stacked."),
                CancellationToken.None);

            var note = await workoutStore.GetMovementNoteAsync(
                userId,
                firstExercise.WorkoutDayExerciseId,
                CancellationToken.None);
            Xunit.Assert.Equal("Keep ribcage stacked.", note.Notes);

            var swapCandidates = await workoutStore.GetSwapCandidatesAsync(
                userId,
                firstExercise.WorkoutDayExerciseId,
                CancellationToken.None);
            if (swapCandidates.Count > 0)
            {
                await workoutStore.SwapAsync(
                    userId,
                    new SwapWorkoutExerciseRequest(
                        firstExercise.WorkoutDayExerciseId,
                        swapCandidates[0].ExerciseId,
                        true),
                    CancellationToken.None);
            }

            await workoutStore.LogAsync(
                userId,
                new LogWorkoutSessionRequest(
                    activeWorkout.WorkoutDayId,
                    today,
                    detail.Exercises.Select(ex => new WorkoutExerciseLogDto(
                        ex.WorkoutDayExerciseId,
                        ex.ExerciseId,
                        3,
                        10,
                        40f)).ToArray()),
                CancellationToken.None);

            var archiveHistory = await archiveStore.GetWorkoutHistoryAsync(userId, 10, 0, CancellationToken.None);
            Xunit.Assert.NotEmpty(archiveHistory);
        }
        finally
        {
            await database.Users.Where(u => u.Id == userId).ExecuteDeleteAsync();
        }
    }

    private static string RequireLocalTestConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Xunit.Assert.False(string.IsNullOrWhiteSpace(connectionString),
            $"Set {ConnectionVariable} to a disposable local PostgreSQL test database.");
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        Xunit.Assert.Equal(LocalHost, builder.Host);
        Xunit.Assert.StartsWith(SyntheticDatabasePrefix, builder.Database, StringComparison.Ordinal);
        return connectionString!;
    }
}
