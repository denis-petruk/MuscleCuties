using Microsoft.Maui.Graphics;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;

using MuscleCuties.UI.Resources.Styles;

namespace MuscleCuties.UI.Models.UI.Cycle;

public class PhaseExplorerPillItem
{
    public CyclePhase Phase { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Window { get; set; } = string.Empty;
    public string Tagline { get; set; } = string.Empty;
    public string IconSource { get; set; } = string.Empty;
    public Color BackgroundColor { get; set; } = AppThemeResources.GetRequired<Color>("Transparent");
    public Color TextColor { get; set; } = AppThemeResources.GetRequired<Color>("TextPrimary");
    public Color AccentColor { get; set; } = AppThemeResources.GetRequired<Color>("Transparent");
    public bool IsActivePhase { get; set; }
}
