using MuscleCuties.Core.ViewModels.Cycle;

namespace MuscleCuties.App.Pages.Cycle;

public partial class CyclePage : ContentPage
{
    private bool _isThemeHandlerAttached;

    public CyclePage(CycleViewModel vm)
    {
        this.InitializeWithTiming(InitializeComponent);
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        AttachThemeHandler();
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        var vm = (CycleViewModel)BindingContext;
        this.BeginPageLoad(async () =>
        {
            await vm.LoadDataCommand.ExecuteAsync(null);
            vm.RefreshThemeColors(IsDarkTheme());
        });
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

    private static bool IsDarkTheme()
    {
        return Application.Current?.RequestedTheme == AppTheme.Dark;
    }
}
