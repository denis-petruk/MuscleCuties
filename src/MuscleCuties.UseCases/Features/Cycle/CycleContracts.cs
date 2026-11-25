namespace MuscleCuties.UseCases.Features.Cycle.Contracts;

/// <summary>The phase as the client sees it, kept separate from the domain enum.</summary>
public enum CyclePhaseDto
{
    Menstrual,
    Follicular,
    Ovulatory,
    Luteal,
    Unknown
}

/// <summary>The flow intensity as the client sees it.</summary>
public enum FlowIntensityDto
{
    None,
    Spotting,
    Light,
    Medium,
    Heavy
}

/// <summary>The symptom as the client sees it.</summary>
public enum SymptomTypeDto
{
    Cramps,
    Bloating,
    Fatigue,
    Headache,
    MoodSwings,
    Spotting,
    Other,
    BreastTenderness,
    LowerBackPain,
    Acne,
    Nausea,
    Cravings
}
