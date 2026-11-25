using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.UseCases.Features.Profile.NutritionSettings;

/// <summary>Reads and saves the nutrition settings against the profile version.</summary>
public sealed class ProfileNutritionSettingsUseCase(IProfileNutritionSettingsStore store)
    : IProfileNutritionSettingsUseCase
{
    /// <summary>The settings with the version a save must send back, or null when no profile exists yet.</summary>
    public Task<NutritionSettingsProfileDto?> LoadAsync(CancellationToken cancellationToken = default)
    {
        return store.LoadAsync(cancellationToken);
    }

    /// <summary>Calculates the nutrition goals for the profile.</summary>
    public NutritionGoalsDto CalculateGoals(
        NutritionSettingsProfileDto profile, NutritionSettingsSelectionDto selection)
    {
        return store.CalculateGoals(profile, Normalize(selection));
    }

    /// <summary>Applies her edits onto the computed goals, so a field she did not touch keeps its value.</summary>
    public NutritionGoalsDto MergeGoals(NutritionGoalsDto saved, NutritionGoalsDto calculated)
    {
        return NutritionGoalsDtoMapper.ToDto(
            NutritionGoalsDtoMapper.ToDomain(saved).WithFallbacks(NutritionGoalsDtoMapper.ToDomain(calculated)));
    }

    /// <summary>Whether she changed any target, which decides if the goals are stored as overrides.</summary>
    public bool HasOverrides(NutritionGoalsDto edited, NutritionGoalsDto calculated)
    {
        return NutritionGoalsDtoMapper.ToDomain(edited)
            .OnlyOverridesFrom(NutritionGoalsDtoMapper.ToDomain(calculated)).HasAnyValue;
    }

    /// <summary>Re-splits the macros when the user overrides one.</summary>
    public NutritionMacroTargetsDto? RebalanceMacros(NutritionSettingsProfileDto profile,
        NutritionSettingsSelectionDto selection, float calories)
    {
        var calculated = NutritionGoalsDtoMapper.ToDomain(CalculateGoals(profile, selection));
        var result = ProfileNutritionTargetsRule.RebalanceMacros(
            calculated, selection.Goal, profile.Phase, profile.Weight, calories);
        return result is null
            ? null
            : new NutritionMacroTargetsDto(
                result.Protein, result.Carbs, result.Fats, result.Fiber);
    }

    /// <summary>Calculates the calorie target for the profile.</summary>
    public NutritionCaloriesTargetsDto? CalculateCalories(NutritionSettingsProfileDto profile,
        float protein, float carbs, float fats)
    {
        var result = ProfileNutritionTargetsRule.CalculateCalories(protein, carbs, fats, profile.Phase);
        return result is null ? null : new NutritionCaloriesTargetsDto(result.Calories, result.Fiber);
    }

    /// <summary>Saves the nutrition settings.</summary>
    public async Task<bool> SaveAsync(NutritionSettingsSaveDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var selection = Normalize(request.Selection);
        NutritionGoalsDto? overrides = null;
        if (request.EditedGoals is not null)
        {
            var calculated = NutritionGoalsDtoMapper.ToDomain(store.CalculateGoals(request.Profile, selection));
            overrides = NutritionGoalsDtoMapper.ToDto(
                NutritionGoalsDtoMapper.ToDomain(request.EditedGoals).OnlyOverridesFrom(calculated));
        }

        return await store.SaveAsync(selection, overrides, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reduces the selection to the targets she actually set, so stale flags are dropped.</summary>
    private static NutritionSettingsSelectionDto Normalize(NutritionSettingsSelectionDto selection)
    {
        var goal = Enum.IsDefined(selection.Goal) ? selection.Goal : UserGoal.MaintainHealth;
        return selection with
        {
            Goal = goal,
            WeightGoalPace = goal is UserGoal.FatLoss or UserGoal.Strength &&
                             Enum.IsDefined(selection.WeightGoalPace)
                ? selection.WeightGoalPace
                : WeightGoalPace.Steady
        };
    }
}
