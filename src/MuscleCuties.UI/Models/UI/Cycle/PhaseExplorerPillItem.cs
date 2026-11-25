using Microsoft.Maui.Graphics;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;


namespace MuscleCuties.UI.Models.UI.Cycle;

/// <summary>One pill in the phase explorer carousel; selecting it swaps the card underneath.</summary>
public class PhaseExplorerPillItem
{
    /// <summary>Domain phase this pill selects.</summary>
    public CyclePhase Phase { get; set; }
    /// <summary>Localized phase name on the pill.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Which cycle days the phase covers, already formatted for display.</summary>
    public string Window { get; set; } = string.Empty;
    /// <summary>Short line under the name that sets the phase's tone.</summary>
    public string Tagline { get; set; } = string.Empty;
    /// <summary>Phase glyph shown on the pill.</summary>
    public string IconSource { get; set; } = string.Empty;
    /// <summary>Pill surface colour for this phase.</summary>
    public Color BackgroundColor { get; set; } = AppThemeResources.GetRequired<Color>("Transparent");
    /// <summary>Pill label colour, readable on its surface.</summary>
    public Color TextColor { get; set; } = AppThemeResources.GetRequired<Color>("TextPrimary");
    /// <summary>Phase accent used for the pill's indicator while it is the active one.</summary>
    public Color AccentColor { get; set; } = AppThemeResources.GetRequired<Color>("Transparent");
    /// <summary>True for the phase the person is in today, which the carousel opens on.</summary>
    public bool IsActivePhase { get; set; }
}
