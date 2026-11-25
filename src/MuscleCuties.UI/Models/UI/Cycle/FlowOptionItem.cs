using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;

namespace MuscleCuties.UI.Models.UI.Cycle;

/// <summary>One flow intensity choice in the quick-log sheet.</summary>
public class FlowOptionItem
{
    /// <summary>Domain intensity this option records.</summary>
    public FlowIntensity Intensity { get; set; }
    /// <summary>Localized intensity name shown on the chip.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Localized hint describing what the intensity looks like.</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Highlights the chip for the intensity already recorded on the day.</summary>
    public bool IsSelected { get; set; }
}
