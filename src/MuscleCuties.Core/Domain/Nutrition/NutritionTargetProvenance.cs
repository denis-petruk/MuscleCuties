using MuscleCuties.Core.Models.Nutrition.Planning;

namespace MuscleCuties.Core.Domain.Nutrition;

/// <summary>Where a target came from, so a computed value is never presented as one she set.</summary>
public enum NutritionTargetSource
{
    Unavailable,
    GeneralStarting,
    Calculated,
    SavedOverride
}

/// <summary>The provenance of one target, with the inputs that produced it.</summary>
public sealed class NutritionTargetProvenance
{
    private const float NoAdjustment = 0f;

    /// <summary>Records the inputs and the outcome of one target calculation.</summary>
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

    /// <summary>Which rule produced the shown target.</summary>
    public NutritionTargetSource Source { get; }
    /// <summary>Energy the calculation started from, before adjustments.</summary>
    public float? StartingEnergyCalories { get; }
    /// <summary>Change the goal applied to the starting energy.</summary>
    public float? GoalAdjustmentCalories { get; }
    /// <summary>Change the cycle phase applied to the target.</summary>
    public float? PhaseAdjustmentCalories { get; }
    /// <summary>Target the calculation produced.</summary>
    public float? CalculatedCalories { get; }
    /// <summary>Target the user asked to save, when they overrode it.</summary>
    public float? SavedRequestedCalories { get; }
    /// <summary>Target the screens display.</summary>
    public float ShownCalories { get; }
    /// <summary>True when the saved target carries macro values the user set.</summary>
    public bool HasSavedMacroOverride { get; }
    /// <summary>True when the saved target differs from the one being shown, so a save is pending.</summary>
    public bool SavedRequestDiffersFromShownTarget =>
        SavedRequestedCalories.HasValue && SavedRequestedCalories.Value != ShownCalories;

    /// <summary>Provenance for a target shown before any personalised calculation.</summary>
    public static NutritionTargetProvenance GeneralStarting(float shownCalories) =>
        new(NutritionTargetSource.GeneralStarting, null, null, null, null, null,
            SafePositive(shownCalories), false);

    /// <summary>Provenance for a target that could not be calculated.</summary>
    public static NutritionTargetProvenance Unavailable(float shownCalories) =>
        new(NutritionTargetSource.Unavailable, null, null, null, null, null,
            SafePositive(shownCalories), false);

    /// <summary>Provenance for a target the rules recomputed from the profile.</summary>
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

    /// <summary>Returns the value when it is finite and positive, otherwise zero.</summary>
    private static float SafePositive(float value) =>
        float.IsFinite(value) && value > NoAdjustment ? value : NoAdjustment;
}
