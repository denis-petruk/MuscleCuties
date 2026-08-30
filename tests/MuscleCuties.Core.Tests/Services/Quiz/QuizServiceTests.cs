using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Repositories.Quiz;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Quiz;

namespace MuscleCuties.Core.Tests.Services.Quiz;

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
    public async Task SaveAnswersAsync_CompletesOnboardingAfterQuiz()
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

    [Fact]
    public async Task SaveAnswersAsync_MapsOnboardingAnswersToProfileAndSnapshot()
    {
        var user = await SeedUserAsync("quiz5@test.com");
        var service = CreateService();

        var questions = new List<QuizQuestion>
        {
            new()
            {
                Question = "Goal?",
                OrderIndex = 30,
                QuestionType = QuizQuestionType.Goal,
                Answers = [new QuizAnswer { Text = "Strength", OrderIndex = 1, MappedValue = (int)UserGoal.Strength }]
            },
            new()
            {
                Question = "Experience?",
                OrderIndex = 31,
                QuestionType = QuizQuestionType.ExperienceLevel,
                Answers = [new QuizAnswer { Text = "Intermediate", OrderIndex = 1, MappedValue = (int)TrainingExperienceLevel.Intermediate }]
            },
            new()
            {
                Question = "Training days?",
                OrderIndex = 32,
                QuestionType = QuizQuestionType.WorkoutDaysPerWeek,
                Answers = [new QuizAnswer { Text = "5 days", OrderIndex = 1, MappedValue = 5 }]
            },
            new()
            {
                Question = "Diet?",
                OrderIndex = 33,
                QuestionType = QuizQuestionType.DietaryPreference,
                Answers = [new QuizAnswer { Text = "Vegetarian", OrderIndex = 1, MappedValue = (int)DietaryTag.Vegetarian }]
            },
            new()
            {
                Question = "Menstrual pain?",
                OrderIndex = 34,
                QuestionType = QuizQuestionType.MenstrualPain,
                Answers = [new QuizAnswer { Text = "High", OrderIndex = 1, MappedValue = 4 }]
            },
            new()
            {
                Question = "Menstrual energy?",
                OrderIndex = 35,
                QuestionType = QuizQuestionType.MenstrualEnergy,
                Answers = [new QuizAnswer { Text = "Low", OrderIndex = 1, MappedValue = 2 }]
            }
        };
        await _fixture.Db.QuizQuestions.AddRangeAsync(questions);
        await _fixture.Db.SaveChangesAsync();

        await service.SaveAnswersAsync(user.Id, questions.Select(question => new UserQuizResponse
        {
            QuizQuestionId = question.Id,
            QuizAnswerId = question.Answers.First().Id
        }).ToList());

        var profile = await new UserRepository(_fixture.Db).GetProfileAsync(user.Id);
        Assert.NotNull(profile);
        Assert.Equal(UserGoal.Strength, profile.Goal);
        Assert.Equal(TrainingExperienceLevel.Intermediate, profile.TrainingExperienceLevel);
        Assert.Equal(CycleTrackingMode.ManualPhaseLogging, profile.CycleTrackingMode);
        Assert.Equal(5, profile.WorkoutDaysPerWeek);
        Assert.Equal(28, profile.CycleLength);
        Assert.Equal("Vegetarian", profile.DietaryTags);

        var snapshot = await new UserRepository(_fixture.Db).GetLatestSnapshotAsync(user.Id);
        Assert.NotNull(snapshot);

        using var document = JsonDocument.Parse(snapshot.ProfileJson);
        var root = document.RootElement;
        Assert.Equal("Intermediate", root.GetProperty("TrainingExperienceLevel").GetString());
        Assert.Equal(4, root.GetProperty("CyclePhaseBaselines").GetProperty("Menstrual").GetProperty("Pain").GetInt32());
        Assert.Equal(2, root.GetProperty("CyclePhaseBaselines").GetProperty("Menstrual").GetProperty("Energy").GetInt32());
        Assert.Equal(6, root.GetProperty("QuizResponses").GetArrayLength());

        var savedResponses = await _fixture.Db.UserQuizResponses
            .Where(response => response.UserId == user.Id)
            .ToListAsync();
        Assert.Equal(6, savedResponses.Count);
        Assert.All(savedResponses, response => Assert.Equal(snapshot.Id, response.UserProfileSnapshotId));
    }

    [Fact]
    public async Task SaveAnswersAsync_CurrentCyclePhase_StoresManualPhaseOnProfile()
    {
        var user = await SeedUserAsync("quiz-manual-phase@test.com");
        var service = CreateService();

        var phaseQuestion = new QuizQuestion
        {
            Question = "Current phase?",
            OrderIndex = 201,
            QuestionType = QuizQuestionType.CurrentCyclePhase,
            Answers =
            [
                new QuizAnswer { Text = "Ovulatory", OrderIndex = 1, MappedValue = (int)CyclePhase.Ovulatory }
            ]
        };
        await _fixture.Db.QuizQuestions.AddAsync(phaseQuestion);
        await _fixture.Db.SaveChangesAsync();

        await service.SaveAnswersAsync(user.Id,
        [
            new UserQuizResponse
            {
                QuizQuestionId = phaseQuestion.Id,
                QuizAnswerId = phaseQuestion.Answers.First().Id
            }
        ]);

        var profile = await new UserRepository(_fixture.Db).GetProfileAsync(user.Id);
        Assert.Equal(CycleTrackingMode.ManualPhaseLogging, profile!.CycleTrackingMode);
        Assert.Equal(CyclePhase.Ovulatory, profile.CurrentCyclePhase);
    }
}
