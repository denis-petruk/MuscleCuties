using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Models.UI.Cycle;

public static class CyclePhaseAssets
{
    public const string Menstrual = "phase_menstrual.png";
    public const string Follicular = "phase_follicular.png";
    public const string Ovulatory = "phase_ovulatory.png";
    public const string Luteal = "phase_luteal.png";
    public const string MenstrualAnimation = "phase_menstrual_blood_drops.json";
    public const string FollicularAnimation = "phase_follicular_plant.json";
    public const string OvulatoryAnimation = "phase_ovulatory_sun.json";
    public const string LutealAnimation = "phase_luteal_moon.json";

    public static string GetIconSource(CyclePhase phase) => phase switch
    {
        CyclePhase.Menstrual => Menstrual,
        CyclePhase.Follicular => Follicular,
        CyclePhase.Ovulatory => Ovulatory,
        CyclePhase.Luteal => Luteal,
        _ => Follicular
    };

    public static string GetVisualSource(CyclePhase phase) => phase switch
    {
        CyclePhase.Menstrual => MenstrualAnimation,
        CyclePhase.Follicular => FollicularAnimation,
        CyclePhase.Ovulatory => OvulatoryAnimation,
        CyclePhase.Luteal => LutealAnimation,
        _ => FollicularAnimation
    };

    public static bool UsesAnimatedVisual(CyclePhase phase) =>
        phase is CyclePhase.Menstrual or CyclePhase.Follicular or CyclePhase.Ovulatory or CyclePhase.Luteal;
}
