using MuscleCuties.UI.Models.UI.Quiz;
using MuscleCuties.UI.ViewModels.Quiz;

namespace MuscleCuties.UI.Pages.Onboarding;

/// <summary>
///     Onboarding question page: one question at a time with its answer choices.
/// </summary>
public partial class QuizPage : ContentPage
{
    private readonly QuizViewModel _viewModel;

    /// <summary>Initializes the page and its view model.</summary>
    public QuizPage(QuizViewModel viewModel)
    {
        _viewModel = viewModel;
        this.InitializeAndBind(InitializeComponent, viewModel);
    }

    /// <summary>Starts the quiz when navigated to.</summary>
    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        this.BeginPageLoad(_viewModel.EnsureQuestionsLoadedAsync);
    }

    /// <summary>Selects the answer whose row was tapped, using the row's own binding context.</summary>
    private void OnAnswerTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border { BindingContext: SelectableQuizAnswer answer })
            _viewModel.SelectAnswerCommand.Execute(answer);
    }

    /// <summary>Toggles the glute-focus option from its label, ignored while the quiz is submitting.</summary>
    private void OnGluteFocusLabelTapped(object? sender, TappedEventArgs e)
    {
        if (_viewModel.IsBusy)
            return;

        OnboardingGluteFocusCheckBox.IsChecked = !OnboardingGluteFocusCheckBox.IsChecked;
    }

    /// <summary>Advances the quiz through the view model, which owns the validation.</summary>
    private async void OnNextClicked(object? sender, EventArgs e)
    {
        await _viewModel.NextCommand.ExecuteAsync(null);
    }

    /// <summary>Returns to profile setup, except once the account has been created and the user must sign in.</summary>
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
