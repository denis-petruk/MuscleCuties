using MuscleCuties.UI.Models.UI.Quiz;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UI.ViewModels.Quiz;

namespace MuscleCuties.UI.Pages.Onboarding;

public partial class QuizPage : ContentPage
{
    private readonly QuizViewModel _viewModel;

    public QuizPage(QuizViewModel viewModel)
    {
        _viewModel = viewModel;
        this.InitializeAndBind(InitializeComponent, viewModel);
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

    private async void OnEditProfileClicked(object? sender, EventArgs e)
    {
        if (_viewModel.IsBusy || _viewModel.AccountCreatedNeedsLogin)
            return;

        if (_viewModel.HasQuizAnswers && !await DisplayAlertAsync(
                UiTextCatalog.Get("Onboarding_Quiz_GoBackToProfileConfirmationTitle"),
                UiTextCatalog.Get("Onboarding_Quiz_GoBackToProfileConfirmationBody"),
                UiTextCatalog.Get("Onboarding_Quiz_GoBackToProfile"),
                UiTextCatalog.Get("Onboarding_Quiz_StayHere")))
            return;

        await _viewModel.EditProfileCommand.ExecuteAsync(null);
    }
}
