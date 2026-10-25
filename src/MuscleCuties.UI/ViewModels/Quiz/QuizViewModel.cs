using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.UI.Models.UI.Quiz;
using MuscleCuties.UseCases.Features.Onboarding.Quiz;

namespace MuscleCuties.UI.ViewModels.Quiz;

public partial class QuizViewModel : ObservableObject
{
    private const int MinimumAvailableWeekdaySelections = 2;
    private const int MaximumAvailableWeekdaySelections = 7;
    private readonly Func<Task> _navigateToDashboardAsync;
    private readonly Func<Task>? _navigateToLoginAsync;
    private readonly Func<Task> _navigateToProfileAsync;
    private bool _quizCommitted;
    private readonly List<(int QuestionId, int AnswerId)> _selectedAnswers = new();
    private readonly IOnboardingQuizUseCase _useCase;
    [ObservableProperty] private bool _accountCreatedNeedsLogin;

    [ObservableProperty] private ObservableCollection<SelectableQuizAnswer> _currentAnswers = new();
    [ObservableProperty] private QuizQuestionDto? _currentQuestion;
    [ObservableProperty] private int _currentQuestionIndex;
    [ObservableProperty] private double _energySliderValue = 3;

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

    [ObservableProperty] private double _painSliderValue = 3;

    [ObservableProperty] private List<QuizQuestionDto> _questions = new();

    public QuizViewModel(
        IOnboardingQuizUseCase useCase,
        Func<Task> navigateToDashboardAsync,
        Func<Task>? navigateToLoginAsync,
        Func<Task> navigateToProfileAsync)
    {
        _useCase = useCase;
        _navigateToDashboardAsync = navigateToDashboardAsync;
        _navigateToLoginAsync = navigateToLoginAsync;
        _navigateToProfileAsync = navigateToProfileAsync;

        LoadQuestionsCommand = new AsyncRelayCommand(LoadQuestionsAsync);
        RetryLoadQuestionsCommand = new AsyncRelayCommand(RetryLoadQuestionsAsync);
        SelectAnswerCommand = new RelayCommand<SelectableQuizAnswer>(SelectAnswer);
        NextCommand = new AsyncRelayCommand(NextAsync);
        BackCommand = new RelayCommand(Back, () => !IsFirstQuestion);
        GoToLoginCommand = new AsyncRelayCommand(() =>
            _navigateToLoginAsync?.Invoke() ?? Task.CompletedTask);
        EditProfileCommand = new AsyncRelayCommand(EditProfileAsync);
    }

    public AsyncRelayCommand LoadQuestionsCommand { get; }
    public AsyncRelayCommand RetryLoadQuestionsCommand { get; }
    public RelayCommand<SelectableQuizAnswer> SelectAnswerCommand { get; }
    public AsyncRelayCommand NextCommand { get; }
    public RelayCommand BackCommand { get; }
    public AsyncRelayCommand GoToLoginCommand { get; }
    public AsyncRelayCommand EditProfileCommand { get; }

    public QuizAnswerDto? FirstSelectedAnswer =>
        CurrentAnswers.FirstOrDefault(answer => answer.IsSelected)?.Answer;

    public QuizAnswerDto? SelectedAnswer => FirstSelectedAnswer;

    public IReadOnlyList<QuizAnswerDto> SelectedAnswers =>
        CurrentAnswers
            .Where(answer => answer.IsSelected)
            .Select(answer => answer.Answer)
            .ToList();

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasQuizAnswers => _selectedAnswers.Count > 0 || CurrentAnswers.Any(answer => answer.IsSelected);
    public bool CanRetryQuestionsLoad => LoadState is QuizLoadState.Empty or QuizLoadState.Failed;
    public bool HasLoadedQuestions => LoadState is QuizLoadState.Ready or QuizLoadState.Empty;
    public bool HasNoQuestions => LoadState is QuizLoadState.Empty;
    public bool HasQuestion => LoadState is QuizLoadState.Ready && CurrentQuestion is not null;
    public bool IsEmptyStateVisible => LoadState is QuizLoadState.Empty or QuizLoadState.Failed;
    public bool IsLayoutVisible => LoadState is QuizLoadState.Ready;
    public bool IsLoading => LoadState is QuizLoadState.Loading;
    public bool IsLoadingVisible => IsLoading;
    public bool IsCurrentQuestionMultiAnswer => CurrentQuestion?.QuestionType is
        QuizQuestionType.DietaryPreference or QuizQuestionType.AvailableWeekdays;
    public bool IsCurrentQuestionAvailableWeekdays =>
        CurrentQuestion?.QuestionType == QuizQuestionType.AvailableWeekdays;
    public bool IsCurrentQuestionPhasePair => IsPainQuestion(CurrentQuestion?.QuestionType);
    public bool IsStandardAnswerListVisible =>
        !IsCurrentQuestionPhasePair;
    public int SelectedWeekdayCount => IsCurrentQuestionAvailableWeekdays
        ? CurrentAnswers.Count(answer => answer.IsSelected)
        : 0;
    public bool IsFirstQuestion => FindPreviousVisibleQuestionIndex(CurrentQuestionIndex) < 0;
    public bool ShowQuizBackButton => !IsFirstQuestion && !AccountCreatedNeedsLogin;
    public bool IsLastQuestion => Questions.Count > 0 && FindNextVisibleQuestionIndex(CurrentQuestionIndex) < 0;

    public float ProgressValue => VisualQuestionCount == 0
        ? 0f
        : (float)CurrentVisualQuestionNumber / VisualQuestionCount;

    public string CurrentQuestionText => IsCurrentQuestionPhasePair
        ? PhasePairTitle
        : CurrentQuestion?.Question ?? string.Empty;

    public string NextButtonText => IsLastQuestion ? "Finish" : "Next";

    public string ProgressText => VisualQuestionCount == 0
        ? string.Empty
        : $"{CurrentVisualQuestionNumber} / {VisualQuestionCount}";

    public string PhasePairTitle => CurrentQuestion?.QuestionType switch
    {
        QuizQuestionType.MenstrualPain => "How does your period usually feel?",
        QuizQuestionType.FollicularPain => "How does your follicular phase usually feel?",
        QuizQuestionType.OvulatoryPain => "How does ovulation usually feel?",
        QuizQuestionType.LutealPain => "How does your luteal phase usually feel?",
        _ => string.Empty
    };

    public string PainSliderLabel => GetPainLabel(ToSliderStep(PainSliderValue));
    public string EnergySliderLabel => GetEnergyLabel(ToSliderStep(EnergySliderValue));
    public string QuestionsStateTitle => HasNoQuestions ? "No quiz questions found" : "Questions could not load";

    public string QuestionsStateMessage => HasNoQuestions
        ? "Restart the app once so the starter quiz can seed."
        : ErrorMessage;

    public string CurrentQuestionIconGlyph => CurrentQuestion?.QuestionType switch
    {
        QuizQuestionType.Goal => "Target24",
        QuizQuestionType.GoalPace => "Gauge24",
        QuizQuestionType.ExperienceLevel => "Dumbbell24",
        QuizQuestionType.AvailableWeekdays => "CalendarWorkWeek24",
        QuizQuestionType.DietaryPreference => "Food24",
        QuizQuestionType.SessionDuration => "Clock24",
        QuizQuestionType.Equipment => "Dumbbell24",
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

    private int VisualQuestionCount => Questions.Count(question => !ShouldSkipQuestion(question));

    private int CurrentVisualQuestionNumber => Questions
        .Take(CurrentQuestionIndex + 1)
        .Count(question => !ShouldSkipQuestion(question));

    partial void OnAccountCreatedNeedsLoginChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowQuizBackButton));
    }

    public async Task EnsureQuestionsLoadedAsync()
    {
        if (HasLoadedQuestions || IsBusy)
            return;

        await LoadQuestionsAsync();
    }

    private Task LoadQuestionsAsync()
    {
        return LoadQuestionsAsync(false);
    }

    private async Task LoadQuestionsAsync(bool refresh)
    {
        BeginLoading();

        try
        {
            var loadedQuestions = await _useCase.LoadQuestionsAsync(refresh);
            ApplyQuestions(loadedQuestions);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[QuizViewModel] Question load failed ({ex.GetType().Name}).");
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
        await LoadQuestionsAsync(true);
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
        if (CurrentQuestion is null || IsBusy || AccountCreatedNeedsLogin)
            return;

        if (IsCurrentQuestionPhasePair)
        {
            if (!RecordPhasePairSelections(CurrentQuestion))
                return;
        }
        else
        {
            var selectedAnswers = SelectedAnswers;
            if (selectedAnswers.Count == 0)
            {
                ErrorMessage = "Choose an answer to continue.";
                return;
            }
            if (CurrentQuestion.QuestionType == QuizQuestionType.AvailableWeekdays &&
                selectedAnswers.Count is < MinimumAvailableWeekdaySelections or > MaximumAvailableWeekdaySelections)
            {
                ErrorMessage = "Onboarding_Quiz_AvailableWeekdaysMinimum";
                return;
            }

            RecordSelections(CurrentQuestion.Id, selectedAnswers.Select(answer => answer.Id));
            if (CurrentQuestion.QuestionType is QuizQuestionType.Goal && !ShouldShowGoalPace())
                RemoveSelections(QuizQuestionType.GoalPace);
        }

        if (!IsLastQuestion)
        {
            MoveToQuestion(FindNextVisibleQuestionIndex(CurrentQuestionIndex));
            return;
        }

        await SaveAnswersAsync();
    }

    private void Back()
    {
        if (IsFirstQuestion)
            return;

        MoveToQuestion(FindPreviousVisibleQuestionIndex(CurrentQuestionIndex));
    }

    private Task EditProfileAsync() =>
        IsBusy || IsPreparingDashboard || AccountCreatedNeedsLogin
            ? Task.CompletedTask
            : _navigateToProfileAsync();

    private void BeginLoading()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        LoadState = QuizLoadState.Loading;
    }

    private void ApplyQuestions(IEnumerable<QuizQuestionDto> loadedQuestions)
    {
        var allLoaded = loadedQuestions
            .Where(question => question.Answers.Count > 0)
            .ToList();
        var hasAvailableWeekdays = allLoaded.Any(
            question => question.QuestionType == QuizQuestionType.AvailableWeekdays);

        Questions = allLoaded
            .Where(question => question.QuestionType.IsAvailableInCurrentOnboarding() ||
                               (!hasAvailableWeekdays && question.QuestionType == QuizQuestionType.WorkoutDaysPerWeek))
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
        RestorePhasePairValues(CurrentQuestion);
        NotifyComputedProperties();
    }

    private void BuildAnswers(QuizQuestionDto question)
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
        var isNoneAnswer = selectedAnswer.QuestionType == QuizQuestionType.DietaryPreference &&
                           selectedAnswer.Answer.MappedValue == 0;

        if (isNoneAnswer)
        {
            foreach (var answer in CurrentAnswers)
                answer.IsSelected = false;

            selectedAnswer.IsSelected = true;
            return;
        }

        selectedAnswer.IsSelected = !selectedAnswer.IsSelected;
        if (selectedAnswer.QuestionType == QuizQuestionType.DietaryPreference)
        {
            foreach (var answer in CurrentAnswers.Where(answer => answer.Answer.MappedValue == 0))
                answer.IsSelected = false;
        }
    }

    private void RecordSelections(int questionId, IEnumerable<int> answerIds)
    {
        _selectedAnswers.RemoveAll(selection => selection.QuestionId == questionId);
        _selectedAnswers.AddRange(answerIds.Select(answerId => (questionId, answerId)));
    }

    private bool RecordPhasePairSelections(QuizQuestionDto painQuestion)
    {
        var energyQuestion = GetPairedEnergyQuestion(painQuestion.QuestionType);
        if (energyQuestion is null)
        {
            ErrorMessage = "This phase check-in could not be saved. Please try again.";
            return false;
        }

        var painValue = 6 - ToSliderStep(PainSliderValue);
        var energyValue = ToSliderStep(EnergySliderValue);
        var painAnswer = painQuestion.Answers.FirstOrDefault(answer => answer.MappedValue == painValue);
        var energyAnswer = energyQuestion.Answers.FirstOrDefault(answer => answer.MappedValue == energyValue);
        if (painAnswer is null || energyAnswer is null)
        {
            ErrorMessage = "This phase check-in could not be saved. Please try again.";
            return false;
        }

        ErrorMessage = string.Empty;
        RecordSelections(painQuestion.Id, [painAnswer.Id]);
        RecordSelections(energyQuestion.Id, [energyAnswer.Id]);
        return true;
    }

    private void RestorePhasePairValues(QuizQuestionDto question)
    {
        if (!IsPainQuestion(question.QuestionType))
            return;

        var painValue = GetSavedMappedValue(question) ?? 3;
        var energyQuestion = GetPairedEnergyQuestion(question.QuestionType);
        var energyValue = energyQuestion is null ? 3 : GetSavedMappedValue(energyQuestion) ?? 3;
        PainSliderValue = 6 - Math.Clamp(painValue, 1, 5);
        EnergySliderValue = Math.Clamp(energyValue, 1, 5);
    }

    private int? GetSavedMappedValue(QuizQuestionDto question)
    {
        var answerId = _selectedAnswers
            .LastOrDefault(selection => selection.QuestionId == question.Id)
            .AnswerId;
        return question.Answers.FirstOrDefault(answer => answer.Id == answerId)?.MappedValue;
    }

    private QuizQuestionDto? GetPairedEnergyQuestion(QuizQuestionType painType)
    {
        var energyType = painType switch
        {
            QuizQuestionType.MenstrualPain => QuizQuestionType.MenstrualEnergy,
            QuizQuestionType.FollicularPain => QuizQuestionType.FollicularEnergy,
            QuizQuestionType.OvulatoryPain => QuizQuestionType.OvulatoryEnergy,
            QuizQuestionType.LutealPain => QuizQuestionType.LutealEnergy,
            _ => (QuizQuestionType?)null
        };
        return energyType is null
            ? null
            : Questions.FirstOrDefault(question => question.QuestionType == energyType);
    }

    private int FindNextVisibleQuestionIndex(int currentIndex)
    {
        for (var index = currentIndex + 1; index < Questions.Count; index++)
            if (!ShouldSkipQuestion(Questions[index]))
                return index;

        return -1;
    }

    private int FindPreviousVisibleQuestionIndex(int currentIndex)
    {
        for (var index = currentIndex - 1; index >= 0; index--)
            if (!ShouldSkipQuestion(Questions[index]))
                return index;

        return -1;
    }

    private bool ShouldSkipQuestion(QuizQuestionDto question)
    {
        return IsEnergyQuestion(question.QuestionType) ||
               (question.QuestionType is QuizQuestionType.GoalPace && !ShouldShowGoalPace());
    }

    private bool ShouldShowGoalPace()
    {
        var goalQuestion = Questions.FirstOrDefault(question => question.QuestionType is QuizQuestionType.Goal);
        if (goalQuestion is null)
            return false;

        var selectedGoal = GetSavedMappedValue(goalQuestion);
        return selectedGoal is null || selectedGoal is (int)UserGoal.FatLoss or (int)UserGoal.Strength;
    }

    private void RemoveSelections(QuizQuestionType questionType)
    {
        var questionIds = Questions
            .Where(question => question.QuestionType == questionType)
            .Select(question => question.Id)
            .ToHashSet();
        _selectedAnswers.RemoveAll(selection => questionIds.Contains(selection.QuestionId));
    }

    private static bool IsPainQuestion(QuizQuestionType? type)
    {
        return type is QuizQuestionType.MenstrualPain or QuizQuestionType.FollicularPain or
            QuizQuestionType.OvulatoryPain or QuizQuestionType.LutealPain;
    }

    private static bool IsEnergyQuestion(QuizQuestionType type)
    {
        return type is QuizQuestionType.MenstrualEnergy or QuizQuestionType.FollicularEnergy or
            QuizQuestionType.OvulatoryEnergy or QuizQuestionType.LutealEnergy;
    }

    private static int ToSliderStep(double value)
    {
        return Math.Clamp((int)Math.Round(value), 1, 5);
    }

    private static string GetPainLabel(int value)
    {
        return value switch
        {
            1 => "Severe",
            2 => "Rough",
            3 => "Noticeable",
            4 => "Manageable",
            _ => "None"
        };
    }

    private static string GetEnergyLabel(int value)
    {
        return value switch
        {
            1 => "Very low",
            2 => "Low",
            3 => "Steady",
            4 => "Strong",
            _ => "Peak"
        };
    }

    private async Task SaveAnswersAsync()
    {
        var totalStopwatch = Stopwatch.StartNew();
        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            IsPreparingDashboard = true;

            if (!_quizCommitted)
            {
                var responses = _selectedAnswers
                    .Select(selection => new QuizSelectionDto(selection.QuestionId, selection.AnswerId))
                    .ToArray();
                var commitResult = await _useCase.CommitAsync(responses);
                if (!commitResult.Succeeded)
                {
                    ErrorMessage = commitResult.ErrorMessage ?? "We could not save your answers. Please try again.";
                    return;
                }

                if (commitResult.AccountCreatedNeedsLogin)
                {
                    AccountCreatedNeedsLogin = true;
                    ErrorMessage = commitResult.ErrorMessage ?? string.Empty;
                    return;
                }

                _quizCommitted = true;
                Trace.WriteLine(
                    $"[Performance][Quiz] Answers and profile saved in {totalStopwatch.ElapsedMilliseconds} ms.");
            }

            // The dashboard loads its state after appearing. Finish the quiz overlay
            // layout before replacing the onboarding shell with the dashboard shell.
            IsPreparingDashboard = false;
            await Task.Delay(50);
            Trace.WriteLine("[Performance][Quiz] Dashboard navigation starting.");
            await _navigateToDashboardAsync();
            Trace.WriteLine(
                $"[Performance][Quiz] Dashboard navigation completed; " +
                $"total={totalStopwatch.ElapsedMilliseconds} ms.");
        }
        catch (Exception ex)
        {
            ErrorMessage = _quizCommitted
                ? "Your answers were saved. Tap Finish to open your dashboard."
                : "We could not save your answers. Please try again.";
            Trace.WriteLine($"[ERROR][Quiz] SaveAnswersAsync failed ({ex.GetType().Name}).");
        }
        finally
        {
            IsPreparingDashboard = false;
            IsBusy = false;
        }
    }

    partial void OnCurrentQuestionChanged(QuizQuestionDto? value)
    {
        OnPropertyChanged(nameof(CurrentQuestionText));
        OnPropertyChanged(nameof(CurrentQuestionIconGlyph));
    }

    partial void OnPainSliderValueChanged(double value)
    {
        OnPropertyChanged(nameof(PainSliderLabel));
    }

    partial void OnEnergySliderValueChanged(double value)
    {
        OnPropertyChanged(nameof(EnergySliderLabel));
    }

    private void NotifySelectionProperties()
    {
        OnPropertyChanged(nameof(FirstSelectedAnswer));
        OnPropertyChanged(nameof(SelectedAnswer));
        OnPropertyChanged(nameof(SelectedAnswers));
        OnPropertyChanged(nameof(SelectedWeekdayCount));
    }

    private void NotifyComputedProperties()
    {
        OnPropertyChanged(nameof(IsCurrentQuestionMultiAnswer));
        OnPropertyChanged(nameof(IsCurrentQuestionAvailableWeekdays));
        OnPropertyChanged(nameof(IsCurrentQuestionPhasePair));
        OnPropertyChanged(nameof(IsStandardAnswerListVisible));
        OnPropertyChanged(nameof(SelectedWeekdayCount));
        OnPropertyChanged(nameof(IsFirstQuestion));
        OnPropertyChanged(nameof(ShowQuizBackButton));
        OnPropertyChanged(nameof(IsLastQuestion));
        OnPropertyChanged(nameof(ProgressValue));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(NextButtonText));
        OnPropertyChanged(nameof(CurrentQuestionText));
        OnPropertyChanged(nameof(QuestionsStateTitle));
        OnPropertyChanged(nameof(QuestionsStateMessage));
        OnPropertyChanged(nameof(CurrentQuestionIconGlyph));
        OnPropertyChanged(nameof(PhasePairTitle));
        OnPropertyChanged(nameof(PainSliderLabel));
        OnPropertyChanged(nameof(EnergySliderLabel));
        BackCommand.NotifyCanExecuteChanged();
    }
}
