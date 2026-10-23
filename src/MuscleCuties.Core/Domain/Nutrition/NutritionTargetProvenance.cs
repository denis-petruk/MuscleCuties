using MuscleCuties.Core.Models.Nutrition.Planning;

namespace MuscleCuties.Core.Domain.Nutrition;

public enum NutritionTargetSource
{
    Unavailable,
    GeneralStarting,
    Calculated,
    SavedOverride
}

public sealed class NutritionTargetProvenance
{
    private const float NoAdjustment = 0f;

    private NutritionTargetProvenance(
        NutritionTargetSource source,
        float? startingEnergyCalories,
        float? goalAdjustmentCalories,
        float? phaseAdjustmentCalories,
        float? calculatedCalories,
        float? savedRequestedCalories,
        float shownCalories,
        bool hasSavedMacroOverride)
    {
        Source = source;
        StartingEnergyCalories = startingEnergyCalories;
        GoalAdjustmentCalories = goalAdjustmentCalories;
        PhaseAdjustmentCalories = phaseAdjustmentCalories;
        CalculatedCalories = calculatedCalories;
        SavedRequestedCalories = savedRequestedCalories;
        ShownCalories = shownCalories;
        HasSavedMacroOverride = hasSavedMacroOverride;
    }

    public NutritionTargetSource Source { get; }
    public float? StartingEnergyCalories { get; }
    public float? GoalAdjustmentCalories { get; }
    public float? PhaseAdjustmentCalories { get; }
    public float? CalculatedCalories { get; }
    public float? SavedRequestedCalories { get; }
    public float ShownCalories { get; }
    public bool HasSavedMacroOverride { get; }
    public bool SavedRequestDiffersFromShownTarget =>
        SavedRequestedCalories.HasValue && SavedRequestedCalories.Value != ShownCalories;

    public static NutritionTargetProvenance GeneralStarting(float shownCalories) =>
        new(NutritionTargetSource.GeneralStarting, null, null, null, null, null,
            SafePositive(shownCalories), false);

    public static NutritionTargetProvenance Unavailable(float shownCalories) =>
        new(NutritionTargetSource.Unavailable, null, null, null, null, null,
            SafePositive(shownCalories), false);

    public static NutritionTargetProvenance Calculated(
        float startingEnergyCalories,
        float goalAdjustmentCalories,
        float phaseAdjustmentCalories,
        float calculatedCalories,
        float shownCalories,
        ProfileNutritionGoals savedGoals,
        ProfileNutritionGoals calculatedGoals,
        bool savedMacroOverridesRetained)
    {
        ArgumentNullException.ThrowIfNull(savedGoals);
        ArgumentNullException.ThrowIfNull(calculatedGoals);

        if (!float.IsFinite(startingEnergyCalories) || startingEnergyCalories <= NoAdjustment ||
            !float.IsFinite(goalAdjustmentCalories) || !float.IsFinite(phaseAdjustmentCalories) ||
            !float.IsFinite(calculatedCalories) || calculatedCalories <= NoAdjustment ||
            !float.IsFinite(shownCalories) || shownCalories <= NoAdjustment)
            return Unavailable(shownCalories);

        var overrides = savedGoals.OnlyOverridesFrom(calculatedGoals);
        var savedRequestedCalories = overrides.Calories;
        var hasSavedMacroOverride = savedMacroOverridesRetained &&
                                    (overrides.Protein.HasValue || overrides.Carbs.HasValue ||
                                     overrides.Fats.HasValue);
        var source = savedRequestedCalories.HasValue || hasSavedMacroOverride
            ? NutritionTargetSource.SavedOverride
            : NutritionTargetSource.Calculated;

        return new NutritionTargetProvenance(
            source,
            startingEnergyCalories,
            goalAdjustmentCalories,
            phaseAdjustmentCalories == NoAdjustment ? null : phaseAdjustmentCalories,
            calculatedCalories,
            savedRequestedCalories,
            shownCalories,
            hasSavedMacroOverride);
    }

    private static float SafePositive(float value) =>
        float.IsFinite(value) && value > NoAdjustment ? value : NoAdjustment;
}
