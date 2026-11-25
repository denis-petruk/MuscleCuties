using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;

namespace MuscleCuties.UI.Models.UI.Cycle;

/// <summary>One symptom choice in the symptom picker.</summary>
public class SymptomOptionItem
{
    /// <summary>Domain symptom this option records.</summary>
    public SymptomType Type { get; set; }
    /// <summary>Localized symptom name shown on the chip.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Inline path data for the symptom glyph, drawn by the chip.</summary>
    public string IconData { get; set; } = string.Empty;
    /// <summary>Highlights the chip for a symptom already logged on the day.</summary>
    public bool IsSelected { get; set; }
}
