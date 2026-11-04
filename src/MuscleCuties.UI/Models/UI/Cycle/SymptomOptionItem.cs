using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;

namespace MuscleCuties.UI.Models.UI.Cycle;

public class SymptomOptionItem
{
    public SymptomType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IconData { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}
