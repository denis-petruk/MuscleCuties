using System.Diagnostics;
using MauiIcons.Core;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.UI.Services.Navigation;
using MuscleCuties.UI.ViewModels.Cycle;

namespace MuscleCuties.UI.Pages.Cycle;

public partial class CyclePhaseDetailPage : ContentPage
{
    private readonly INavigationContextService _navigationContext;
    private readonly CyclePhaseDetailViewModel _viewModel;

    public CyclePhaseDetailPage(
        CyclePhaseDetailViewModel vm,
        INavigationContextService navigationContext)
    {
        var started = Stopwatch.GetTimestamp();
        this.InitializeWithTiming(InitializeComponent);
        _ = new MauiIcon();
        _navigationContext = navigationContext;
        this.BindWithTiming(vm, started);
        _viewModel = vm;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        if (_navigationContext.TryTake<CyclePhase>("phase", out var phase))
            this.BeginPageLoad(async () =>
            {
                _viewModel.Load(phase);
                await Task.CompletedTask;
            });
    }
}
