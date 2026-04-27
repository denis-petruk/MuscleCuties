using MuscleCuties.Core.ViewModels;

namespace MuscleCuties.App.Pages.Nutrition;

public partial class NutritionPage : ContentPage
{
    public NutritionPage(NutritionViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ((NutritionViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null);
    }
}
