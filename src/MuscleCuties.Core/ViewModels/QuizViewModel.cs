using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Services;

namespace MuscleCuties.Core.ViewModels;

public partial class QuizViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IQuizService _quizService;
    private readonly Action _navigateToProfileSetup;
    private readonly List<(int QuestionId, int AnswerId)> _selectedAnswers = new();

    [ObservableProperty] private List<QuizQuestion> _questions = new();
    [ObservableProperty] private QuizQuestion? _currentQuestion;
    [ObservableProperty] private int _currentQuestionIndex;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private ObservableCollection<SelectableQuizAnswer> _currentAnswers = new();

    public QuizAnswer? SelectedAnswer =>
        CurrentAnswers.FirstOrDefault(a => a.IsSelected)?.Answer;

    public bool HasQuestion => CurrentQuestion != null;
    public bool IsLoading => IsBusy;
    public bool HasNoQuestions => !IsBusy && Questions.Count == 0;
    public bool IsFirstQuestion => CurrentQuestionIndex == 0;
    public bool IsLastQuestion => Questions.Count > 0 && CurrentQuestionIndex == Questions.Count - 1;
    public float ProgressValue => Questions.Count > 0 ? (float)(CurrentQuestionIndex + 1) / Questions.Count : 0f;
    public string ProgressText => $"{CurrentQuestionIndex + 1} / {Questions.Count}";
    public string NextButtonText => IsLastQuestion ? "Finish" : "Next";

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
            Questions = await _quizService.GetOnboardingQuestionsAsync();
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
        OnPropertyChanged(nameof(SelectedAnswer));
    }

    private async Task NextAsync()
    {
        var selected = SelectedAnswer;
        if (selected is null) return;

        if (!IsLastQuestion)
        {
            if (selected is not null && CurrentQuestion is not null)
                RecordSelection(CurrentQuestion.Id, selected.Id);

            CurrentQuestionIndex++;
            CurrentQuestion = Questions[CurrentQuestionIndex];
            BuildAnswers(CurrentQuestion);
            NotifyComputedProperties();
            return;
        }

        if (CurrentQuestion is null) return;

        RecordSelection(CurrentQuestion.Id, selected.Id);

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

    private void NotifyComputedProperties()
    {
        OnPropertyChanged(nameof(HasQuestion));
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(HasNoQuestions));
        OnPropertyChanged(nameof(IsFirstQuestion));
        OnPropertyChanged(nameof(IsLastQuestion));
        OnPropertyChanged(nameof(ProgressValue));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(NextButtonText));
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

    partial void OnQuestionsChanged(List<QuizQuestion> value)
    {
        NotifyComputedProperties();
    }
}
