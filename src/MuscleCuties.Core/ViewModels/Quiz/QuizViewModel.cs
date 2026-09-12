using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.UI.Quiz;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Quiz;
using MuscleCuties.Core.ViewModels.Common;

namespace MuscleCuties.Core.ViewModels.Quiz;

public partial class QuizViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly Func<Task> _navigateToDashboardAsync;
    private readonly IAppPreloadService _preloadService;
    private readonly QuizQuestionCache _quizQuestionCache;
    private readonly IQuizService _quizService;
    private readonly List<(int QuestionId, int AnswerId)> _selectedAnswers = new();

    [ObservableProperty] private ObservableCollection<SelectableQuizAnswer> _currentAnswers = new();
    [ObservableProperty] private QuizQuestion? _currentQuestion;
    [ObservableProperty] private int _currentQuestionIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(QuestionsStateMessage))]
    private string _errorMessage = string.Empty;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isPreparingDashboard;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRetryQuestionsLoad))]
    [NotifyPropertyChangedFor(nameof(HasLoadedQuestions))]
    [NotifyPropertyChangedFor(nameof(HasNoQuestions))]
    [NotifyPropertyChangedFor(nameof(HasQuestion))]
    [NotifyPropertyChangedFor(nameof(IsEmptyStateVisible))]
    [NotifyPropertyChangedFor(nameof(IsLayoutVisible))]
    [NotifyPropertyChangedFor(nameof(IsLoading))]
    [NotifyPropertyChangedFor(nameof(IsLoadingVisible))]
    [NotifyPropertyChangedFor(nameof(QuestionsStateTitle))]
    [NotifyPropertyChangedFor(nameof(QuestionsStateMessage))]
    private QuizLoadState _loadState = QuizLoadState.Loading;

    [ObservableProperty] private List<QuizQuestion> _questions = new();

    public QuizViewModel(
        IAuthService authService,
        IQuizService quizService,
        IAppPreloadService preloadService,
        QuizQuestionCache quizQuestionCache,
        Func<Task> navigateToDashboardAsync)
    {
        _authService = authService;
        _quizService = quizService;
        _preloadService = preloadService;
        _quizQuestionCache = quizQuestionCache;
        _navigateToDashboardAsync = navigateToDashboardAsync;

        LoadQuestionsCommand = new AsyncRelayCommand(LoadQuestionsAsync);
        RetryLoadQuestionsCommand = new AsyncRelayCommand(RetryLoadQuestionsAsync);
        SelectAnswerCommand = new RelayCommand<SelectableQuizAnswer>(SelectAnswer);
        NextCommand = new AsyncRelayCommand(NextAsync);
        BackCommand = new RelayCommand(Back, () => !IsFirstQuestion);
    }

    public AsyncRelayCommand LoadQuestionsCommand { get; }
    public AsyncRelayCommand RetryLoadQuestionsCommand { get; }
    public RelayCommand<SelectableQuizAnswer> SelectAnswerCommand { get; }
    public AsyncRelayCommand NextCommand { get; }
    public RelayCommand BackCommand { get; }

    public QuizAnswer? FirstSelectedAnswer =>
        CurrentAnswers.FirstOrDefault(answer => answer.IsSelected)?.Answer;

    public QuizAnswer? SelectedAnswer => FirstSelectedAnswer;

    public IReadOnlyList<QuizAnswer> SelectedAnswers =>
        CurrentAnswers
            .Where(answer => answer.IsSelected)
            .Select(answer => answer.Answer)
            .ToList();

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool CanRetryQuestionsLoad => LoadState is QuizLoadState.Empty or QuizLoadState.Failed;
    public bool HasLoadedQuestions => LoadState is QuizLoadState.Ready or QuizLoadState.Empty;
    public bool HasNoQuestions => LoadState is QuizLoadState.Empty;
    public bool HasQuestion => LoadState is QuizLoadState.Ready && CurrentQuestion is not null;
    public bool IsEmptyStateVisible => LoadState is QuizLoadState.Empty or QuizLoadState.Failed;
    public bool IsLayoutVisible => LoadState is QuizLoadState.Ready;
    public bool IsLoading => LoadState is QuizLoadState.Loading;
    public bool IsLoadingVisible => IsLoading;
    public bool IsCurrentQuestionMultiAnswer => CurrentQuestion?.QuestionType is QuizQuestionType.DietaryPreference;
    public bool IsFirstQuestion => CurrentQuestionIndex == 0;
    public bool IsLastQuestion => Questions.Count > 0 && CurrentQuestionIndex == Questions.Count - 1;
    public float ProgressValue => Questions.Count == 0 ? 0f : (float)(CurrentQuestionIndex + 1) / Questions.Count;

    public string CurrentQuestionText => CurrentQuestion?.Question ?? string.Empty;
    public string NextButtonText => IsLastQuestion ? "Finish" : "Next";
    public string ProgressText => Questions.Count == 0 ? string.Empty : $"{CurrentQuestionIndex + 1} / {Questions.Count}";
    public string QuestionsStateTitle => HasNoQuestions ? "No quiz questions found" : "Questions could not load";

    public string QuestionsStateMessage => HasNoQuestions
        ? "Restart the app once so the starter quiz can seed."
        : ErrorMessage;

    public string CurrentQuestionIconGlyph => CurrentQuestion?.QuestionType switch
    {
        QuizQuestionType.Goal => "Target24",
        QuizQuestionType.ExperienceLevel => "Dumbbell24",
        QuizQuestionType.WorkoutDaysPerWeek => "CalendarWorkWeek24",
        QuizQuestionType.DietaryPreference => "Food24",
        QuizQuestionType.CurrentCyclePhase => "HeartCircle24",
        QuizQuestionType.MenstrualPain or
            QuizQuestionType.FollicularPain or
            QuizQuestionType.OvulatoryPain or
            QuizQuestionType.LutealPain => "HeartBroken24",
        QuizQuestionType.MenstrualEnergy or
            QuizQuestionType.FollicularEnergy or
            QuizQuestionType.OvulatoryEnergy or
            QuizQuestionType.LutealEnergy => "BatteryCharge24",
        _ => "CheckmarkCircle24"
    };

    public async Task EnsureQuestionsLoadedAsync()
    {
        if (HasLoadedQuestions || IsBusy)
            return;

        await LoadQuestionsAsync();
    }

    private async Task LoadQuestionsAsync()
    {
        BeginLoading();

        try
        {
            var loadedQuestions = await _quizQuestionCache.GetOrLoadAsync(
                () => DataLoadScheduler.RunAsync(_quizService.GetOnboardingQuestionsAsync));
            ApplyQuestions(loadedQuestions);
        }
        catch
        {
            ClearQuestionState();
            ErrorMessage = "Questions could not load. Please reopen this page.";
            ShowEmptyState(false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RetryLoadQuestionsAsync()
    {
        _quizQuestionCache.Clear();
        await LoadQuestionsAsync();
    }

    private void SelectAnswer(SelectableQuizAnswer? selectable)
    {
        if (selectable is null)
            return;

        ErrorMessage = string.Empty;

        if (IsCurrentQuestionMultiAnswer)
            ToggleMultiAnswer(selectable);
        else
            SelectSingleAnswer(selectable);

        NotifySelectionProperties();
    }

    private async Task NextAsync()
    {
        if (CurrentQuestion is null || IsBusy)
            return;

        var selectedAnswers = SelectedAnswers;
        if (selectedAnswers.Count == 0)
        {
            ErrorMessage = "Choose an answer to continue.";
            return;
        }

        RecordSelections(CurrentQuestion.Id, selectedAnswers.Select(answer => answer.Id));

        if (!IsLastQuestion)
        {
            MoveToQuestion(CurrentQuestionIndex + 1);
            return;
        }

        await SaveAnswersAsync();
    }

    private void Back()
    {
        if (IsFirstQuestion)
            return;

        MoveToQuestion(CurrentQuestionIndex - 1);
    }

    private void BeginLoading()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        LoadState = QuizLoadState.Loading;
    }

    private void ApplyQuestions(IEnumerable<QuizQuestion> loadedQuestions)
    {
        Questions = loadedQuestions
            .Where(question => question.Answers.Count > 0)
            .OrderBy(question => question.OrderIndex)
            .ThenBy(question => question.Id)
            .ToList();

        if (Questions.Count == 0)
        {
            ClearQuestionState();
            ShowEmptyState(true);
            return;
        }

        MoveToQuestion(0);
        LoadState = QuizLoadState.Ready;
    }

    private void ShowEmptyState(bool noQuestions)
    {
        LoadState = noQuestions ? QuizLoadState.Empty : QuizLoadState.Failed;
        NotifyComputedProperties();
    }

    private void ClearQuestionState()
    {
        Questions = [];
        CurrentQuestion = null;
        CurrentAnswers = [];
        CurrentQuestionIndex = 0;
        NotifySelectionProperties();
    }

    private void MoveToQuestion(int index)
    {
        CurrentQuestionIndex = Math.Clamp(index, 0, Questions.Count - 1);
        CurrentQuestion = Questions[CurrentQuestionIndex];
        BuildAnswers(CurrentQuestion);
        NotifyComputedProperties();
    }

    private void BuildAnswers(QuizQuestion question)
    {
        var savedAnswerIds = _selectedAnswers
            .Where(selection => selection.QuestionId == question.Id)
            .Select(selection => selection.AnswerId)
            .ToHashSet();

        CurrentAnswers = new ObservableCollection<SelectableQuizAnswer>(
            question.Answers
                .OrderBy(answer => answer.OrderIndex)
                .ThenBy(answer => answer.Id)
                .Select(answer => new SelectableQuizAnswer
                {
                    Answer = answer,
                    QuestionType = question.QuestionType,
                    IsSelected = savedAnswerIds.Contains(answer.Id)
                }));

        NotifySelectionProperties();
    }

    private void SelectSingleAnswer(SelectableQuizAnswer selectedAnswer)
    {
        foreach (var answer in CurrentAnswers)
            answer.IsSelected = ReferenceEquals(answer, selectedAnswer);
    }

    private void ToggleMultiAnswer(SelectableQuizAnswer selectedAnswer)
    {
        var isNoneAnswer = selectedAnswer.Answer.MappedValue == 0;

        if (isNoneAnswer)
        {
            foreach (var answer in CurrentAnswers)
                answer.IsSelected = false;

            selectedAnswer.IsSelected = true;
            return;
        }

        selectedAnswer.IsSelected = !selectedAnswer.IsSelected;
        foreach (var answer in CurrentAnswers.Where(answer => answer.Answer.MappedValue == 0))
            answer.IsSelected = false;
    }

    private void RecordSelections(int questionId, IEnumerable<int> answerIds)
    {
        _selectedAnswers.RemoveAll(selection => selection.QuestionId == questionId);
        _selectedAnswers.AddRange(answerIds.Select(answerId => (questionId, answerId)));
    }

    private async Task SaveAnswersAsync()
    {
        var totalStopwatch = Stopwatch.StartNew();
        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            IsPreparingDashboard = true;

            var userId = await DataLoadScheduler.RunAsync(_authService.GetCurrentUserIdAsync);
            var responses = _selectedAnswers
                .Select(selection => new UserQuizResponse
                {
                    QuizQuestionId = selection.QuestionId,
                    QuizAnswerId = selection.AnswerId
                })
                .ToList();

            var stageStopwatch = Stopwatch.StartNew();
            await DataLoadScheduler.RunAsync(() => _quizService.SaveAnswersAsync(userId, responses));
            Trace.WriteLine(
                $"[Performance][Quiz] Answers and profile saved in {stageStopwatch.ElapsedMilliseconds} ms.");

            _preloadService.InvalidateAll();
            stageStopwatch.Restart();
            await _preloadService.PreloadAllAsync();
            Trace.WriteLine(
                $"[Performance][Quiz] All pages prepared in {stageStopwatch.ElapsedMilliseconds} ms.");

            stageStopwatch.Restart();
            await _navigateToDashboardAsync();
            Trace.WriteLine(
                $"[Performance][Quiz] Dashboard navigation completed in {stageStopwatch.ElapsedMilliseconds} ms; " +
                $"total sync={totalStopwatch.ElapsedMilliseconds} ms.");
        }
        catch
        {
            IsPreparingDashboard = false;
            ErrorMessage = "We could not save your answers. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnCurrentQuestionChanged(QuizQuestion? value)
    {
        OnPropertyChanged(nameof(CurrentQuestionText));
        OnPropertyChanged(nameof(CurrentQuestionIconGlyph));
    }

    private void NotifySelectionProperties()
    {
        OnPropertyChanged(nameof(FirstSelectedAnswer));
        OnPropertyChanged(nameof(SelectedAnswer));
        OnPropertyChanged(nameof(SelectedAnswers));
    }

    private void NotifyComputedProperties()
    {
        OnPropertyChanged(nameof(IsCurrentQuestionMultiAnswer));
        OnPropertyChanged(nameof(IsFirstQuestion));
        OnPropertyChanged(nameof(IsLastQuestion));
        OnPropertyChanged(nameof(ProgressValue));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(NextButtonText));
        OnPropertyChanged(nameof(CurrentQuestionText));
        OnPropertyChanged(nameof(QuestionsStateTitle));
        OnPropertyChanged(nameof(QuestionsStateMessage));
        OnPropertyChanged(nameof(CurrentQuestionIconGlyph));
        BackCommand.NotifyCanExecuteChanged();
    }
}
