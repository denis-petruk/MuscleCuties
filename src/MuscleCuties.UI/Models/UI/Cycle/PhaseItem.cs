using Microsoft.Maui.Graphics;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;

namespace MuscleCuties.UI.Models.UI.Cycle;

public class PhaseItem
{
    public CyclePhase Phase { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IllustrationSource { get; set; } = string.Empty;
    public string IconSource { get; set; } = string.Empty;
    public bool UsesAnimatedIllustration { get; set; }
    public Color BackgroundColor { get; set; } = Colors.Transparent;
    public Color TextColor { get; set; } = Colors.Black;
    public Color StrokeColor { get; set; } = Colors.Transparent;
    public double StrokeThickness { get; set; }
}
