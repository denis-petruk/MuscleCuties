using MauiIcons.Core;
using MuscleCuties.App.Services.Navigation;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.ViewModels.Cycle;

namespace MuscleCuties.App.Pages.Cycle;

public partial class CyclePhaseDetailPage : ContentPage
{
    private readonly INavigationContextService _navigationContext;
    private readonly CyclePhaseDetailViewModel _viewModel;

    public CyclePhaseDetailPage(
        CyclePhaseDetailViewModel vm,
        INavigationContextService navigationContext)
    {
        InitializeComponent();
        _ = new MauiIcon();
        _navigationContext = navigationContext;
        BindingContext = _viewModel = vm;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        if (_navigationContext.TryTake<CyclePhase>("phase", out var phase))
            _viewModel.Load(phase);
    }
}
