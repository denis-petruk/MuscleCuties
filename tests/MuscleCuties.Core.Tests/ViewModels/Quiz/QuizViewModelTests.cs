using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Quiz;
using MuscleCuties.Core.ViewModels.Quiz;
using NSubstitute;

namespace MuscleCuties.Core.Tests.ViewModels.Quiz;

public class QuizViewModelTests
{
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly IAppPreloadService _preloadService = Substitute.For<IAppPreloadService>();
    private readonly IQuizService _quizService = Substitute.For<IQuizService>();
    private bool _navigatedToDashboard;

    private QuizViewModel CreateViewModel()
    {
        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(IAuthService)).Returns(_authService);
        services.GetService(typeof(IQuizService)).Returns(_quizService);
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(services);
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        return new QuizViewModel(
            scopeFactory,
            _preloadService,
            new QuizQuestionCache(),
            () =>
            {
                _navigatedToDashboard = true;
                return Task.CompletedTask;
            });
    }

    private static List<QuizQuestion> CreateQuestions(int count)
    {
        var questions = new List<QuizQuestion>();
        for (var i = 0; i < count; i++)
            questions.Add(new QuizQuestion
            {
                Id = i + 1,
                Question = $"Question {i + 1}",
                OrderIndex = i,
                Answers = new List<QuizAnswer>
                {
                    new() { Id = i * 2 + 1, QuestionId = i + 1, Text = "Answer A", MappedValue = 1 },
                    new() { Id = i * 2 + 2, QuestionId = i + 1, Text = "Answer B", MappedValue = 2 }
                }
            });
        return questions;
    }

    private static List<QuizQuestion> CreateCyclePhaseQuestions()
    {
        return
        [
            new QuizQuestion
            {
                Id = 2,
                Question = "What phase are you in today?",
                OrderIndex = -1,
                QuestionType = QuizQuestionType.CurrentCyclePhase,
                Answers =
                [
                    new QuizAnswer
                    {
                        Id = 4, QuestionId = 2, Text = "Menstrual", OrderIndex = 1,
                        MappedValue = (int)CyclePhase.Menstrual
                    },
                    new QuizAnswer
                    {
                        Id = 5, QuestionId = 2, Text = "Ovulatory", OrderIndex = 2,
                        MappedValue = (int)CyclePhase.Ovulatory
                    }
                ]
            },
            new QuizQuestion
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

        await _quizService.Received(1)
            .SaveAnswersAsync(1, Arg.Is<List<UserQuizResponse>>(responses => responses.Count == 2));
        Assert.True(_navigatedToDashboard);
    }

    [Fact]
    public void BeforeFirstLoad_ShowsLoadingUntilPageStartsLoad()
    {
        var vm = CreateViewModel();

        Assert.True(vm.IsLoading);
        Assert.False(vm.HasNoQuestions);
        Assert.False(vm.IsLayoutVisible);
        Assert.True(vm.IsLoadingVisible);
        Assert.False(vm.IsEmptyStateVisible);
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
        Assert.False(vm.IsLayoutVisible);
        Assert.False(vm.IsLoadingVisible);
        Assert.True(vm.IsEmptyStateVisible);
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
        Assert.True(vm.IsLayoutVisible);
        Assert.False(vm.IsLoadingVisible);
        Assert.False(vm.IsEmptyStateVisible);
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

    [Fact]
    public async Task Next_FromMaintainGoal_SkipsGoalPace()
    {
        var questions = CreateGoalPaceQuestions();
        _quizService.GetOnboardingQuestionsAsync().Returns(questions);

        var vm = CreateViewModel();
        await vm.LoadQuestionsCommand.ExecuteAsync(null);
        vm.SelectAnswerCommand.Execute(vm.CurrentAnswers.Single(answer =>
            answer.Answer.MappedValue == (int)UserGoal.MaintainHealth));

        await vm.NextCommand.ExecuteAsync(null);

        Assert.Equal(QuizQuestionType.ExperienceLevel, vm.CurrentQuestion!.QuestionType);
        Assert.Equal("2 / 2", vm.ProgressText);
    }

    [Fact]
    public async Task Next_FromStrengthGoal_ShowsGoalPace()
    {
        var questions = CreateGoalPaceQuestions();
        _quizService.GetOnboardingQuestionsAsync().Returns(questions);

        var vm = CreateViewModel();
        await vm.LoadQuestionsCommand.ExecuteAsync(null);
        vm.SelectAnswerCommand.Execute(vm.CurrentAnswers.Single(answer =>
            answer.Answer.MappedValue == (int)UserGoal.Strength));

        await vm.NextCommand.ExecuteAsync(null);

        Assert.Equal(QuizQuestionType.GoalPace, vm.CurrentQuestion!.QuestionType);
        Assert.Equal("2 / 3", vm.ProgressText);
    }

    [Fact]
    public async Task PhasePair_RecordsPainAndEnergyAsTwoResponses()
    {
        var questions = CreatePhasePairQuestions();
        _quizService.GetOnboardingQuestionsAsync().Returns(questions);
        _authService.GetCurrentUserIdAsync().Returns(1);

        var vm = CreateViewModel();
        await vm.LoadQuestionsCommand.ExecuteAsync(null);
        vm.PainSliderValue = 1;
        vm.EnergySliderValue = 4;

        await vm.NextCommand.ExecuteAsync(null);

        await _quizService.Received(1).SaveAnswersAsync(1,
            Arg.Is<List<UserQuizResponse>>(responses =>
                responses.Count == 2 &&
                responses.Any(response => response.QuizAnswerId == 15) &&
                responses.Any(response => response.QuizAnswerId == 24)));
        Assert.True(vm.IsCurrentQuestionPhasePair);
        Assert.Equal("Severe", vm.PainSliderLabel);
        Assert.Equal("Strong", vm.EnergySliderLabel);
    }

    private static List<QuizQuestion> CreateGoalPaceQuestions()
    {
        return
        [
            new QuizQuestion
            {
                Id = 10,
                OrderIndex = 1,
                QuestionType = QuizQuestionType.Goal,
                Question = "Goal?",
                Answers =
                [
                    new QuizAnswer { Id = 101, Text = "Strength", MappedValue = (int)UserGoal.Strength },
                    new QuizAnswer { Id = 102, Text = "Maintain", MappedValue = (int)UserGoal.MaintainHealth }
                ]
            },
            new QuizQuestion
            {
                Id = 11,
                OrderIndex = 2,
                QuestionType = QuizQuestionType.GoalPace,
                Question = "How fast?",
                Answers =
                [
                    new QuizAnswer { Id = 111, Text = "Steady", MappedValue = (int)WeightGoalPace.Steady },
                    new QuizAnswer { Id = 112, Text = "Aggressive", MappedValue = (int)WeightGoalPace.Aggressive }
                ]
            },
            new QuizQuestion
            {
                Id = 12,
                OrderIndex = 3,
                QuestionType = QuizQuestionType.ExperienceLevel,
                Question = "Experience?",
                Answers = [new QuizAnswer { Id = 121, Text = "Beginner", MappedValue = 1 }]
            }
        ];
    }

    private static List<QuizQuestion> CreatePhasePairQuestions()
    {
        return
        [
            new QuizQuestion
            {
                Id = 20,
                OrderIndex = 1,
                QuestionType = QuizQuestionType.MenstrualPain,
                Question = "Pain?",
                Answers = Enumerable.Range(1, 5)
                    .Select(value => new QuizAnswer { Id = 10 + value, Text = $"Pain {value}", MappedValue = value })
                    .ToList()
            },
            new QuizQuestion
            {
                Id = 21,
                OrderIndex = 2,
                QuestionType = QuizQuestionType.MenstrualEnergy,
                Question = "Energy?",
                Answers = Enumerable.Range(1, 5)
                    .Select(value => new QuizAnswer { Id = 20 + value, Text = $"Energy {value}", MappedValue = value })
                    .ToList()
            }
        ];
    }
}
