using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Graphics;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;

using MuscleCuties.UI.Resources.Styles;

namespace MuscleCuties.UI.Models.UI.Cycle;

public partial class CyclePhaseOptionItem : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    public CyclePhase Phase { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string IconSource { get; init; } = string.Empty;
    public bool UsesAnimatedIcon { get; init; }
    public Color BackgroundColor { get; init; } = AppThemeResources.GetRequired<Color>("Transparent");
    public Color TextColor { get; init; } = AppThemeResources.GetRequired<Color>("TextPrimary");
}
