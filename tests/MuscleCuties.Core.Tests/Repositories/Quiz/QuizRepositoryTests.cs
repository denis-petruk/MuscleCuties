using MuscleCuties.Core.Models.Entities.Cycle;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Repositories.Common;
using MuscleCuties.Core.Repositories.Cycle;
using MuscleCuties.Core.Repositories.Nutrition;
using MuscleCuties.Core.Repositories.Quiz;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Repositories.Workout;

namespace MuscleCuties.Core.Tests.Repositories.Quiz;

public class QuizRepositoryTests : IDisposable
{
    private readonly DatabaseFixture _fixture;

    public QuizRepositoryTests()
    {
        _fixture = new DatabaseFixture();
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }

    [Fact]
    public async Task AreQuestionsSeededAsync_WhenEmpty_ReturnsFalse()
    {
        var repo = new QuizRepository(_fixture.Db);

        var result = await repo.AreQuestionsSeededAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task AddRangeQuestionsAsync_ThenGetWithAnswers_ReturnsOrdered()
    {
        var repo = new QuizRepository(_fixture.Db);
        var questions = new List<QuizQuestion>
        {
            new() { Question = "Q1", OrderIndex = 1, QuestionType = QuizQuestionType.Goal,
                Answers = [new QuizAnswer { Text = "A1", OrderIndex = 1, MappedValue = 0 }] },
            new() { Question = "Q2", OrderIndex = 2, QuestionType = QuizQuestionType.ExperienceLevel,
                Answers = [new QuizAnswer { Text = "B1", OrderIndex = 1, MappedValue = 0 }] }
        };

        await repo.AddRangeQuestionsAsync(questions);
        var result = await repo.GetQuestionsWithAnswersAsync();

        Assert.True(result.Count >= 2);
        Assert.All(result, q => Assert.NotEmpty(q.Answers));
    }

    [Fact]
    public async Task GetAnswerTextAsync_ExistingAnswerId_ReturnsText()
    {
        var repo = new QuizRepository(_fixture.Db);
        var question = new QuizQuestion
        {
            Question = "How many days?",
            OrderIndex = 99,
            QuestionType = QuizQuestionType.WorkoutDaysPerWeek,
            Answers = [new QuizAnswer { Text = "3 days", OrderIndex = 1, MappedValue = 3 }]
        };
        await repo.AddAsync(question);

        var answerId = question.Answers.First().Id;
        var text = await repo.GetAnswerTextAsync(answerId);

        Assert.Equal("3 days", text);
    }

    [Fact]
    public async Task GetQuestionsWithAnswersAsync_WhenAnswerRefreshWasInterrupted_ReturnsFallbackAnswers()
    {
        var repo = new QuizRepository(_fixture.Db);
        await repo.AddRangeQuestionsAsync(
        [
            new QuizQuestion
            {
                Question = "Current cycle phase?",
                OrderIndex = 1,
                QuestionType = QuizQuestionType.CurrentCyclePhase,
                Answers =
                [
                    new QuizAnswer { Text = "Menstrual", OrderIndex = 10_000, MappedValue = 1 },
                    new QuizAnswer { Text = "Follicular", OrderIndex = 10_001, MappedValue = 2 }
                ]
            }
        ]);

        var result = await repo.GetQuestionsWithAnswersAsync();

        var phaseQuestion = result.Single(question => question.QuestionType == QuizQuestionType.CurrentCyclePhase);
        Assert.NotEmpty(phaseQuestion.Answers);
        Assert.Contains(phaseQuestion.Answers, answer => answer.Text == "Menstrual");
    }
}
