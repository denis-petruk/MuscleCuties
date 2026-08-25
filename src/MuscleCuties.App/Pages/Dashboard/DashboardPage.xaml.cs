using MuscleCuties.App.Pages;
using MuscleCuties.Core.ViewModels.Dashboard;

namespace MuscleCuties.App.Pages.Dashboard;

public partial class DashboardPage : ContentPage
{
    private bool _isThemeHandlerAttached;

    public DashboardPage(DashboardViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        AttachThemeHandler();
        ((DashboardViewModel)BindingContext).RefreshThemeColors(IsDarkTheme());
        this.LoadAfterFirstRender(() => ((DashboardViewModel)BindingContext).RefreshCommand.ExecuteAsync(null));
    }

    protected override void OnDisappearing()
    {
        DetachThemeHandler();
        base.OnDisappearing();
    }

    private void AttachThemeHandler()
    {
        if (_isThemeHandlerAttached || Application.Current is null)
            return;

        Application.Current.RequestedThemeChanged += OnRequestedThemeChanged;
        _isThemeHandlerAttached = true;
    }

    private void DetachThemeHandler()
    {
        if (!_isThemeHandlerAttached || Application.Current is null)
            return;

        Application.Current.RequestedThemeChanged -= OnRequestedThemeChanged;
        _isThemeHandlerAttached = false;
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        ((DashboardViewModel)BindingContext).RefreshThemeColors(e.RequestedTheme == AppTheme.Dark);
    }

    private static bool IsDarkTheme() =>
        Application.Current?.RequestedTheme == AppTheme.Dark;
}
