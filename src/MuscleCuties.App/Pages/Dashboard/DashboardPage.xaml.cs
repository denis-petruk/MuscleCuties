using MuscleCuties.Core.ViewModels;

namespace MuscleCuties.App.Pages.Dashboard;

public partial class DashboardPage : ContentPage
{
    public DashboardPage(DashboardViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ((DashboardViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null);
    }
}
