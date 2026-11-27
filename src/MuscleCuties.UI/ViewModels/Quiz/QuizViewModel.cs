using MuscleCuties.Contracts.Onboarding;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.UI.Models.UI.Quiz;
using MuscleCuties.UseCases.Features.Onboarding.Quiz;

namespace MuscleCuties.UI.ViewModels.Quiz;

/// <summary>The onboarding quiz: one question at a time, including the phase-pain questions the rest of onboarding does</summary>
/// not ask, and then the hand-off to the created account.</summary>
public partial class QuizViewModel : ObservableObject
{
    private const int MinimumAvailableWeekdaySelections = 2;
    private const int MaximumAvailableWeekdaySelections = 7;
    private const string WeekdayCatalogUnavailableKey = "Onboarding_Quiz_WeekdayCatalogUnavailable";
    private readonly Func<Task> _navigateToDashboardAsync;
    private readonly Func<Task>? _navigateToLoginAsync;
    private readonly Func<Task> _navigateToProfileAsync;
    private readonly Action? _onAccountCreatedNeedsLogin;
    private readonly Action<int[]>? _onWeekdaysCommitted;
    private readonly Func<bool, Task<bool>>? _saveGluteFocusAsync;
    private bool _gluteFocusAnswered;
    [ObservableProperty] private bool _gluteFocus;
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
    [ObservableProperty] private bool _isPreparingWorkoutPreferences;

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

    /// <summary>Takes the quiz use case and the navigation callbacks.</summary>
    public QuizViewModel(
        IOnboardingQuizUseCase useCase,
        Func<Task> navigateToDashboardAsync,
        Func<Task>? navigateToLoginAsync,
        Func<Task> navigateToProfileAsync,
        Action? onAccountCreatedNeedsLogin = null,
        Action<int[]>? onWeekdaysCommitted = null,
        Func<bool, Task<bool>>? saveGluteFocusAsync = null)
    {
        _useCase = useCase;
        _navigateToDashboardAsync = navigateToDashboardAsync;
        _navigateToLoginAsync = navigateToLoginAsync;
        _navigateToProfileAsync = navigateToProfileAsync;
        _onAccountCreatedNeedsLogin = onAccountCreatedNeedsLogin;
        _onWeekdaysCommitted = onWeekdaysCommitted;
        _saveGluteFocusAsync = saveGluteFocusAsync;

        LoadQuestionsCommand = new AsyncRelayCommand(LoadQuestionsAsync);
        RetryLoadQuestionsCommand = new AsyncRelayCommand(RetryLoadQuestionsAsync);
        SelectAnswerCommand = new RelayCommand<SelectableQuizAnswer>(SelectAnswer);
        NextCommand = new AsyncRelayCommand(NextAsync);
        BackCommand = new RelayCommand(Back, () => !IsFirstQuestion && !IsBusy);
        GoToLoginCommand = new AsyncRelayCommand(() =>
            _navigateToLoginAsync?.Invoke() ?? Task.CompletedTask);
        EditProfileCommand = new AsyncRelayCommand(EditProfileAsync);
    }

    /// <summary>Loads the quiz questions.</summary>
    public AsyncRelayCommand LoadQuestionsCommand { get; }
    /// <summary>Retries the failed load.</summary>
    public AsyncRelayCommand RetryLoadQuestionsCommand { get; }
    /// <summary>Selects an answer.</summary>
    public RelayCommand<SelectableQuizAnswer> SelectAnswerCommand { get; }
    /// <summary>Moves to the next question.</summary>
    public AsyncRelayCommand NextCommand { get; }
    /// <summary>Moves to the previous question.</summary>
    public RelayCommand BackCommand { get; }
    /// <summary>Opens the login page.</summary>
    public AsyncRelayCommand GoToLoginCommand { get; }
    /// <summary>Opens the profile for editing.</summary>
    public AsyncRelayCommand EditProfileCommand { get; }

    /// <summary>The single answer the standard list treats as chosen — the first one selected, because that list is</summary>
    /// single-choice.</summary>
    public QuizAnswerDto? FirstSelectedAnswer =>
        CurrentAnswers.FirstOrDefault(answer => answer.IsSelected)?.Answer;

    /// <summary>Every selected answer for a question that takes more than one, in the order the list shows them.</summary>
    public IReadOnlyList<QuizAnswerDto> SelectedAnswers =>
        CurrentAnswers
            .Where(answer => answer.IsSelected)
            .Select(answer => answer.Answer)
            .ToList();

    /// <summary>True when the quiz has an error.</summary>
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    /// <summary>True once any answer has been chosen, including choices carried in from an earlier visit, which is what</summary>
    /// decides whether the quiz is offered as resumable.</summary>
    public bool HasQuizAnswers => _selectedAnswers.Count > 0 || CurrentAnswers.Any(answer => answer.IsSelected);
    /// <summary>Only the empty and failed states can be fixed by asking again, so they alone offer the retry.</summary>
    public bool CanRetryQuestionsLoad => LoadState is QuizLoadState.Empty or QuizLoadState.Failed;
    /// <summary>An empty quiz counts as loaded: it is a real answer from the server, not a state still in progress.</summary>
    public bool HasLoadedQuestions => LoadState is QuizLoadState.Ready or QuizLoadState.Empty;
    /// <summary>True when the quiz has no questions.</summary>
    public bool HasNoQuestions => LoadState is QuizLoadState.Empty;
    /// <summary>True when a question is shown.</summary>
    public bool HasQuestion => LoadState is QuizLoadState.Ready && CurrentQuestion is not null;
    /// <summary>The placeholder panel covers both a quiz with no questions and one that failed to load; they differ only in</summary>
    /// the words it shows.</summary>
    public bool IsEmptyStateVisible => LoadState is QuizLoadState.Empty or QuizLoadState.Failed;
    /// <summary>The quiz layout only exists once there are questions, so the loading and empty panes never stack on top of</summary>
    /// a half-built question.</summary>
    public bool IsLayoutVisible => LoadState is QuizLoadState.Ready;
    /// <summary>The first load is running.</summary>
    public bool IsLoading => LoadState is QuizLoadState.Loading;
    /// <summary>True when the loading state is shown.</summary>
    public bool IsLoadingVisible => IsLoading;
    /// <summary>Dietary preference and weekdays accept several answers; every other question takes one.</summary>
    public bool IsCurrentQuestionMultiAnswer => CurrentQuestion?.QuestionType is
        QuizQuestionType.DietaryPreference or QuizQuestionType.AvailableWeekdays;
    /// <summary>True for the available weekday question.</summary>
    public bool IsCurrentQuestionAvailableWeekdays =>
        CurrentQuestion?.QuestionType == QuizQuestionType.AvailableWeekdays;
    /// <summary>True for the dietary preference question.</summary>
    public bool IsCurrentQuestionDietaryPreference =>
        CurrentQuestion?.QuestionType == QuizQuestionType.DietaryPreference;
    /// <summary>True for the goal question.</summary>
    public bool IsCurrentQuestionGoal => CurrentQuestion?.QuestionType == QuizQuestionType.Goal;
    /// <summary>True for the equipment question.</summary>
    public bool IsCurrentQuestionEquipment => CurrentQuestion?.QuestionType == QuizQuestionType.Equipment;

    /// <summary>Every answer the quiz offers is selectable.</summary>
    internal static bool IsOfferedAnswer(QuizQuestionType questionType, QuizAnswerDto answer) => true;
    /// <summary>The pain questions are answered with a pair of sliders rather than an answer list.</summary>
    public bool IsCurrentQuestionPhasePair => IsPainQuestion(CurrentQuestion?.QuestionType);
    /// <summary>The sliders give way to the sign-in prompt once the account has been created.</summary>
    public bool IsPhasePairVisible => IsCurrentQuestionPhasePair && !AccountCreatedNeedsLogin;
    /// <summary>The answer list is what replaces both the sliders and the created-account prompt, so it hides for either.</summary>
    public bool IsStandardAnswerListVisible =>
        !IsCurrentQuestionPhasePair && !AccountCreatedNeedsLogin;
    /// <summary>How many weekdays are chosen; the quiz enforces a minimum of them before it lets the answer stand.</summary>
    public int SelectedWeekdayCount => IsCurrentQuestionAvailableWeekdays
        ? CurrentAnswers.Count(answer => answer.IsSelected)
        : 0;
    /// <summary>True when no question before this one is shown, so a hidden question cannot make the back action appear</summary>
    /// part-way through.</summary>
    public bool IsFirstQuestion => FindPreviousVisibleQuestionIndex(CurrentQuestionIndex) < 0;
    /// <summary>True when the back button is shown.</summary>
    public bool ShowQuizBackButton => !IsFirstQuestion && !AccountCreatedNeedsLogin;
    /// <summary>True when no shown question follows, so hidden questions cannot end the quiz early.</summary>
    public bool IsLastQuestion => Questions.Count > 0 && FindNextVisibleQuestionIndex(CurrentQuestionIndex) < 0;

    /// <summary>Progress across the questions actually shown, so a skipped question leaves no gap in the bar.</summary>
    public float ProgressValue => VisualQuestionCount == 0
        ? 0f
        : (float)CurrentVisualQuestionNumber / VisualQuestionCount;

    /// <summary>The question as asked; the slider pair carries its own title instead of a question text.</summary>
    public string CurrentQuestionText => IsCurrentQuestionPhasePair
        ? PhasePairTitle
        : CurrentQuestion?.Question ?? string.Empty;

    /// <summary>The action reads as finishing on the last question.</summary>
    public string NextButtonText => IsLastQuestion ? "Finish" : "Next";

    /// <summary>The position among shown questions, empty while there are none.</summary>
    public string ProgressText => VisualQuestionCount == 0
        ? string.Empty
        : $"{CurrentVisualQuestionNumber} / {VisualQuestionCount}";

    /// <summary>The slider pair's title for each pain question.</summary>
    public string PhasePairTitle => CurrentQuestion?.QuestionType switch
    {
        QuizQuestionType.MenstrualPain => "How does your period usually feel?",
        QuizQuestionType.FollicularPain => "How does your follicular phase usually feel?",
        QuizQuestionType.OvulatoryPain => "How does ovulation usually feel?",
        QuizQuestionType.LutealPain => "How does your luteal phase usually feel?",
        _ => string.Empty
    };

    /// <summary>The resource key of the helper line for the pain question being asked.</summary>
    public string CurrentPhasePairHelperText => CurrentQuestion?.QuestionType switch
    {
        QuizQuestionType.MenstrualPain => "Onboarding_Quiz_PhaseHelper_Menstrual",
        QuizQuestionType.FollicularPain => "Onboarding_Quiz_PhaseHelper_Follicular",
        QuizQuestionType.OvulatoryPain => "Onboarding_Quiz_PhaseHelper_Ovulatory",
        QuizQuestionType.LutealPain => "Onboarding_Quiz_PhaseHelper_Luteal",
        _ => string.Empty
    };

    /// <summary>The word for the pain step the slider is on, so the number is not left to interpretation.</summary>
    public string PainSliderLabel => GetPainLabel(ToSliderStep(PainSliderValue));
    /// <summary>The word for the energy step the slider is on.</summary>
    public string EnergySliderLabel => GetEnergyLabel(ToSliderStep(EnergySliderValue));
    /// <summary>The placeholder's title distinguishes a quiz with no questions from one that failed to load.</summary>
    public string QuestionsStateTitle => HasNoQuestions ? "No quiz questions found" : "Questions could not load";

    /// <summary>The placeholder's message: a seeding hint when the quiz is really empty, otherwise the load error.</summary>
    public string QuestionsStateMessage => HasNoQuestions
        ? "Restart the app once so the starter quiz can seed."
        : ErrorMessage;

    /// <summary>The icon that heads the question being asked.</summary>
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

    /// <summary>How many questions the user will actually be shown, which is what the progress bar is measured against.</summary>
    private int VisualQuestionCount => Questions.Count(question => !ShouldSkipQuestion(question));

    /// <summary>The shown-question number of the current question, so a skipped one does not inflate the count.</summary>
    private int CurrentVisualQuestionNumber => Questions
        .Take(CurrentQuestionIndex + 1)
        .Count(question => !ShouldSkipQuestion(question));

    /// <summary>Refreshes the page state when the account needs a login.</summary>
    partial void OnAccountCreatedNeedsLoginChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowQuizBackButton));
        OnPropertyChanged(nameof(IsStandardAnswerListVisible));
        OnPropertyChanged(nameof(IsPhasePairVisible));
    }

    /// <summary>Refreshes the command states when busy changes.</summary>
    partial void OnIsBusyChanged(bool value)
    {
        BackCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Loads the quiz only if it is not already there, so returning to the page does not refetch it.</summary>
    public async Task EnsureQuestionsLoadedAsync()
    {
        if (HasLoadedQuestions || IsBusy)
            return;

        await LoadQuestionsAsync();
    }

    /// <summary>The load command's entry point, which always refetches.</summary>
    private Task LoadQuestionsAsync()
    {
        return LoadQuestionsAsync(false);
    }

    /// <summary>Reads the questions — unless asked not to refresh something already loaded — and puts the page into the</summary>
    /// ready, empty or failed state that the rest of the page reads.</summary>
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
            ErrorMessage = "Ui_Quiz_Error_LoadQuestions";
            ShowEmptyState(false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Asks again after an empty or failed load.</summary>
    private async Task RetryLoadQuestionsAsync()
    {
        await LoadQuestionsAsync(true);
    }

    /// <summary>Applies a tap on an answer: a single-choice question replaces its choice, a multi-answer one toggles it.</summary>
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

    /// <summary>Moves to the next question, capturing the slider pair first and saving everything on the last one.</summary>
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
                ErrorMessage = "Ui_Quiz_Error_ChooseAnswer";
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

    /// <summary>Returns to the previous shown question, skipping the ones the quiz hides.</summary>
    private void Back()
    {
        if (IsFirstQuestion)
            return;

        MoveToQuestion(FindPreviousVisibleQuestionIndex(CurrentQuestionIndex));
    }

    /// <summary>Opens the profile step for editing.</summary>
    private Task EditProfileAsync() =>
        IsBusy || IsPreparingWorkoutPreferences || AccountCreatedNeedsLogin
            ? Task.CompletedTask
            : _navigateToProfileAsync();

    /// <summary>Puts the page in its loading state and clears any previous error, so a retry does not show both.</summary>
    private void BeginLoading()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        LoadState = QuizLoadState.Loading;
    }

    /// <summary>Installs freshly loaded questions and restores the answers already given, so a reload does not restart the</summary>
    /// quiz.</summary>
    private void ApplyQuestions(IEnumerable<QuizQuestionDto> loadedQuestions)
    {
        var allLoaded = loadedQuestions
            .Where(question => question.Answers.Count > 0)
            .ToList();
        var hasAvailableWeekdays = allLoaded.Any(question =>
            question.QuestionType == QuizQuestionType.AvailableWeekdays &&
            question.Answers.Count == MaximumAvailableWeekdaySelections &&
            question.Answers.Select(answer => answer.MappedValue).Distinct().Count() ==
            MaximumAvailableWeekdaySelections &&
            question.Answers.All(answer => Enum.IsDefined(typeof(DayOfWeek), answer.MappedValue)));

        if (!hasAvailableWeekdays)
        {
            ClearQuestionState();
            ErrorMessage = WeekdayCatalogUnavailableKey;
            ShowEmptyState(false);
            return;
        }

        Questions = allLoaded
            .Where(question => question.QuestionType.IsAvailableInCurrentOnboarding())
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

    /// <summary>Shows the placeholder, with the wording for either a quiz that has no questions or one that failed to</summary>
    /// load.</summary>
    private void ShowEmptyState(bool noQuestions)
    {
        LoadState = noQuestions ? QuizLoadState.Empty : QuizLoadState.Failed;
        NotifyComputedProperties();
    }

    /// <summary>Drops the current question's answers before another one is shown, so they cannot leak across.</summary>
    private void ClearQuestionState()
    {
        Questions = [];
        CurrentQuestion = null;
        CurrentAnswers = [];
        CurrentQuestionIndex = 0;
        NotifySelectionProperties();
    }

    /// <summary>Shows the question at an index and rebuilds its answer list.</summary>
    private void MoveToQuestion(int index)
    {
        CurrentQuestionIndex = Math.Clamp(index, 0, Questions.Count - 1);
        CurrentQuestion = Questions[CurrentQuestionIndex];
        BuildAnswers(CurrentQuestion);
        RestorePhasePairValues(CurrentQuestion);
        NotifyComputedProperties();
    }

    /// <summary>The selectable answers for a question, with any chosen earlier already marked.</summary>
    private void BuildAnswers(QuizQuestionDto question)
    {
        var savedAnswerIds = _selectedAnswers
            .Where(selection => selection.QuestionId == question.Id)
            .Select(selection => selection.AnswerId)
            .ToHashSet();

        CurrentAnswers = new ObservableCollection<SelectableQuizAnswer>(
            question.Answers
                .Where(answer => IsOfferedAnswer(question.QuestionType, answer))
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

    /// <summary>Clears the other choices and keeps only the tapped one.</summary>
    private void SelectSingleAnswer(SelectableQuizAnswer selectedAnswer)
    {
        foreach (var answer in CurrentAnswers)
            answer.IsSelected = ReferenceEquals(answer, selectedAnswer);
    }

    /// <summary>Adds or removes one choice without touching the rest.</summary>
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

    /// <summary>Stores the chosen answer ids for one question, replacing whatever was stored for it.</summary>
    private void RecordSelections(int questionId, IEnumerable<int> answerIds)
    {
        _selectedAnswers.RemoveAll(selection => selection.QuestionId == questionId);
        _selectedAnswers.AddRange(answerIds.Select(answerId => (questionId, answerId)));
    }

    /// <summary>Turns the two sliders into answers for the pain question and its matching energy question, reporting whether</summary>
    /// the pair was complete enough to keep.</summary>
    private bool RecordPhasePairSelections(QuizQuestionDto painQuestion)
    {
        var energyQuestion = GetPairedEnergyQuestion(painQuestion.QuestionType);
        if (energyQuestion is null)
        {
            ErrorMessage = "Ui_Quiz_Error_PhaseCheckIn";
            return false;
        }

        var painValue = 6 - ToSliderStep(PainSliderValue);
        var energyValue = ToSliderStep(EnergySliderValue);
        var painAnswer = painQuestion.Answers.FirstOrDefault(answer => answer.MappedValue == painValue);
        var energyAnswer = energyQuestion.Answers.FirstOrDefault(answer => answer.MappedValue == energyValue);
        if (painAnswer is null || energyAnswer is null)
        {
            ErrorMessage = "Ui_Quiz_Error_PhaseCheckIn";
            return false;
        }

        ErrorMessage = string.Empty;
        RecordSelections(painQuestion.Id, [painAnswer.Id]);
        RecordSelections(energyQuestion.Id, [energyAnswer.Id]);
        return true;
    }

    /// <summary>Puts previously saved values back on the sliders.</summary>
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

    /// <summary>The mapped value saved for a question's type, or nothing when it was never answered.</summary>
    private int? GetSavedMappedValue(QuizQuestionDto question)
    {
        var answerId = _selectedAnswers
            .LastOrDefault(selection => selection.QuestionId == question.Id)
            .AnswerId;
        return question.Answers.FirstOrDefault(answer => answer.Id == answerId)?.MappedValue;
    }

    /// <summary>The energy question that belongs to the same phase as a pain question.</summary>
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

    /// <summary>The next question the quiz will show, or -1 when this is the last one.</summary>
    private int FindNextVisibleQuestionIndex(int currentIndex)
    {
        for (var index = currentIndex + 1; index < Questions.Count; index++)
            if (!ShouldSkipQuestion(Questions[index]))
                return index;

        return -1;
    }

    /// <summary>The previous question the quiz will show, or -1 when this is the first.</summary>
    private int FindPreviousVisibleQuestionIndex(int currentIndex)
    {
        for (var index = currentIndex - 1; index >= 0; index--)
            if (!ShouldSkipQuestion(Questions[index]))
                return index;

        return -1;
    }

    /// <summary>Energy questions are answered inside the pain pair, and the pace question is hidden when the saved goal has no</summary>
    /// pace — so a question is only asked once.</summary>
    private bool ShouldSkipQuestion(QuizQuestionDto question)
    {
        return IsEnergyQuestion(question.QuestionType) ||
               (question.QuestionType is QuizQuestionType.GoalPace && !ShouldShowGoalPace());
    }

    /// <summary>Pace is asked for fat loss and strength, and also before a goal is saved so the question is not skipped by</summary>
    /// default.</summary>
    private bool ShouldShowGoalPace()
    {
        var goalQuestion = Questions.FirstOrDefault(question => question.QuestionType is QuizQuestionType.Goal);
        if (goalQuestion is null)
            return false;

        var selectedGoal = GetSavedMappedValue(goalQuestion);
        return selectedGoal is null || selectedGoal is (int)UserGoal.FatLoss or (int)UserGoal.Strength;
    }

    /// <summary>Drops the answers to every question of one type, which is how an answer that invalidates an earlier one clears</summary>
    /// it.</summary>
    private void RemoveSelections(QuizQuestionType questionType)
    {
        var questionIds = Questions
            .Where(question => question.QuestionType == questionType)
            .Select(question => question.Id)
            .ToHashSet();
        _selectedAnswers.RemoveAll(selection => questionIds.Contains(selection.QuestionId));
    }

    /// <summary>The four phase pain questions, which are answered with the slider pair.</summary>
    private static bool IsPainQuestion(QuizQuestionType? type)
    {
        return type is QuizQuestionType.MenstrualPain or QuizQuestionType.FollicularPain or
            QuizQuestionType.OvulatoryPain or QuizQuestionType.LutealPain;
    }

    /// <summary>The four phase energy questions, which ride along with their pain question instead of being asked.</summary>
    private static bool IsEnergyQuestion(QuizQuestionType type)
    {
        return type is QuizQuestionType.MenstrualEnergy or QuizQuestionType.FollicularEnergy or
            QuizQuestionType.OvulatoryEnergy or QuizQuestionType.LutealEnergy;
    }

    /// <summary>Snaps a slider's value to the 1–5 steps its labels describe.</summary>
    private static int ToSliderStep(double value)
    {
        return Math.Clamp((int)Math.Round(value), 1, 5);
    }

    /// <summary>The word for a pain step.</summary>
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

    /// <summary>The word for an energy step.</summary>
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

    /// <summary>Submits the answers once and turns the result into the created-account state, so the quiz cannot submit twice</summary>
    /// from a double tap.</summary>
    private async Task SaveAnswersAsync()
    {
        var totalStopwatch = Stopwatch.StartNew();
        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            IsPreparingWorkoutPreferences = true;

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

                var weekdayQuestion = Questions.FirstOrDefault(question =>
                    question.QuestionType == QuizQuestionType.AvailableWeekdays);
                if (weekdayQuestion is not null)
                {
                    var selectedIds = _selectedAnswers.Where(selection => selection.QuestionId == weekdayQuestion.Id)
                        .Select(selection => selection.AnswerId).ToHashSet();
                    var weekdays = weekdayQuestion.Answers.Where(answer => selectedIds.Contains(answer.Id))
                        .Select(answer => answer.MappedValue)
                        .Where(day => Enum.IsDefined(typeof(DayOfWeek), day)).Distinct().ToArray();
                    if (weekdays.Length >= MinimumAvailableWeekdaySelections)
                        _onWeekdaysCommitted?.Invoke(weekdays);
                }

                if (commitResult.AccountCreatedNeedsLogin)
                {
                    _onAccountCreatedNeedsLogin?.Invoke();
                    AccountCreatedNeedsLogin = true;
                    ErrorMessage = commitResult.ErrorMessage ?? string.Empty;
                    return;
                }

                _quizCommitted = true;
                Trace.WriteLine(
                    $"[Performance][Quiz] Answers and profile saved in {totalStopwatch.ElapsedMilliseconds} ms.");
            }

            if (_gluteFocusAnswered && _saveGluteFocusAsync is not null &&
                !await _saveGluteFocusAsync(GluteFocus))
            {
                ErrorMessage = "Onboarding_Quiz_GluteFocusSaveFailed";
                return;
            }

            // Finish the quiz overlay layout before opening first-plan setup.
            IsPreparingWorkoutPreferences = false;
            await Task.Delay(50);
            Trace.WriteLine("[Performance][Quiz] First-plan navigation starting.");
            await _navigateToDashboardAsync();
            Trace.WriteLine(
                $"[Performance][Quiz] First-plan navigation completed; " +
                $"total={totalStopwatch.ElapsedMilliseconds} ms.");
        }
        catch (Exception ex)
        {
            ErrorMessage = _quizCommitted
                ? "Your answers were saved. Tap Finish to set up your workout plan."
                : "We could not save your answers. Please try again.";
            Trace.WriteLine($"[ERROR][Quiz] SaveAnswersAsync failed ({ex.GetType().Name}).");
        }
        finally
        {
            IsPreparingWorkoutPreferences = false;
            IsBusy = false;
        }
    }

    /// <summary>Refreshes the question state when the question changes.</summary>
    partial void OnCurrentQuestionChanged(QuizQuestionDto? value)
    {
        OnPropertyChanged(nameof(CurrentQuestionText));
        OnPropertyChanged(nameof(CurrentQuestionIconGlyph));
    }

    /// <summary>Stores the pain answer when the slider moves.</summary>
    partial void OnPainSliderValueChanged(double value)
    {
        OnPropertyChanged(nameof(PainSliderLabel));
    }

    /// <summary>Stores the energy answer when the slider moves.</summary>
    partial void OnEnergySliderValueChanged(double value)
    {
        OnPropertyChanged(nameof(EnergySliderLabel));
    }

    /// <summary>Raises the selection properties.</summary>
    private void NotifySelectionProperties()
    {
        OnPropertyChanged(nameof(FirstSelectedAnswer));
        OnPropertyChanged(nameof(SelectedAnswers));
        OnPropertyChanged(nameof(SelectedWeekdayCount));
    }

    /// <summary>Records that the glute focus was answered.</summary>
    partial void OnGluteFocusChanged(bool value) => _gluteFocusAnswered = true;

    /// <summary>Raises the computed properties the page binds.</summary>
    private void NotifyComputedProperties()
    {
        OnPropertyChanged(nameof(IsCurrentQuestionMultiAnswer));
        OnPropertyChanged(nameof(IsCurrentQuestionAvailableWeekdays));
        OnPropertyChanged(nameof(IsCurrentQuestionDietaryPreference));
        OnPropertyChanged(nameof(IsCurrentQuestionEquipment));
        OnPropertyChanged(nameof(IsCurrentQuestionGoal));
        OnPropertyChanged(nameof(IsCurrentQuestionPhasePair));
        OnPropertyChanged(nameof(IsPhasePairVisible));
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
        OnPropertyChanged(nameof(CurrentPhasePairHelperText));
        OnPropertyChanged(nameof(PainSliderLabel));
        OnPropertyChanged(nameof(EnergySliderLabel));
        BackCommand.NotifyCanExecuteChanged();
    }
}
