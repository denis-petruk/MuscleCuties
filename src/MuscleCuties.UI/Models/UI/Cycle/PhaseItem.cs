using Microsoft.Maui.Graphics;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;


namespace MuscleCuties.UI.Models.UI.Cycle;

/// <summary>One phase card in the phase explorer: its copy, illustration and card styling.</summary>
public class PhaseItem
{
    /// <summary>Domain phase this card describes.</summary>
    public CyclePhase Phase { get; set; }
    /// <summary>Localized phase name used as the card title.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Localized summary of what the phase means for the body.</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Large illustration file, or the first frame when the card animates.</summary>
    public string IllustrationSource { get; set; } = string.Empty;
    /// <summary>Small glyph used where the card is collapsed.</summary>
    public string IconSource { get; set; } = string.Empty;
    /// <summary>True when the card plays the phase animation instead of the still illustration.</summary>
    public bool UsesAnimatedIllustration { get; set; }
    /// <summary>Card surface colour for this phase.</summary>
    public Color BackgroundColor { get; set; } = AppThemeResources.GetRequired<Color>("Transparent");
    /// <summary>Colour of the card copy, readable on the card surface.</summary>
    public Color TextColor { get; set; } = AppThemeResources.GetRequired<Color>("TextPrimary");
    /// <summary>Card border colour; transparent when the card has no border.</summary>
    public Color StrokeColor { get; set; } = AppThemeResources.GetRequired<Color>("Transparent");
    /// <summary>Card border width in device-independent units.</summary>
    public double StrokeThickness { get; set; }
}
