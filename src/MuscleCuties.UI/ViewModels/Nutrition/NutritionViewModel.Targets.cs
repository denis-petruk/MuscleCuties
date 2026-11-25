// Per-meal calorie target shares.
// Second half of NutritionViewModel; see its declaration for the full contract.

namespace MuscleCuties.UI.ViewModels.Nutrition;

/// <summary>Applies the plan's calorie and macro targets, including the per-meal split the editor's shares depend</summary>
/// on.</summary>
public partial class NutritionViewModel
{
    private const float NoTargetAdjustment = 0f;
    private const string GeneralStartingSource = "GeneralStarting";
    private const string UnavailableSource = "Unavailable";

    [ObservableProperty] private bool _hasStartingEnergy;
    [ObservableProperty] private bool _hasGoalAdjustment;
    [ObservableProperty] private bool _hasPhaseAdjustment;
    [ObservableProperty] private bool _hasCalculatedTarget;
    [ObservableProperty] private bool _hasSavedCalorieRequest;
    [ObservableProperty] private bool _hasSavedMacroOverride;
    [ObservableProperty] private bool _savedRequestDiffersFromShownTarget;
    [ObservableProperty] private bool _hasGeneralStartingNotice;
    [ObservableProperty] private bool _hasUnavailableBreakdown;
    [ObservableProperty] private string _startingEnergyText = string.Empty;
    [ObservableProperty] private string _goalAdjustmentText = string.Empty;
    [ObservableProperty] private string _phaseAdjustmentText = string.Empty;
    [ObservableProperty] private string _calculatedTargetText = string.Empty;
    [ObservableProperty] private string _savedCalorieRequestText = string.Empty;
    [ObservableProperty] private bool _hasActivityAdjustment;
    [ObservableProperty] private string _activityAdjustmentText = string.Empty;
    [ObservableProperty] private float _activityAdjustmentCalories;

    /// <summary>Sets the targets from a breakdown; a missing breakdown leaves the current targets in place rather than</summary>
    /// clearing them to zero.</summary>
    private void ApplyTargetBreakdown(NutritionTargetBreakdown? breakdown)
    {
        HasStartingEnergy = breakdown?.StartingEnergyCalories is { } starting && float.IsFinite(starting);
        StartingEnergyText = HasStartingEnergy
            ? FormatCalories(breakdown!.StartingEnergyCalories!.Value)
            : string.Empty;

        HasGoalAdjustment = breakdown?.GoalAdjustmentCalories is { } goal &&
                            float.IsFinite(goal) && goal != NoTargetAdjustment;
        GoalAdjustmentText = HasGoalAdjustment
            ? FormatSignedCalories(breakdown!.GoalAdjustmentCalories!.Value)
            : string.Empty;

        HasPhaseAdjustment = breakdown?.PhaseAdjustmentCalories is { } phase &&
                             float.IsFinite(phase) && phase != NoTargetAdjustment;
        PhaseAdjustmentText = HasPhaseAdjustment
            ? FormatSignedCalories(breakdown!.PhaseAdjustmentCalories!.Value)
            : string.Empty;

        HasCalculatedTarget = breakdown?.CalculatedCalories is { } calculated && float.IsFinite(calculated);
        CalculatedTargetText = HasCalculatedTarget
            ? FormatCalories(breakdown!.CalculatedCalories!.Value)
            : string.Empty;

        HasSavedCalorieRequest = breakdown?.SavedRequestedCalories is { } saved && float.IsFinite(saved);
        SavedCalorieRequestText = HasSavedCalorieRequest
            ? FormatCalories(breakdown!.SavedRequestedCalories!.Value)
            : string.Empty;

        HasSavedMacroOverride = breakdown?.HasSavedMacroOverride == true;
        SavedRequestDiffersFromShownTarget = breakdown?.SavedRequestDiffersFromShownTarget == true;
        HasGeneralStartingNotice = breakdown?.Source == GeneralStartingSource;
        HasUnavailableBreakdown = breakdown is null || breakdown.Source == UnavailableSource;
    }

    /// <summary>The calories line for a value.</summary>
    private static string FormatCalories(float value) => DisplayFormat.Calories(value);

    /// <summary>The calories line with an explicit sign.</summary>
    private static string FormatSignedCalories(float value) =>
        value > NoTargetAdjustment ? $"+{FormatCalories(value)}" : FormatCalories(value);
}
