using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Domain.Nutrition;

public enum NutritionPhaseAvailability
{
    AccountUnavailable,
    CycleUnavailable,
    TargetGuidanceUnavailable,
    Available
}

public sealed record NutritionPhaseState(CyclePhase Phase, NutritionPhaseAvailability Availability);

public static class NutritionPhaseAvailabilityRule
{
    public static NutritionPhaseState Resolve(bool hasAccount, CyclePhase phase)
    {
        if (!hasAccount)
            return new NutritionPhaseState(CyclePhase.Unknown, NutritionPhaseAvailability.AccountUnavailable);

        return Enum.IsDefined(phase) && phase != CyclePhase.Unknown
            ? new NutritionPhaseState(phase, NutritionPhaseAvailability.Available)
            : new NutritionPhaseState(CyclePhase.Unknown, NutritionPhaseAvailability.CycleUnavailable);
    }

    public static NutritionPhaseState ResolveForTargets(
        bool hasAccount, CyclePhase observedPhase, CyclePhase targetPhase)
    {
        var observed = Resolve(hasAccount, observedPhase);
        if (observed.Availability is not NutritionPhaseAvailability.Available)
            return observed;

        if (Resolve(hasAccount, targetPhase).Availability is not NutritionPhaseAvailability.Available)
            return new NutritionPhaseState(CyclePhase.Unknown,
                NutritionPhaseAvailability.TargetGuidanceUnavailable);

        return Resolve(hasAccount, targetPhase);
    }
}
