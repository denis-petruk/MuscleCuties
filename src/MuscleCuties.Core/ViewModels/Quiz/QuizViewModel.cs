using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Diagnostics;
using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.UI.Quiz;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Quiz;

namespace MuscleCuties.Core.ViewModels.Quiz;

public partial class QuizViewModel : ObservableObject
{
    private const int QuestionLoadTimeoutSeconds = 10;

    private readonly IAuthService _authService;
    private readonly IQuizService _quizService;
    private readonly Action _navigateToDashboard;
    private readonly List<(int QuestionId, int AnswerId)> _selectedAnswers = new();
    private QuizLoadState _loadState = QuizLoadState.NotStarted;

    [ObservableProperty]
    private List<QuizQuestion> _questions = new();

    [ObservableProperty] private QuizQuestion? _currentQuestion;
    [ObservableProperty] private int _currentQuestionIndex;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = string.Empty;

    [ObservableProperty] private ObservableCollection<SelectableQuizAnswer> _currentAnswers = new();
    [ObservableProperty] private bool _hasQuestion;
    [ObservableProperty] private bool _hasLoadedQuestions;
    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private bool _hasNoQuestions;
    [ObservableProperty] private bool _canRetryQuestionsLoad;

    public QuizAnswer? FirstSelectedAnswer =>
        CurrentAnswers.FirstOrDefault(a => a.IsSelected)?.Answer;
    public QuizAnswer? SelectedAnswer => FirstSelectedAnswer;
    public IReadOnlyList<QuizAnswer> SelectedAnswers =>
        CurrentAnswers
            .Where(answer => answer.IsSelected)
            .Select(answer => answer.Answer)
            .ToList();

    public bool IsCurrentQuestionMultiAnswer => CurrentQuestion?.QuestionType is QuizQuestionType.DietaryPreference;
    public bool IsFirstQuestion => CurrentQuestionIndex == 0;
    public bool IsLastQuestion => Questions.Count > 0 && CurrentQuestionIndex == Questions.Count - 1;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public float ProgressValue => Questions.Count > 0 ? (float)(CurrentQuestionIndex + 1) / Questions.Count : 0f;
    public string ProgressText => Questions.Count > 0
        ? $"{CurrentQuestionIndex + 1} / {Questions.Count}"
        : string.Empty;
    public string NextButtonText => IsLastQuestion ? "Finish" : "Next";
    public string CurrentQuestionText => CurrentQuestion?.Question ?? string.Empty;
    public string QuestionsStateTitle => _loadState == QuizLoadState.Failed
        ? "Questions could not load"
        : "No quiz questions found";
    public string QuestionsStateMessage => _loadState == QuizLoadState.Failed
        ? ErrorMessage
        : "Tap Try again so the starter quiz can refresh.";
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

    public AsyncRelayCommand LoadQuestionsCommand { get; }
    public AsyncRelayCommand RetryLoadQuestionsCommand { get; }
    public RelayCommand<SelectableQuizAnswer> SelectAnswerCommand { get; }
    public AsyncRelayCommand NextCommand { get; }
    public RelayCommand BackCommand { get; }

    public QuizViewModel(
        IAuthService authService,
        IQuizService quizService,
        Action navigateToDashboard)
    {
        _authService = authService;
        _quizService = quizService;
        _navigateToDashboard = navigateToDashboard;
        LoadQuestionsCommand = new AsyncRelayCommand(LoadQuestionsAsync);
        RetryLoadQuestionsCommand = new AsyncRelayCommand(LoadQuestionsAsync);
        SelectAnswerCommand = new RelayCommand<SelectableQuizAnswer>(SelectAnswer);
        NextCommand = new AsyncRelayCommand(NextAsync);
        BackCommand = new RelayCommand(Back, () => !IsFirstQuestion);
    }

    public async Task EnsureQuestionsLoadedAsync()
    {
        AppDebugLog.Write("QuizVM", $"EnsureQuestionsLoaded state={_loadState}, isBusy={IsBusy}.");
        if (_loadState is QuizLoadState.Loading or QuizLoadState.Ready)
        {
            AppDebugLog.Write("QuizVM", "EnsureQuestionsLoaded skipped.");
            return;
        }

        await LoadQuestionsAsync();
    }

    private async Task LoadQuestionsAsync()
    {
        AppDebugLog.Write("QuizVM", "LoadQuestions start.");
        IsBusy = true;
        SetLoadState(QuizLoadState.Loading);
        ErrorMessage = string.Empty;
        try
        {
            AppDebugLog.Write("QuizVM", "Requesting onboarding questions from service.");
            var loaded = await _quizService
                .GetOnboardingQuestionsAsync()
                .WaitAsync(TimeSpan.FromSeconds(QuestionLoadTimeoutSeconds));
            AppDebugLog.Write("QuizVM", $"Service returned {loaded.Count} questions.");

            Questions = loaded
                .Where(question => question.Answers.Count > 0)
                .OrderBy(question => question.OrderIndex)
                .ThenBy(question => question.Id)
                .ToList();
            AppDebugLog.Write("QuizVM", $"Usable questions after answer filter: {Questions.Count}.");

            CurrentQuestionIndex = 0;
            CurrentQuestion = Questions.FirstOrDefault();
            if (CurrentQuestion != null)
            {
                AppDebugLog.Write(
                    "QuizVM",
                    $"Current question id={CurrentQuestion.Id}, type={CurrentQuestion.QuestionType}, answers={CurrentQuestion.Answers.Count}.");
                BuildAnswers(CurrentQuestion);
            }
            else
            {
                AppDebugLog.Write("QuizVM", "No current question. Moving to empty state.");
                CurrentAnswers = [];
            }

            SetLoadState(CurrentQuestion is null ? QuizLoadState.Empty : QuizLoadState.Ready);
            AppDebugLog.Write("QuizVM", $"LoadQuestions state after load={_loadState}.");
        }
        catch (TimeoutException)
        {
            ClearQuestionState();
            ErrorMessage = "Questions are taking too long to load. Try again.";
            SetLoadState(QuizLoadState.Failed);
            AppDebugLog.Write("QuizVM", "LoadQuestions timed out.");
        }
        catch (Exception ex)
        {
            ClearQuestionState();
            ErrorMessage = "Questions could not load. Please reopen this page.";
            SetLoadState(QuizLoadState.Failed);
            AppDebugLog.Error("QuizVM", ex, "LoadQuestions failed");
        }
        finally
        {
            IsBusy = false;
            NotifyComputedProperties();
            AppDebugLog.Write(
                "QuizVM",
                $"LoadQuestions finished. state={_loadState}, isLoading={IsLoading}, hasQuestion={HasQuestion}, hasNoQuestions={HasNoQuestions}.");
        }
    }

    private void SelectAnswer(SelectableQuizAnswer? selectable)
    {
        if (selectable is null) return;
        if (IsCurrentQuestionMultiAnswer)
        {
            ToggleMultiAnswer(selectable);
            ErrorMessage = string.Empty;
            OnPropertyChanged(nameof(FirstSelectedAnswer));
            OnPropertyChanged(nameof(SelectedAnswer));
            OnPropertyChanged(nameof(SelectedAnswers));
            return;
        }

        foreach (var a in CurrentAnswers)
            a.IsSelected = false;
        selectable.IsSelected = true;
        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(FirstSelectedAnswer));
        OnPropertyChanged(nameof(SelectedAnswer));
        OnPropertyChanged(nameof(SelectedAnswers));
    }

    private async Task NextAsync()
    {
        AppDebugLog.Write(
            "QuizVM",
            $"Next start. index={CurrentQuestionIndex}, total={Questions.Count}, selected={SelectedAnswers.Count}.");
        var selectedAnswers = SelectedAnswers;
        if (CurrentQuestion is null)
        {
            AppDebugLog.Write("QuizVM", "Next ignored: no current question.");
            return;
        }

        if (selectedAnswers.Count == 0)
        {
            ErrorMessage = "Choose an answer to continue.";
            AppDebugLog.Write("QuizVM", "Next blocked: no answer selected.");
            return;
        }

        RecordSelections(CurrentQuestion.Id, selectedAnswers.Select(answer => answer.Id));

        if (CurrentQuestionIndex < Questions.Count - 1)
        {
            CurrentQuestionIndex++;
            CurrentQuestion = Questions[CurrentQuestionIndex];
            AppDebugLog.Write(
                "QuizVM",
                $"Next moved to index={CurrentQuestionIndex}, questionId={CurrentQuestion.Id}, type={CurrentQuestion.QuestionType}.");
            BuildAnswers(CurrentQuestion);
            NotifyComputedProperties();
            return;
        }

        IsBusy = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            AppDebugLog.Write("QuizVM", $"Finishing quiz for userId={userId}, answers={_selectedAnswers.Count}.");
            var responses = _selectedAnswers
                .Select(pair => new UserQuizResponse { QuizQuestionId = pair.QuestionId, QuizAnswerId = pair.AnswerId })
                .ToList();
            await _quizService.SaveAnswersAsync(userId, responses);
            AppDebugLog.Write("QuizVM", "Quiz saved. Navigating to dashboard.");
            _navigateToDashboard();
        }
        catch (Exception ex)
        {
            AppDebugLog.Error("QuizVM", ex, "Next failed while saving quiz");
            throw;
        }
        finally
        {
            IsBusy = false;
            AppDebugLog.Write("QuizVM", "Next finished.");
        }
    }

    private void Back()
    {
        if (IsFirstQuestion) return;
        CurrentQuestionIndex--;
        CurrentQuestion = Questions[CurrentQuestionIndex];
        BuildAnswers(CurrentQuestion);
        NotifyComputedProperties();
    }

    private void BuildAnswers(QuizQuestion question)
    {
        AppDebugLog.Write("QuizVM", $"BuildAnswers questionId={question.Id}, answerCount={question.Answers.Count}.");
        ErrorMessage = string.Empty;
        var savedAnswerIds = _selectedAnswers
            .Where(pair => pair.QuestionId == question.Id)
            .Select(pair => pair.AnswerId)
            .ToHashSet();
        var list = question.Answers
            .Select(answer => new SelectableQuizAnswer
            {
                Answer = answer,
                QuestionType = question.QuestionType,
                IsSelected = savedAnswerIds.Contains(answer.Id)
            })
            .ToList();
        CurrentAnswers = new ObservableCollection<SelectableQuizAnswer>(list);
        OnPropertyChanged(nameof(FirstSelectedAnswer));
        OnPropertyChanged(nameof(SelectedAnswer));
        OnPropertyChanged(nameof(SelectedAnswers));
        OnPropertyChanged(nameof(IsCurrentQuestionMultiAnswer));
    }

    private void RecordSelections(int questionId, IEnumerable<int> answerIds)
    {
        _selectedAnswers.RemoveAll(p => p.QuestionId == questionId);
        _selectedAnswers.AddRange(answerIds.Select(answerId => (questionId, answerId)));
    }

    private void ToggleMultiAnswer(SelectableQuizAnswer selectable)
    {
        var isNone = selectable.Answer.MappedValue == 0;

        if (isNone)
        {
            foreach (var answer in CurrentAnswers)
                answer.IsSelected = false;

            selectable.IsSelected = true;
            return;
        }

        selectable.IsSelected = !selectable.IsSelected;

        foreach (var answer in CurrentAnswers.Where(answer => answer.Answer.MappedValue == 0))
            answer.IsSelected = false;
    }

    private void NotifyComputedProperties()
    {
        OnPropertyChanged(nameof(IsCurrentQuestionMultiAnswer));
        OnPropertyChanged(nameof(IsFirstQuestion));
        OnPropertyChanged(nameof(IsLastQuestion));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(ProgressValue));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(NextButtonText));
        OnPropertyChanged(nameof(CurrentQuestionText));
        OnPropertyChanged(nameof(QuestionsStateTitle));
        OnPropertyChanged(nameof(QuestionsStateMessage));
        OnPropertyChanged(nameof(CurrentQuestionIconGlyph));
        BackCommand.NotifyCanExecuteChanged();
    }

    private void SetLoadState(QuizLoadState loadState)
    {
        if (_loadState == loadState)
            return;

        AppDebugLog.Write("QuizVM", $"LoadState {_loadState} -> {loadState}.");
        _loadState = loadState;
        RefreshLoadStateFlags();
        NotifyComputedProperties();
    }

    private void RefreshLoadStateFlags()
    {
        IsLoading = _loadState is QuizLoadState.NotStarted or QuizLoadState.Loading;
        HasQuestion = CurrentQuestion is not null && _loadState == QuizLoadState.Ready;
        HasLoadedQuestions = _loadState is QuizLoadState.Ready or QuizLoadState.Empty;
        HasNoQuestions = _loadState is QuizLoadState.Empty or QuizLoadState.Failed;
        CanRetryQuestionsLoad = _loadState is QuizLoadState.Empty or QuizLoadState.Failed;

        AppDebugLog.Write(
            "QuizVM",
            $"Flags refreshed: isLoading={IsLoading}, hasQuestion={HasQuestion}, hasLoaded={HasLoadedQuestions}, hasNoQuestions={HasNoQuestions}, canRetry={CanRetryQuestionsLoad}.");
    }

    private void ClearQuestionState()
    {
        Questions = [];
        CurrentQuestion = null;
        CurrentAnswers = [];
        CurrentQuestionIndex = 0;
    }

    private enum QuizLoadState
    {
        NotStarted,
        Loading,
        Ready,
        Empty,
        Failed
    }
}
