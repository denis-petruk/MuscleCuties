using NSubstitute;
using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.ViewModels;

namespace MuscleCuties.Core.Tests.ViewModels;

public class QuizViewModelTests
{
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly IQuizService _quizService = Substitute.For<IQuizService>();
    private bool _navigatedToProfileSetup;

    private QuizViewModel CreateViewModel() =>
        new(_authService, _quizService, () => _navigatedToProfileSetup = true);

    private static List<QuizQuestion> CreateQuestions(int count)
    {
        var questions = new List<QuizQuestion>();
        for (int i = 0; i < count; i++)
        {
            questions.Add(new QuizQuestion
            {
                Id = i + 1,
                Question = $"Question {i + 1}",
                OrderIndex = i,
                Answers = new List<QuizAnswer>
                {
                    new QuizAnswer { Id = (i * 2) + 1, QuestionId = i + 1, Text = "Answer A", MappedValue = 1 },
                    new QuizAnswer { Id = (i * 2) + 2, QuestionId = i + 1, Text = "Answer B", MappedValue = 2 }
                }
            });
        }
        return questions;
    }

    [Fact]
    public async Task LoadQuestions_PopulatesQuestions_SetsCurrentQuestion()
    {
        var questions = CreateQuestions(3);
        _quizService.GetOnboardingQuestionsAsync().Returns(questions);

        var vm = CreateViewModel();
        await vm.LoadQuestionsCommand.ExecuteAsync(null);

        Assert.Equal(3, vm.Questions.Count);
        Assert.Equal(questions[0], vm.CurrentQuestion);
        Assert.Equal(0, vm.CurrentQuestionIndex);
    }

    [Fact]
    public async Task SelectAnswer_SetsSelectedAnswer()
    {
        var questions = CreateQuestions(1);
        _quizService.GetOnboardingQuestionsAsync().Returns(questions);

        var vm = CreateViewModel();
        await vm.LoadQuestionsCommand.ExecuteAsync(null);

        var selectable = vm.CurrentAnswers.First();
        vm.SelectAnswerCommand.Execute(selectable);

        Assert.Equal(selectable.Answer, vm.SelectedAnswer);
        Assert.True(selectable.IsSelected);
    }

    [Fact]
    public async Task Next_NotLastQuestion_AdvancesIndex()
    {
        var questions = CreateQuestions(3);
        _quizService.GetOnboardingQuestionsAsync().Returns(questions);

        var vm = CreateViewModel();
        await vm.LoadQuestionsCommand.ExecuteAsync(null);

        vm.SelectAnswerCommand.Execute(vm.CurrentAnswers.First());
        await vm.NextCommand.ExecuteAsync(null);

        Assert.Equal(1, vm.CurrentQuestionIndex);
        Assert.Equal(questions[1], vm.CurrentQuestion);
        Assert.Null(vm.SelectedAnswer);
    }

    [Fact]
    public async Task Next_LastQuestion_SavesAndNavigates()
    {
        var questions = CreateQuestions(2);
        _quizService.GetOnboardingQuestionsAsync().Returns(questions);
        _authService.GetCurrentUserIdAsync().Returns(1);

        var vm = CreateViewModel();
        await vm.LoadQuestionsCommand.ExecuteAsync(null);

        vm.SelectAnswerCommand.Execute(vm.CurrentAnswers.First());
        await vm.NextCommand.ExecuteAsync(null);

        vm.SelectAnswerCommand.Execute(vm.CurrentAnswers.First());
        await vm.NextCommand.ExecuteAsync(null);

        await _quizService.Received(1).SaveAnswersAsync(1, Arg.Any<List<UserQuizResponse>>());
        Assert.True(_navigatedToProfileSetup);
    }
}
