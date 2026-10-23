using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Domain.Nutrition;

public static class NutritionTargetPhaseRule
{
    public const float MenstrualCaloriesAdjustment = -50f;
    public const float OvulatoryCaloriesAdjustment = 50f;
    public const float LutealCaloriesAdjustment = 150f;
    public const float NeutralCaloriesAdjustment = 0f;

    public static CyclePhase ForAutomaticTargets(CyclePhase observedPhase, ReproductiveContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.AllowsCyclePhaseTrainingAdjustments &&
               Enum.IsDefined(observedPhase) && observedPhase != CyclePhase.Unknown
            ? observedPhase
            : CyclePhase.Unknown;
    }

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
