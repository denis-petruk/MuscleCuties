using MuscleCuties.UI.Models.UI.Cycle;
using MuscleCuties.UI.ViewModels.Cycle;

namespace MuscleCuties.UI.Controls.Cycle;

public partial class CyclePhaseExplorerCarousel : ContentView
{
    public CyclePhaseExplorerCarousel()
    {
        InitializeComponent();
    }

    private void OnPhasePillClicked(object? sender, EventArgs e)
    {
        if (sender is not BindableObject { BindingContext: PhaseExplorerPillItem pill } ||
            BindingContext is not CycleViewModel viewModel)
            return;

        viewModel.SelectPhaseExplorerPillCommand.Execute(pill);
    }
}
