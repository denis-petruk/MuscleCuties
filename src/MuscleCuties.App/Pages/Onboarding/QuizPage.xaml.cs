using MuscleCuties.Core.Models.UI.Quiz;
using MuscleCuties.Core.ViewModels.Quiz;

namespace MuscleCuties.App.Pages.Onboarding;

public partial class QuizPage : ContentPage
{
    private readonly QuizViewModel _viewModel;

    public QuizPage(QuizViewModel viewModel)
    {
        this.InitializeWithTiming(InitializeComponent);
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        this.BeginPageLoad(_viewModel.EnsureQuestionsLoadedAsync);
    }

    private void OnAnswerTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border { BindingContext: SelectableQuizAnswer answer })
            _viewModel.SelectAnswerCommand.Execute(answer);
    }

    private void OnBackTapped(object? sender, TappedEventArgs e)
    {
        _viewModel.BackCommand.Execute(null);
    }

    private async void OnNextClicked(object? sender, EventArgs e)
    {
        await _viewModel.NextCommand.ExecuteAsync(null);
    }
}
