using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.UI.Quiz;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Quiz;

namespace MuscleCuties.Core.ViewModels.Quiz;

public partial class QuizViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IQuizService _quizService;
    private readonly Action _navigateToProfileSetup;
    private readonly List<QuizQuestion> _allQuestions = new();
    private readonly List<(int QuestionId, int AnswerId)> _selectedAnswers = new();

    [ObservableProperty] private List<QuizQuestion> _questions = new();
    [ObservableProperty] private QuizQuestion? _currentQuestion;
    [ObservableProperty] private int _currentQuestionIndex;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private ObservableCollection<SelectableQuizAnswer> _currentAnswers = new();

    public QuizAnswer? SelectedAnswer =>
        CurrentAnswers.FirstOrDefault(a => a.IsSelected)?.Answer;

    public bool HasQuestion => CurrentQuestion != null;
    public bool IsLoading => IsBusy;
    public bool HasNoQuestions => !IsBusy && Questions.Count == 0;
    public bool IsFirstQuestion => CurrentQuestionIndex == 0;
    public bool IsLastQuestion => Questions.Count > 0 && CurrentQuestionIndex == Questions.Count - 1;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public float ProgressValue => Questions.Count > 0 ? (float)(CurrentQuestionIndex + 1) / Questions.Count : 0f;
    public string ProgressText => $"{CurrentQuestionIndex + 1} / {Questions.Count}";
    public string NextButtonText => IsLastQuestion ? "Finish" : "Next";
    public string CurrentQuestionText => CurrentQuestion?.Question ?? string.Empty;

    public AsyncRelayCommand LoadQuestionsCommand { get; }
    public RelayCommand<SelectableQuizAnswer> SelectAnswerCommand { get; }
    public AsyncRelayCommand NextCommand { get; }
    public RelayCommand BackCommand { get; }

    public QuizViewModel(
        IAuthService authService,
        IQuizService quizService,
        Action navigateToProfileSetup)
    {
        _authService = authService;
        _quizService = quizService;
        _navigateToProfileSetup = navigateToProfileSetup;
        LoadQuestionsCommand = new AsyncRelayCommand(LoadQuestionsAsync);
        SelectAnswerCommand = new RelayCommand<SelectableQuizAnswer>(SelectAnswer);
        NextCommand = new AsyncRelayCommand(NextAsync);
        BackCommand = new RelayCommand(Back, () => !IsFirstQuestion);
    }

    private async Task LoadQuestionsAsync()
    {
        IsBusy = true;
        try
        {
            _allQuestions.Clear();
            _allQuestions.AddRange(await _quizService.GetOnboardingQuestionsAsync());
            Questions = BuildInitialQuestionFlow(_allQuestions);
            CurrentQuestionIndex = 0;
            CurrentQuestion = Questions.Count > 0 ? Questions[0] : null;
            if (CurrentQuestion != null)
                BuildAnswers(CurrentQuestion);
            NotifyComputedProperties();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SelectAnswer(SelectableQuizAnswer? selectable)
    {
        if (selectable is null) return;
        foreach (var a in CurrentAnswers)
            a.IsSelected = false;
        selectable.IsSelected = true;
        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(SelectedAnswer));
    }

    private async Task NextAsync()
    {
        var selected = SelectedAnswer;
        if (CurrentQuestion is null)
            return;

        if (selected is null)
        {
            ErrorMessage = "Choose an answer to continue.";
            return;
        }

        RecordSelection(CurrentQuestion.Id, selected.Id);
        ApplyConditionalQuestionFlow(CurrentQuestion, selected);

        if (CurrentQuestionIndex < Questions.Count - 1)
        {
            CurrentQuestionIndex++;
            CurrentQuestion = Questions[CurrentQuestionIndex];
            BuildAnswers(CurrentQuestion);
            NotifyComputedProperties();
            return;
        }

        IsBusy = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var responses = _selectedAnswers
                .Select(pair => new UserQuizResponse { QuizQuestionId = pair.QuestionId, QuizAnswerId = pair.AnswerId })
                .ToList();
            await _quizService.SaveAnswersAsync(userId, responses);
            _navigateToProfileSetup();
        }
        finally
        {
            IsBusy = false;
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
        ErrorMessage = string.Empty;
        var saved = _selectedAnswers.FirstOrDefault(p => p.QuestionId == question.Id);
        var list = question.Answers
            .Select(a => new SelectableQuizAnswer
            {
                Answer = (QuizAnswer)a,
                IsSelected = saved != default && saved.AnswerId == ((QuizAnswer)a).Id
            })
            .ToList();
        CurrentAnswers = new ObservableCollection<SelectableQuizAnswer>(list);
        OnPropertyChanged(nameof(SelectedAnswer));
    }

    private void RecordSelection(int questionId, int answerId)
    {
        _selectedAnswers.RemoveAll(p => p.QuestionId == questionId);
        _selectedAnswers.Add((questionId, answerId));
    }

    private static List<QuizQuestion> BuildInitialQuestionFlow(IEnumerable<QuizQuestion> questions) =>
        questions
            .Where(question => question.QuestionType is not QuizQuestionType.CurrentCyclePhase)
            .ToList();

    private void ApplyConditionalQuestionFlow(QuizQuestion question, QuizAnswer selected)
    {
        if (question.QuestionType is not QuizQuestionType.CycleTrackingMode)
            return;

        var isManual = selected.MappedValue == (int)CycleTrackingMode.ManualPhaseLogging;
        var phaseQuestion = _allQuestions.FirstOrDefault(q => q.QuestionType is QuizQuestionType.CurrentCyclePhase);
        if (phaseQuestion is null)
            return;

        var existingPhaseQuestionIndex = Questions.FindIndex(q => q.QuestionType is QuizQuestionType.CurrentCyclePhase);

        if (isManual)
        {
            if (existingPhaseQuestionIndex >= 0)
                return;

            var updatedQuestions = Questions.ToList();
            updatedQuestions.Insert(CurrentQuestionIndex + 1, phaseQuestion);
            Questions = updatedQuestions;
            return;
        }

        if (existingPhaseQuestionIndex < 0)
            return;

        _selectedAnswers.RemoveAll(pair => pair.QuestionId == phaseQuestion.Id);
        var withoutPhaseQuestion = Questions
            .Where(q => q.QuestionType is not QuizQuestionType.CurrentCyclePhase)
            .ToList();

        Questions = withoutPhaseQuestion;
        if (CurrentQuestionIndex >= Questions.Count)
            CurrentQuestionIndex = Math.Max(0, Questions.Count - 1);
    }

    private void NotifyComputedProperties()
    {
        OnPropertyChanged(nameof(HasQuestion));
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(HasNoQuestions));
        OnPropertyChanged(nameof(IsFirstQuestion));
        OnPropertyChanged(nameof(IsLastQuestion));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(ProgressValue));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(NextButtonText));
        OnPropertyChanged(nameof(CurrentQuestionText));
        BackCommand.NotifyCanExecuteChanged();
    }

    partial void OnCurrentQuestionIndexChanged(int value)
    {
        NotifyComputedProperties();
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(HasNoQuestions));
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnQuestionsChanged(List<QuizQuestion> value)
    {
        NotifyComputedProperties();
    }
}
