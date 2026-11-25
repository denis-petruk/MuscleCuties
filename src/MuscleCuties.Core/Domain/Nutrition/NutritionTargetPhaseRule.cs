using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Domain.Nutrition;

/// <summary>Decides which phase automatic targets use, so a projection never drives a calorie change.</summary>
public static class NutritionTargetPhaseRule
{
    public const float MenstrualCaloriesAdjustment = -50f;
    public const float OvulatoryCaloriesAdjustment = 50f;
    public const float LutealCaloriesAdjustment = 150f;
    public const float NeutralCaloriesAdjustment = 0f;

    /// <summary>The phase automatic targets may use, falling back to neutral when it is unknown.</summary>
    public static CyclePhase ForAutomaticTargets(CyclePhase observedPhase, ReproductiveContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.AllowsCyclePhaseTrainingAdjustments &&
               Enum.IsDefined(observedPhase) && observedPhase != CyclePhase.Unknown
            ? observedPhase
            : CyclePhase.Unknown;
    }

    /// <summary>The adjustment already inside a stored target, so recomputing does not apply it twice.</summary>
    public static float CalculateExistingCalorieAdjustment(CyclePhase targetPhase, UserGoal goal)
    {
        if (goal is UserGoal.FatLoss && targetPhase is CyclePhase.Menstrual)
            return NeutralCaloriesAdjustment;

        return targetPhase switch
        {
            CyclePhase.Menstrual => MenstrualCaloriesAdjustment,
            CyclePhase.Ovulatory => OvulatoryCaloriesAdjustment,
            CyclePhase.Luteal => LutealCaloriesAdjustment,
            _ => NeutralCaloriesAdjustment
        };
    }
}
