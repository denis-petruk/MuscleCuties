using MuscleCuties.UI.Models.UI.Cycle;
using MuscleCuties.UI.ViewModels.Cycle;

namespace MuscleCuties.UI.Controls.Cycle;

/// <summary>
///     Horizontal carousel that opens each cycle phase detail page.
/// </summary>
public partial class CyclePhaseExplorerCarousel : ContentView
{
    /// <summary>Initializes the carousel declared in its XAML.</summary>
    public CyclePhaseExplorerCarousel()
    {
        InitializeComponent();
    }

    /// <summary>Raises the pill's own command, so the carousel works whichever pill the template produced.</summary>
    private void OnPhasePillClicked(object? sender, EventArgs e)
    {
        if (sender is not BindableObject { BindingContext: PhaseExplorerPillItem pill } ||
            BindingContext is not CycleViewModel viewModel)
            return;

        viewModel.SelectPhaseExplorerPillCommand.Execute(pill);
    }
}
