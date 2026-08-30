using NSubstitute;
using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Quiz;
using MuscleCuties.Core.ViewModels.Quiz;

namespace MuscleCuties.Core.Tests.ViewModels.Quiz;

public class QuizViewModelTests
{
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly IQuizService _quizService = Substitute.For<IQuizService>();
    private bool _navigatedToDashboard;

    private QuizViewModel CreateViewModel() =>
        new(_authService, _quizService, () => _navigatedToDashboard = true);

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

    private static List<QuizQuestion> CreateCyclePhaseQuestions() =>
    [
        new()
        {
            Id = 2,
            Question = "What phase are you in today?",
            OrderIndex = -1,
            QuestionType = QuizQuestionType.CurrentCyclePhase,
            Answers =
            [
                new QuizAnswer { Id = 4, QuestionId = 2, Text = "Menstrual", OrderIndex = 1, MappedValue = (int)CyclePhase.Menstrual },
                new QuizAnswer { Id = 5, QuestionId = 2, Text = "Ovulatory", OrderIndex = 2, MappedValue = (int)CyclePhase.Ovulatory }
            ]
        },
        new()
        {
            Id = 3,
            Question = "Goal?",
            OrderIndex = 1,
            QuestionType = QuizQuestionType.Goal,
            Answers =
            [
                new QuizAnswer { Id = 6, QuestionId = 3, Text = "Strength", OrderIndex = 1, MappedValue = 2 }
            ]
        }
    ];

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
    public async Task Next_WithoutSelection_DoesNotAdvance()
    {
        var questions = CreateQuestions(2);
        _quizService.GetOnboardingQuestionsAsync().Returns(questions);

        var vm = CreateViewModel();
        await vm.LoadQuestionsCommand.ExecuteAsync(null);

        await vm.NextCommand.ExecuteAsync(null);

        Assert.Equal(0, vm.CurrentQuestionIndex);
        Assert.Equal("Choose an answer to continue.", vm.ErrorMessage);
        Assert.True(vm.HasError);
        await _quizService.DidNotReceive().SaveAnswersAsync(Arg.Any<int>(), Arg.Any<List<UserQuizResponse>>());
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

        await _quizService.Received(1).SaveAnswersAsync(1, Arg.Is<List<UserQuizResponse>>(responses => responses.Count == 2));
        Assert.True(_navigatedToDashboard);
    }

    [Fact]
    public void BeforeFirstLoad_ShowsLoadingUntilPageStartsLoad()
    {
        var vm = CreateViewModel();

        Assert.True(vm.IsLoading);
        Assert.False(vm.HasNoQuestions);
    }

    [Fact]
    public async Task LoadQuestions_WhenEmpty_ShowsNoQuestionsStateAfterLoad()
    {
        _quizService.GetOnboardingQuestionsAsync().Returns([]);

        var vm = CreateViewModel();
        await vm.LoadQuestionsCommand.ExecuteAsync(null);

        Assert.True(vm.HasLoadedQuestions);
        Assert.False(vm.IsLoading);
        Assert.True(vm.HasNoQuestions);
    }

    [Fact]
    public async Task LoadQuestions_IncludesCurrentPhaseQuestionBeforeGoal()
    {
        var questions = CreateCyclePhaseQuestions();
        _quizService.GetOnboardingQuestionsAsync().Returns(questions);

        var vm = CreateViewModel();
        await vm.LoadQuestionsCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Questions.Count);
        Assert.Equal(QuizQuestionType.CurrentCyclePhase, vm.CurrentQuestion!.QuestionType);
    }

    [Fact]
    public async Task Next_FromCurrentPhaseQuestion_AdvancesToGoal()
    {
        var questions = CreateCyclePhaseQuestions();
        _quizService.GetOnboardingQuestionsAsync().Returns(questions);

        var vm = CreateViewModel();
        await vm.LoadQuestionsCommand.ExecuteAsync(null);

        vm.SelectAnswerCommand.Execute(vm.CurrentAnswers.Single(answer =>
            answer.Answer.MappedValue == (int)CyclePhase.Ovulatory));
        await vm.NextCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Questions.Count);
        Assert.Equal(QuizQuestionType.Goal, vm.CurrentQuestion!.QuestionType);
    }
}
