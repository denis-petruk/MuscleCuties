using MuscleCuties.App.Pages;
using MuscleCuties.Core.ViewModels.Quiz;

namespace MuscleCuties.App.Pages.Onboarding;

public partial class QuizPage : ContentPage
{
    public QuizPage(QuizViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        var vm = (QuizViewModel)BindingContext;
        if (!vm.HasQuestion && !vm.IsBusy)
            this.LoadAfterFirstRender(() => vm.LoadQuestionsCommand.ExecuteAsync(null));
    }
}
