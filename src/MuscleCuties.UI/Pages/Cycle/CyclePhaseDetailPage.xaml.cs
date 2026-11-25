using MauiIcons.Core;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;
using MuscleCuties.UI.Services.Navigation;
using MuscleCuties.UI.ViewModels.Cycle;

namespace MuscleCuties.UI.Pages.Cycle;

/// <summary>
///     Detail page for one cycle phase, opened from the cycle carousel.
/// </summary>
public partial class CyclePhaseDetailPage : ContentPage
{
    private readonly INavigationContextService _navigationContext;
    private readonly CyclePhaseDetailViewModel _viewModel;

    /// <summary>Initializes the page with its view model and navigation context.</summary>
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

    /// <summary>Reads the phase the page was opened for.</summary>
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
