using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.ViewModels.Cycle;

namespace MuscleCuties.App.Pages.Cycle;

[QueryProperty(nameof(Phase), "phase")]
public partial class CyclePhaseDetailPage : ContentPage
{
    public CyclePhaseDetailPage(CyclePhaseDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    public string Phase
    {
        set
        {
            if (Enum.TryParse<CyclePhase>(value, ignoreCase: true, out var phase))
                ((CyclePhaseDetailViewModel)BindingContext).Load(phase);
        }
    }
}
