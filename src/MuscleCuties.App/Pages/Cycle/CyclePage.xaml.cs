using MuscleCuties.Core.ViewModels;

namespace MuscleCuties.App.Pages.Cycle;

public partial class CyclePage : ContentPage
{
    public CyclePage(CycleViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ((CycleViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null);
    }
}
