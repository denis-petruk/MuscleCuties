using Microsoft.EntityFrameworkCore;
using NSubstitute;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Repositories;
using MuscleCuties.Core.Services;

namespace MuscleCuties.Tests.Services;

public class QuizServiceTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;
    private readonly IWorkoutService _workoutService = Substitute.For<IWorkoutService>();

    public QuizServiceTests(DatabaseFixture fixture) => _fixture = fixture;

    private QuizService CreateService() =>
        new QuizService(
            new UserRepository(_fixture.Db),
            new QuizRepository(_fixture.Db),
            _workoutService);

    private async Task<User> SeedUserAsync(string email)
    {
        var user = new User { Email = email, PasswordHash = "hash", CreatedAt = DateTime.UtcNow };
        await _fixture.Db.Users.AddAsync(user);
        await _fixture.Db.SaveChangesAsync();
        return user;
    }

    private static AppDatabase CreateFreshDb()
    {
        var options = new DbContextOptionsBuilder<AppDatabase>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        var db = new AppDatabase(options);
        db.Database.OpenConnection();
        return db;
    }

    // --- QuizService behaviour tests (use shared fixture DB) ---

    [Fact]
    public async Task IsOnboardingCompleteAsync_NewUser_ReturnsFalse()
    {
        var user = await SeedUserAsync("quiz1@test.com");
        var result = await CreateService().IsOnboardingCompleteAsync(user.Id);
        Assert.False(result);
    }

    [Fact]
    public async Task SaveAnswersAsync_SetsOnboardingComplete()
    {
        var user = await SeedUserAsync("quiz2@test.com");
        var question = new QuizQuestion
        {
            Question = "Goal?", OrderIndex = 1,
            QuestionType = QuizQuestionType.Goal,
            Answers = [new QuizAnswer { Text = "Lose fat", OrderIndex = 1, MappedValue = (int)UserGoal.FatLoss }]
        };
        await _fixture.Db.QuizQuestions.AddAsync(question);
        await _fixture.Db.SaveChangesAsync();

        await CreateService().SaveAnswersAsync(user.Id, [new UserQuizResponse
        {
            QuizQuestionId = question.Id,
            QuizAnswerId = question.Answers.First().Id
        }]);

        Assert.True(await CreateService().IsOnboardingCompleteAsync(user.Id));
    }

    [Fact]
    public async Task SaveAnswersAsync_GoalQuestion_SetsProfileGoal()
    {
        var user = await SeedUserAsync("quiz3@test.com");
        var question = new QuizQuestion
        {
            Question = "What is your goal?", OrderIndex = 2,
            QuestionType = QuizQuestionType.Goal,
            Answers = [new QuizAnswer { Text = "Strength", OrderIndex = 1, MappedValue = (int)UserGoal.Strength }]
        };
        await _fixture.Db.QuizQuestions.AddAsync(question);
        await _fixture.Db.SaveChangesAsync();

        await CreateService().SaveAnswersAsync(user.Id, [new UserQuizResponse
        {
            QuizQuestionId = question.Id,
            QuizAnswerId = question.Answers.First().Id
        }]);

        var profile = await new UserRepository(_fixture.Db).GetProfileAsync(user.Id);
        Assert.Equal(UserGoal.Strength, profile!.Goal);
    }

    [Fact]
    public async Task SaveAnswersAsync_WritesUserProfileSnapshot()
    {
        var user = await SeedUserAsync("quiz4@test.com");
        var question = new QuizQuestion
        {
            Question = "Goal?", OrderIndex = 3,
            QuestionType = QuizQuestionType.Goal,
            Answers = [new QuizAnswer { Text = "Maintain", OrderIndex = 1, MappedValue = (int)UserGoal.MaintainHealth }]
        };
        await _fixture.Db.QuizQuestions.AddAsync(question);
        await _fixture.Db.SaveChangesAsync();

        await CreateService().SaveAnswersAsync(user.Id, [new UserQuizResponse
        {
            QuizQuestionId = question.Id,
            QuizAnswerId = question.Answers.First().Id
        }]);

        var snapshot = await new UserRepository(_fixture.Db).GetLatestSnapshotAsync(user.Id);
        Assert.NotNull(snapshot);
        Assert.Equal("Initial", snapshot.SnapshotReason);
        Assert.False(string.IsNullOrEmpty(snapshot.ProfileJson));
    }

    [Fact]
    public async Task SaveAnswers_CallsGenerateUserPlans()
    {
        var user = await SeedUserAsync("quiz5@test.com");
        var question = new QuizQuestion
        {
            Question = "Goal?", OrderIndex = 4,
            QuestionType = QuizQuestionType.Goal,
            Answers = [new QuizAnswer { Text = "Strength", OrderIndex = 1, MappedValue = (int)UserGoal.Strength }]
        };
        await _fixture.Db.QuizQuestions.AddAsync(question);
        await _fixture.Db.SaveChangesAsync();

        await CreateService().SaveAnswersAsync(user.Id, [new UserQuizResponse
        {
            QuizQuestionId = question.Id,
            QuizAnswerId = question.Answers.First().Id
        }]);

        await _workoutService.Received(1).GenerateUserPlansAsync(user.Id, Arg.Any<UserGoal>(), Arg.Any<int>());
    }

    // --- Seeding tests (each needs a clean, isolated DB) ---

    [Fact]
    public async Task AreQuestionsSeededAsync_ReturnsFalse_WhenNoQuestionsExist()
    {
        using var db = CreateFreshDb();
        db.Database.EnsureCreated();
        Assert.False(await db.AreQuestionsSeededAsync());
    }

    [Fact]
    public async Task SeedQuizAsync_Seeds15Questions()
    {
        using var db = CreateFreshDb();
        db.Database.EnsureCreated();
        await db.SeedQuizAsync();
        Assert.Equal(15, await db.QuizQuestions.CountAsync());
    }

    [Fact]
    public async Task SeedQuizAsync_IsIdempotent()
    {
        using var db = CreateFreshDb();
        db.Database.EnsureCreated();
        await db.SeedQuizAsync();
        await db.SeedQuizAsync();
        Assert.Equal(15, await db.QuizQuestions.CountAsync());
    }
}
