using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;

namespace MuscleCuties.UI.Models.UI.Cycle;

/// <summary>File names for the four phase illustrations and their animations, and the phase to asset</summary>
/// mapping every cycle surface uses, so no screen spells an asset path itself.</summary>
public static class CyclePhaseAssets
{
    public const string Menstrual = "phase_menstrual.png";
    public const string Follicular = "phase_follicular.png";
    public const string Ovulatory = "phase_ovulatory.png";
    public const string Luteal = "phase_luteal.png";
    public const string MenstrualAnimation = "Animations/Cycle/phase_menstrual_blood_drops.json";
    public const string FollicularAnimation = "Animations/Cycle/phase_follicular_plant.json";
    public const string OvulatoryAnimation = "Animations/Cycle/phase_ovulatory_sun.json";
    public const string LutealAnimation = "Animations/Cycle/phase_luteal_moon.json";

    /// <summary>The static illustration for a phase. An unknown phase falls back to the follicular art</summary>
    /// instead of rendering nothing.</summary>
    public static string GetIconSource(CyclePhase phase)
    {
        return phase switch
        {
            CyclePhase.Menstrual => Menstrual,
            CyclePhase.Follicular => Follicular,
            CyclePhase.Ovulatory => Ovulatory,
            CyclePhase.Luteal => Luteal,
            _ => Follicular
        };
    }

    /// <summary>The animation played for a phase, with the same follicular fallback.</summary>
    public static string GetVisualSource(CyclePhase phase)
    {
        return phase switch
        {
            CyclePhase.Menstrual => MenstrualAnimation,
            CyclePhase.Follicular => FollicularAnimation,
            CyclePhase.Ovulatory => OvulatoryAnimation,
            CyclePhase.Luteal => LutealAnimation,
            _ => FollicularAnimation
        };
    }

    /// <summary>Only the four known phases animate; anything else uses the static illustration.</summary>
    public static bool UsesAnimatedVisual(CyclePhase phase)
    {
        return phase is CyclePhase.Menstrual or CyclePhase.Follicular or CyclePhase.Ovulatory or CyclePhase.Luteal;
    }
}
