using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Repositories.Nutrition;
using MuscleCuties.Core.Repositories.Workout;

namespace MuscleCuties.Core.Tests.Data;

public sealed class AppDatabaseSchemaUpgradeTests : IAsyncLifetime
{
    private static readonly DateTime LogDate = new(2026, 9, 2);
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"musclecuties-upgrade-{Guid.NewGuid():N}.db");

    public async Task InitializeAsync()
    {
        await using var db = CreateDatabase();
        await db.Database.EnsureCreatedAsync();

        var user = new User
        {
            Id = 1000, Email = "schema-upgrade@test.com", PasswordHash = "hash",
            UserProfile = new UserProfile { Name = "Saved profile", Height = 165, Weight = 60 }
        };
        var exercise = new Exercise
        {
            Id = 1000, Code = "LEGACY_MOVE", Name = "Legacy Move",
            Description = "Saved exercise", IsInjuryFriendly = true
        };
        db.WorkoutDays.Add(new WorkoutDay
        {
            Id = 1000, Name = "Saved workout", DayOfWeek = 2,
            WorkoutPlan = new WorkoutPlan { Name = "Saved plan", User = user, IsActive = true },
            WorkoutDayExercises = [new WorkoutDayExercise { Exercise = exercise, Sets = 3, Reps = 10 }]
        });
        db.LoggedMeals.Add(new LoggedMeal
        {
            Id = 1000, User = user, Date = LogDate, LoggedAt = LogDate.AddHours(12), CreatedAt = LogDate,
            Entries =
            [
                new LoggedMealEntry
                {
                    FoodItem = new FoodItem { Id = 1000, Name = "Saved food", Calories = 125 }, Grams = 150
                }
            ]
        });
        await db.SaveChangesAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Initialize_UpgradesLegacyDatabaseAndPreservesSavedData(bool startupOnly)
    {
        await using (var legacy = CreateDatabase())
        {
            await legacy.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "Exercises" DROP COLUMN "IsInjuryFriendly";
                DROP TABLE "EngineConfig";
                DROP TABLE "MealTemplateEntries";
                DROP TABLE "MealTemplates";
                """);
        }

        // Open a new context, as the app does when restarting with an older file.
        await using (var db = CreateDatabase())
        {
            await InitializeDatabaseAsync(db, startupOnly);

            var day = await new WorkoutRepository(db).GetWorkoutDayWithExercisesAsync(1000);
            Assert.NotNull(day);
            var savedExercise = Assert.Single(day.WorkoutDayExercises);
            Assert.Equal(3, savedExercise.Sets);
            Assert.Equal(10, savedExercise.Reps);
            Assert.NotNull(savedExercise.Exercise);
            Assert.Equal("Saved exercise", savedExercise.Exercise.Description);
            Assert.False(savedExercise.Exercise.IsInjuryFriendly);

            var meal = Assert.Single(await new NutritionRepository(db).GetLoggedMealsByDateAsync(1000, LogDate));
            Assert.Equal(1000, meal.Id);
            var entry = Assert.Single(meal.Entries);
            Assert.Equal(150f, entry.Grams);
            Assert.Equal("Saved food", entry.FoodItem!.Name);
            Assert.Null(await ReadMealTemplateIdAsync(db));
            Assert.Equal(1, await db.Database.SqlQueryRaw<int>("""
                SELECT "unique" AS "Value" FROM pragma_index_list('EngineConfig')
                WHERE "name" = 'IX_EngineConfig_Section_Key'
                """).SingleAsync());

            // Values written after the upgrade must survive the next startup.
            var exercise = await db.Exercises.SingleAsync(e => e.Id == 1000);
            exercise.IsInjuryFriendly = true;
            db.WorkoutPlanningConfigEntries.Add(new WorkoutPlanningConfigEntry
            {
                Section = "custom", Key = "preserved", Value = "42"
            });
            await db.SaveChangesAsync();
        }

        await using var reopened = CreateDatabase();
        await InitializeDatabaseAsync(reopened, startupOnly);

        Assert.True((await reopened.Exercises.SingleAsync(e => e.Id == 1000)).IsInjuryFriendly);
        Assert.Equal("42", (await reopened.WorkoutPlanningConfigEntries.SingleAsync(e => e.Section == "custom")).Value);
        Assert.Single(await reopened.Users.ToListAsync());
        Assert.Single(await reopened.LoggedMeals.ToListAsync());
        Assert.Single(await reopened.LoggedMealEntries.ToListAsync());
        Assert.Null(await ReadMealTemplateIdAsync(reopened));
        Assert.Equal(System.Data.ConnectionState.Closed, reopened.Database.GetDbConnection().State);
    }

    [Fact]
    public async Task InitializeStartupAsync_PreservesExistingColumnsAndTemplateLinks()
    {
        await using var db = CreateDatabase();
        db.MealTemplates.Add(new MealTemplate { Id = 1000, Name = "Saved template", CreatedAt = LogDate });
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "LoggedMeals" ADD COLUMN "MealTemplateId" INTEGER NULL
                REFERENCES "MealTemplates" ("Id") ON DELETE SET NULL;
            UPDATE "LoggedMeals" SET "MealTemplateId" = 1000 WHERE "Id" = 1000;
            """);
        await db.Database.OpenConnectionAsync();

        await db.InitializeStartupAsync();
        await db.InitializeStartupAsync();

        Assert.Equal(1000, await ReadMealTemplateIdAsync(db));
        Assert.True((await db.Exercises.AsNoTracking().SingleAsync(e => e.Id == 1000)).IsInjuryFriendly);
        Assert.Equal(System.Data.ConnectionState.Open, db.Database.GetDbConnection().State);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Initialize_UpgradesDatabaseFromBeforeWorkoutPlanning(bool startupOnly)
    {
        await using (var legacy = CreateDatabase())
        {
            await legacy.Database.ExecuteSqlRawAsync("""
                DROP TABLE "SlotTemplates";
                DROP TABLE "ExerciseMuscleContributions";
                DROP TABLE "GoalTierWeights";
                DROP TABLE "VolumeBudgetRows";
                DROP TABLE "WeekTemplates";
                DROP TABLE "SessionArchetypes";
                DROP TABLE "EngineExercises";
                DROP TABLE "EngineMuscleGroups";
                DROP TABLE "DailyReadinessLogs";
                DROP TABLE "EngineInjuryLogs";
                DROP TABLE "EngineExercisePreferences";
                DROP TABLE "EngineConfig";
                ALTER TABLE "Exercises" DROP COLUMN "IsInjuryFriendly";
                ALTER TABLE "UserProfiles" DROP COLUMN "SessionDurationMinutes";
                ALTER TABLE "UserProfiles" DROP COLUMN "EquipmentLevel";
                ALTER TABLE "UserProfiles" DROP COLUMN "PhaseBaselinesJson";
                """);
        }

        await using var upgraded = CreateDatabase();
        await InitializeDatabaseAsync(upgraded, startupOnly);
        await upgraded.SeedDeferredReferenceDataAsync();
        await InitializeDatabaseAsync(upgraded, startupOnly);

        var profile = await upgraded.UserProfiles.AsNoTracking().SingleAsync();
        Assert.Equal("Saved profile", profile.Name);
        Assert.Equal(165f, profile.Height);
        Assert.Equal(60f, profile.Weight);
        Assert.Equal(60, profile.SessionDurationMinutes);
        Assert.Equal("FullGym", profile.EquipmentLevel);
        Assert.Equal(string.Empty, profile.PhaseBaselinesJson);
        Assert.True(await upgraded.WorkoutPlanningConfigEntries.AnyAsync());
        Assert.True(await upgraded.WorkoutExerciseDefinitions.AnyAsync());
        Assert.True(await upgraded.WorkoutMuscleGroups.AnyAsync());
        Assert.True(await upgraded.SlotTemplates.AnyAsync());
        Assert.Empty(await upgraded.DailyReadinessLogs.ToListAsync());
        Assert.Empty(await upgraded.WorkoutInjuryLogs.ToListAsync());
        Assert.Empty(await upgraded.UserExercisePreferences.ToListAsync());
        Assert.Single(await upgraded.LoggedMeals.ToListAsync());
        Assert.NotNull(await new WorkoutRepository(upgraded).GetWorkoutDayWithExercisesAsync(1000));
    }

    [Fact]
    public async Task InitializeStartupAsync_AllowsConcurrentContextsToUpgradeOnce()
    {
        await using (var legacy = CreateDatabase())
        {
            // Existing planning rows make startup backfill materialize Exercises.
            legacy.WorkoutExerciseDefinitions.Add(new WorkoutExerciseDefinition { Id = 1000, Name = "Legacy Move" });
            await legacy.SaveChangesAsync();
            await legacy.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "Exercises" DROP COLUMN "IsInjuryFriendly";
                DROP TABLE "EngineConfig";
                """);
        }

        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
        {
            await using var db = CreateDatabase();
            await db.InitializeStartupAsync();
        })));

        await using var upgraded = CreateDatabase();
        Assert.Equal("ENGINE_1000", (await upgraded.Exercises.SingleAsync()).Code);
        Assert.Empty(await upgraded.WorkoutPlanningConfigEntries.ToListAsync());
        Assert.Null(await ReadMealTemplateIdAsync(upgraded));
        Assert.Equal(16, await upgraded.QuizQuestions.CountAsync());
    }

    private AppDatabase CreateDatabase() => new(new DbContextOptionsBuilder<AppDatabase>()
        .UseSqlite(new SqliteConnectionStringBuilder { DataSource = _databasePath, Pooling = false }.ToString())
        .Options);

    private static Task InitializeDatabaseAsync(AppDatabase db, bool startupOnly) =>
        startupOnly ? db.InitializeStartupAsync() : db.InitializeAsync();

    private static Task<int?> ReadMealTemplateIdAsync(AppDatabase db) => db.Database.SqlQueryRaw<int?>("""
        SELECT "MealTemplateId" AS "Value" FROM "LoggedMeals" WHERE "Id" = 1000
        """).SingleAsync();

    public Task DisposeAsync()
    {
        File.Delete(_databasePath);
        return Task.CompletedTask;
    }
}
