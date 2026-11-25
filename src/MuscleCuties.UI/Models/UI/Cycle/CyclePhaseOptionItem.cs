using Microsoft.Maui.Graphics;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;


namespace MuscleCuties.UI.Models.UI.Cycle;

/// <summary>One phase choice in the phase picker, with the colours already resolved for the tile.</summary>
public partial class CyclePhaseOptionItem : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    /// <summary>Domain phase this option stands for, saved when the person picks it.</summary>
    public CyclePhase Phase { get; init; }
    /// <summary>Localized phase name shown on the tile.</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>Localized one-line description under the name.</summary>
    public string Description { get; init; } = string.Empty;
    /// <summary>Static illustration file; ignored when <see cref="UsesAnimatedIcon" /> is true.</summary>
    public string IconSource { get; init; } = string.Empty;
    /// <summary>True when the tile plays the phase animation instead of showing the still image.</summary>
    public bool UsesAnimatedIcon { get; init; }
    /// <summary>Tile surface colour for this phase.</summary>
    public Color BackgroundColor { get; init; } = AppThemeResources.GetRequired<Color>("Transparent");
    /// <summary>Label colour that keeps the name readable on the tile surface.</summary>
    public Color TextColor { get; init; } = AppThemeResources.GetRequired<Color>("TextPrimary");
}
