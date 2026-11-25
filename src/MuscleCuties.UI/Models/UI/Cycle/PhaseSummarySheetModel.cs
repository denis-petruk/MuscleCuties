using Microsoft.Maui.Graphics;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;


namespace MuscleCuties.UI.Models.UI.Cycle;

/// <summary>Content of the phase summary sheet: what the phase means for hormones, training,</summary>
/// nutrition and recovery, plus the one rule that summarises it.</summary>
public class PhaseSummarySheetModel
{
    /// <summary>Domain phase this sheet describes.</summary>
    public CyclePhase Phase { get; set; }
    /// <summary>Localized phase name used as the sheet title.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Which cycle days the phase covers, already formatted for display.</summary>
    public string Window { get; set; } = string.Empty;
    /// <summary>Short line under the title that sets the phase's tone.</summary>
    public string Tagline { get; set; } = string.Empty;
    /// <summary>What the hormones are doing in this phase.</summary>
    public string HormonalSummary { get; set; } = string.Empty;
    /// <summary>How to train in this phase.</summary>
    public string TrainingFocus { get; set; } = string.Empty;
    /// <summary>What to eat in this phase.</summary>
    public string NutritionFocus { get; set; } = string.Empty;
    /// <summary>How to recover in this phase.</summary>
    public string RecoveryFocus { get; set; } = string.Empty;
    /// <summary>One-sentence rule of thumb for the phase, shown last.</summary>
    public string QuickRule { get; set; } = string.Empty;
    /// <summary>Phase glyph shown beside the title.</summary>
    public string IconSource { get; set; } = string.Empty;
    /// <summary>Sheet surface colour for this phase.</summary>
    public Color BackgroundColor { get; set; } = AppThemeResources.GetRequired<Color>("Transparent");
    /// <summary>Sheet body colour, readable on its surface.</summary>
    public Color TextColor { get; set; } = AppThemeResources.GetRequired<Color>("TextPrimary");
    /// <summary>Phase accent for headings and dividers inside the sheet.</summary>
    public Color AccentColor { get; set; } = AppThemeResources.GetRequired<Color>("Transparent");
}
