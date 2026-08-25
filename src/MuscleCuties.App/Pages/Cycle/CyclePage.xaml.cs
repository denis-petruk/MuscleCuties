using MuscleCuties.App.Pages;
using MuscleCuties.Core.ViewModels.Cycle;

namespace MuscleCuties.App.Pages.Cycle;

public partial class CyclePage : ContentPage
{
    private bool _isThemeHandlerAttached;

    public CyclePage(CycleViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var vm = (CycleViewModel)BindingContext;
        AttachThemeHandler();
        vm.RefreshThemeColors(IsDarkTheme());
        this.LoadAfterFirstRender(() => vm.LoadDataCommand.ExecuteAsync(null));
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
        ((CycleViewModel)BindingContext).RefreshThemeColors(e.RequestedTheme == AppTheme.Dark);
    }

    private static bool IsDarkTheme() =>
        Application.Current?.RequestedTheme == AppTheme.Dark;
}
