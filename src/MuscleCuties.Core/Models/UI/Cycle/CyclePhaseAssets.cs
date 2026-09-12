using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Models.UI.Cycle;

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

    public static bool UsesAnimatedVisual(CyclePhase phase)
    {
        return phase is CyclePhase.Menstrual or CyclePhase.Follicular or CyclePhase.Ovulatory or CyclePhase.Luteal;
    }
}
