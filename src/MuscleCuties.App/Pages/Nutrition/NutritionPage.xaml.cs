using MuscleCuties.App.Pages;
using MuscleCuties.Core.ViewModels.Nutrition;

namespace MuscleCuties.App.Pages.Nutrition;

public partial class NutritionPage : ContentPage
{
    public NutritionPage(NutritionViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        this.LoadAfterFirstRender(() => ((NutritionViewModel)BindingContext).RefreshCommand.ExecuteAsync(null));
    }
}
