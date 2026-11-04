using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.UseCases.Features.Cycle.Contracts;

public static class CycleContractMapper
{
    public static CyclePhase ToCore(CyclePhaseDto phase) => phase switch
    {
        CyclePhaseDto.Menstrual => CyclePhase.Menstrual,
        CyclePhaseDto.Follicular => CyclePhase.Follicular,
        CyclePhaseDto.Ovulatory => CyclePhase.Ovulatory,
        CyclePhaseDto.Luteal => CyclePhase.Luteal,
        CyclePhaseDto.Unknown => CyclePhase.Unknown,
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown cycle phase.")
    };

    public static CyclePhaseDto ToDto(CyclePhase phase) => phase switch
    {
        CyclePhase.Menstrual => CyclePhaseDto.Menstrual,
        CyclePhase.Follicular => CyclePhaseDto.Follicular,
        CyclePhase.Ovulatory => CyclePhaseDto.Ovulatory,
        CyclePhase.Luteal => CyclePhaseDto.Luteal,
        CyclePhase.Unknown => CyclePhaseDto.Unknown,
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown cycle phase.")
    };

    public static FlowIntensity ToCore(FlowIntensityDto intensity) => intensity switch
    {
        FlowIntensityDto.None => FlowIntensity.None,
        FlowIntensityDto.Spotting => FlowIntensity.Spotting,
        FlowIntensityDto.Light => FlowIntensity.Light,
        FlowIntensityDto.Medium => FlowIntensity.Medium,
        FlowIntensityDto.Heavy => FlowIntensity.Heavy,
        _ => throw new ArgumentOutOfRangeException(nameof(intensity), intensity, "Unknown flow intensity.")
    };

    public static FlowIntensityDto ToDto(FlowIntensity intensity) => intensity switch
    {
        FlowIntensity.None => FlowIntensityDto.None,
        FlowIntensity.Spotting => FlowIntensityDto.Spotting,
        FlowIntensity.Light => FlowIntensityDto.Light,
        FlowIntensity.Medium => FlowIntensityDto.Medium,
        FlowIntensity.Heavy => FlowIntensityDto.Heavy,
        _ => throw new ArgumentOutOfRangeException(nameof(intensity), intensity, "Unknown flow intensity.")
    };

    public static SymptomType ToCore(SymptomTypeDto symptom) => symptom switch
    {
        SymptomTypeDto.Cramps => SymptomType.Cramps,
        SymptomTypeDto.Bloating => SymptomType.Bloating,
        SymptomTypeDto.Fatigue => SymptomType.Fatigue,
        SymptomTypeDto.Headache => SymptomType.Headache,
        SymptomTypeDto.MoodSwings => SymptomType.MoodSwings,
        SymptomTypeDto.Spotting => SymptomType.Spotting,
        SymptomTypeDto.Other => SymptomType.Other,
        SymptomTypeDto.BreastTenderness => SymptomType.BreastTenderness,
        SymptomTypeDto.LowerBackPain => SymptomType.LowerBackPain,
        SymptomTypeDto.Acne => SymptomType.Acne,
        SymptomTypeDto.Nausea => SymptomType.Nausea,
        SymptomTypeDto.Cravings => SymptomType.Cravings,
        _ => throw new ArgumentOutOfRangeException(nameof(symptom), symptom, "Unknown cycle symptom.")
    };

    public static SymptomTypeDto ToDto(SymptomType symptom) => symptom switch
    {
        SymptomType.Cramps => SymptomTypeDto.Cramps,
        SymptomType.Bloating => SymptomTypeDto.Bloating,
        SymptomType.Fatigue => SymptomTypeDto.Fatigue,
        SymptomType.Headache => SymptomTypeDto.Headache,
        SymptomType.MoodSwings => SymptomTypeDto.MoodSwings,
        SymptomType.Spotting => SymptomTypeDto.Spotting,
        SymptomType.Other => SymptomTypeDto.Other,
        SymptomType.BreastTenderness => SymptomTypeDto.BreastTenderness,
        SymptomType.LowerBackPain => SymptomTypeDto.LowerBackPain,
        SymptomType.Acne => SymptomTypeDto.Acne,
        SymptomType.Nausea => SymptomTypeDto.Nausea,
        SymptomType.Cravings => SymptomTypeDto.Cravings,
        _ => throw new ArgumentOutOfRangeException(nameof(symptom), symptom, "Unknown cycle symptom.")
    };
}
