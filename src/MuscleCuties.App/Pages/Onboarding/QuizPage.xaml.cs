using MuscleCuties.Core.ViewModels;

namespace MuscleCuties.App.Pages.Onboarding;

public partial class QuizPage : ContentPage
{
    public QuizPage(QuizViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var vm = (QuizViewModel)BindingContext;
        if (!vm.HasQuestion && !vm.IsBusy)
            await vm.LoadQuestionsCommand.ExecuteAsync(null);
    }
}
