using MuscleCuties.Core.Diagnostics;
using MuscleCuties.Core.ViewModels.Quiz;
using System.ComponentModel;

namespace MuscleCuties.App.Pages.Onboarding;

public partial class QuizPage : ContentPage
{
    private readonly QuizViewModel _viewModel;
    private bool _loadQueued;

    public QuizPage(QuizViewModel vm)
    {
        InitializeComponent();
        _viewModel = vm;
        BindingContext = vm;
        AppDebugLog.Write("QuizPage", "Constructed and binding context assigned.");
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += OnLoaded;
        UpdateVisualState();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        AppDebugLog.Write("QuizPage", "OnAppearing.");
        QueueQuestionLoad();
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        AppDebugLog.Write("QuizPage", "Loaded event.");
        QueueQuestionLoad();
    }

    private void QueueQuestionLoad()
    {
        AppDebugLog.Write(
            "QuizPage",
            $"QueueQuestionLoad: loadQueued={_loadQueued}, isBusy={_viewModel.IsBusy}, hasLoaded={_viewModel.HasLoadedQuestions}, canRetry={_viewModel.CanRetryQuestionsLoad}.");
        if (_loadQueued || _viewModel.IsBusy || _viewModel.HasLoadedQuestions || _viewModel.CanRetryQuestionsLoad)
            return;

        _loadQueued = true;
        var dispatched = Dispatcher.Dispatch(() => _ = LoadQuestionsSafelyAsync());
        AppDebugLog.Write("QuizPage", $"QueueQuestionLoad dispatched={dispatched}.");
        if (!dispatched)
            _ = LoadQuestionsSafelyAsync();
    }

    private async Task LoadQuestionsSafelyAsync()
    {
        AppDebugLog.Write("QuizPage", "LoadQuestionsSafelyAsync start.");
        try
        {
            await Task.Yield();
            await _viewModel.EnsureQuestionsLoadedAsync();
            UpdateVisualStateOnMainThread();
            AppDebugLog.Write("QuizPage", "LoadQuestionsSafelyAsync complete.");
        }
        catch (Exception ex)
        {
            AppDebugLog.Error("QuizPage", ex, "LoadQuestionsSafelyAsync failed");
        }
        finally
        {
            _loadQueued = false;
            AppDebugLog.Write("QuizPage", "LoadQuestionsSafelyAsync queue released.");
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(QuizViewModel.IsLoading) or
            nameof(QuizViewModel.HasQuestion) or
            nameof(QuizViewModel.HasNoQuestions))
        {
            UpdateVisualStateOnMainThread();
        }
    }

    private void UpdateVisualStateOnMainThread()
    {
        if (Dispatcher.IsDispatchRequired)
        {
            Dispatcher.Dispatch(UpdateVisualState);
            return;
        }

        UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        var showQuestion = _viewModel.HasQuestion;
        var showLoading = _viewModel.IsLoading && !showQuestion;
        var showEmpty = _viewModel.HasNoQuestions && !showQuestion;

        LoadingStatePanel.IsVisible = showLoading;
        LoadingIndicator.IsRunning = showLoading;
        EmptyStatePanel.IsVisible = showEmpty;
        QuestionHeader.IsVisible = showQuestion;
        QuestionScroll.IsVisible = showQuestion;
        ActionPanel.IsVisible = showQuestion;

        AppDebugLog.Write(
            "QuizPage",
            $"Visual state applied: loading={showLoading}, empty={showEmpty}, question={showQuestion}.");
    }
}
