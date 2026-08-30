using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Repositories.Quiz;

namespace MuscleCuties.Core.Tests.Data;

public class AppDatabaseInitializationTests
{
    [Fact]
    public async Task InitializeAsync_CreatesDatabaseAndSeedsStartupData()
    {
        await using var db = await CreateDatabaseAsync();

        await db.InitializeAsync();

        Assert.Equal(13, await db.QuizQuestions.CountAsync());
        Assert.Equal(17, await db.MealTemplates.CountAsync(t => t.IsSystem));
        Assert.True(await db.FoodItems.CountAsync() >= 32);
        Assert.True(await db.MealTemplates.AnyAsync(t => t.Name == "Margherita Pizza Beans"));
        Assert.True(await db.MealTemplates.AnyAsync(t => t.Name == "Vegan Pizza Beans"));
        Assert.True(await db.MealTemplates.AnyAsync(t => t.Name == "Pepperoni Pizza Beans"));
        Assert.True(await db.MealTemplates.AnyAsync(t => t.Name == "Gluten-Free Pizza Beans"));
        Assert.True(await db.FoodItems.AnyAsync(f => f.Name == "Carrot, raw"));
        Assert.True(await db.FoodItems.AnyAsync(f => f.Name == "Olive oil"));
        Assert.True(await db.FoodItems.AllAsync(f => f.DataType == "Starter"));
        Assert.True(await db.Exercises.CountAsync() >= 20);
        Assert.True(await db.Exercises.AllAsync(e => e.Code != string.Empty));
    }

    [Fact]
    public async Task InitializeAsync_WhenCalledTwice_DoesNotDuplicateStartupData()
    {
        await using var db = await CreateDatabaseAsync();

        await db.InitializeAsync();
        await db.InitializeAsync();

        Assert.Equal(13, await db.QuizQuestions.CountAsync());
        Assert.Equal(17, await db.MealTemplates.CountAsync(t => t.IsSystem));
        Assert.Equal(32, await db.FoodItems.CountAsync());
    }

    [Fact]
    public async Task ResetAndSeedDebugDatabaseAsync_RecreatesReferenceData()
    {
        await using var db = await CreateDatabaseAsync();
        await db.InitializeAsync();
        await db.Users.AddAsync(new()
        {
            Email = "debug-reset@test.com",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        await db.ResetAndSeedDebugDatabaseAsync();

        Assert.Empty(await db.Users.ToListAsync());
        Assert.Equal(13, await db.QuizQuestions.CountAsync());
        Assert.Equal(17, await db.MealTemplates.CountAsync(t => t.IsSystem));
        Assert.Equal(32, await db.FoodItems.CountAsync());
        Assert.True(await db.Exercises.CountAsync() >= 20);
    }

    [Fact]
    public async Task InitializeAsync_WhenOldQuizSeedExists_AddsCurrentPhaseQuestionWithoutConnectors()
    {
        await using var db = await CreateDatabaseAsync();
        await db.Database.EnsureCreatedAsync();
        await db.QuizQuestions.AddAsync(new QuizQuestion
        {
            Question = "What is your primary fitness goal?",
            OrderIndex = 1,
            QuestionType = QuizQuestionType.Goal,
            Answers =
            [
                new() { Text = "Maintain health", OrderIndex = 1, MappedValue = 3 }
            ]
        });
        await db.SaveChangesAsync();

        await db.InitializeAsync();

        Assert.True(await db.QuizQuestions.AnyAsync(q => q.QuestionType == QuizQuestionType.CurrentCyclePhase));
        Assert.False(await db.QuizQuestions.AnyAsync(q => q.QuestionType == QuizQuestionType.CycleTrackingMode));
        Assert.Equal(13, await db.QuizQuestions.Select(q => q.QuestionType).Distinct().CountAsync());
    }

    [Fact]
    public async Task InitializeAsync_WhenOldCycleTrackingQuestionExists_HidesItFromActiveOnboarding()
    {
        await using var db = await CreateDatabaseAsync();
        await db.Database.EnsureCreatedAsync();
        await db.QuizQuestions.AddAsync(new QuizQuestion
        {
            Question = "How would you like to track your cycle?",
            OrderIndex = -2,
            QuestionType = QuizQuestionType.CycleTrackingMode,
            Answers =
            [
                new() { Text = "Automatic", OrderIndex = 1, MappedValue = (int)CycleTrackingMode.AutomaticPrediction },
                new() { Text = "Flo", OrderIndex = 2, MappedValue = (int)CycleTrackingMode.FloConnector },
                new() { Text = "Manual", OrderIndex = 3, MappedValue = (int)CycleTrackingMode.ManualPhaseLogging }
            ]
        });
        await db.SaveChangesAsync();

        await db.InitializeAsync();

        var onboardingQuestions = await new QuizRepository(db).GetQuestionsWithAnswersAsync();
        Assert.DoesNotContain(onboardingQuestions, question => question.QuestionType == QuizQuestionType.CycleTrackingMode);
    }

    [Fact]
    public async Task InitializeAsync_WhenExerciseCodeIsBlank_RepairsCode()
    {
        await using var db = await CreateDatabaseAsync();
        await db.Database.EnsureCreatedAsync();
        await db.Exercises.AddAsync(new()
        {
            Code = string.Empty,
            Name = "Legacy Move",
            Description = "Legacy exercise"
        });
        await db.SaveChangesAsync();

        await db.InitializeAsync();

        var exercise = await db.Exercises.SingleAsync(e => e.Name == "Legacy Move");
        Assert.Equal("LEGACY_MOVE", exercise.Code);
    }

    [Fact]
    public async Task InitializeAsync_WhenStarterFoodExistsWithZeroNutrition_RepairsNutrition()
    {
        await using var db = await CreateDatabaseAsync();
        await db.Database.EnsureCreatedAsync();
        await db.FoodItems.AddAsync(new()
        {
            Name = "Olive oil",
            FdcId = 172187,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        await db.InitializeAsync();

        var oliveOil = await db.FoodItems.SingleAsync(f => f.Name == "Olive oil");
        Assert.Equal(884f, oliveOil.Calories);
        Assert.Equal(100f, oliveOil.Fats);
        Assert.Equal("Starter", oliveOil.DataType);
        Assert.Equal(32, await db.FoodItems.CountAsync());
    }

    private static async Task<AppDatabase> CreateDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<AppDatabase>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        var db = new AppDatabase(options);
        await db.Database.OpenConnectionAsync();
        return db;
    }
}
