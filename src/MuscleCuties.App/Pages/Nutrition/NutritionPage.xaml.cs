using System.ComponentModel;
using MuscleCuties.Core.ViewModels.Nutrition;

namespace MuscleCuties.App.Pages.Nutrition;

public partial class NutritionPage : ContentPage
{
    private int _lastCelebrationToken;

    public NutritionPage(NutritionViewModel vm)
    {
        this.InitializeWithTiming(InitializeComponent);
        BindingContext = vm;
        vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        this.BeginPageLoad(() => ((NutritionViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(NutritionViewModel.CelebrationToken) ||
            BindingContext is not NutritionViewModel viewModel ||
            viewModel.CelebrationToken <= 0 ||
            viewModel.CelebrationToken == _lastCelebrationToken)
            return;

        _lastCelebrationToken = viewModel.CelebrationToken;
        MainThread.BeginInvokeOnMainThread(async () =>
            await SaluteOverlay.PlayAsync(viewModel.CelebrationIconSource));
    }
}
