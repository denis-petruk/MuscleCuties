using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Repositories;
using MuscleCuties.Core.Services;

namespace MuscleCuties.Core.Tests.Services;

public class QuizServiceTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public QuizServiceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private QuizService CreateService() =>
        new QuizService(
            new UserRepository(_fixture.Db),
            new QuizRepository(_fixture.Db));

    private async Task<User> SeedUserAsync(string email)
    {
        var user = new User { Email = email, PasswordHash = "hash", CreatedAt = DateTime.UtcNow };
        await _fixture.Db.Users.AddAsync(user);
        await _fixture.Db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task IsOnboardingCompleteAsync_NewUser_ReturnsFalse()
    {
        var user = await SeedUserAsync("quiz1@test.com");
        var service = CreateService();

        var result = await service.IsOnboardingCompleteAsync(user.Id);

        Assert.False(result);
    }

    [Fact]
    public async Task SaveAnswersAsync_SetsOnboardingComplete()
    {
        var user = await SeedUserAsync("quiz2@test.com");
        var service = CreateService();

        var question = new QuizQuestion
        {
            Question = "Goal?",
            OrderIndex = 1,
            QuestionType = QuizQuestionType.Goal,
            Answers = [new QuizAnswer { Text = "Lose fat", OrderIndex = 1, MappedValue = (int)UserGoal.FatLoss }]
        };
        await _fixture.Db.QuizQuestions.AddAsync(question);
        await _fixture.Db.SaveChangesAsync();

        var response = new UserQuizResponse
        {
            QuizQuestionId = question.Id,
            QuizAnswerId = question.Answers.First().Id
        };

        await service.SaveAnswersAsync(user.Id, [response]);

        Assert.True(await service.IsOnboardingCompleteAsync(user.Id));
    }

    [Fact]
    public async Task SaveAnswersAsync_GoalQuestion_SetsProfileGoal()
    {
        var user = await SeedUserAsync("quiz3@test.com");
        var service = CreateService();

        var question = new QuizQuestion
        {
            Question = "What is your goal?",
            OrderIndex = 2,
            QuestionType = QuizQuestionType.Goal,
            Answers = [new QuizAnswer { Text = "Strength", OrderIndex = 1, MappedValue = (int)UserGoal.Strength }]
        };
        await _fixture.Db.QuizQuestions.AddAsync(question);
        await _fixture.Db.SaveChangesAsync();

        await service.SaveAnswersAsync(user.Id, [new UserQuizResponse
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
        var service = CreateService();

        var question = new QuizQuestion
        {
            Question = "Goal?",
            OrderIndex = 3,
            QuestionType = QuizQuestionType.Goal,
            Answers = [new QuizAnswer { Text = "Maintain", OrderIndex = 1, MappedValue = (int)UserGoal.MaintainHealth }]
        };
        await _fixture.Db.QuizQuestions.AddAsync(question);
        await _fixture.Db.SaveChangesAsync();

        await service.SaveAnswersAsync(user.Id, [new UserQuizResponse
        {
            QuizQuestionId = question.Id,
            QuizAnswerId = question.Answers.First().Id
        }]);

        var snapshot = await new UserRepository(_fixture.Db).GetLatestSnapshotAsync(user.Id);
        Assert.NotNull(snapshot);
        Assert.Equal("Initial", snapshot.SnapshotReason);
        Assert.False(string.IsNullOrEmpty(snapshot.ProfileJson));
    }
}