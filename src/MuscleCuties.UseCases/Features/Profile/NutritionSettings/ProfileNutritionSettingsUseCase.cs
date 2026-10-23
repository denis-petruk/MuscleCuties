using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.UseCases.Features.Profile.NutritionSettings;

public sealed class ProfileNutritionSettingsUseCase(IProfileNutritionSettingsStore store)
    : IProfileNutritionSettingsUseCase
{
    public Task<NutritionSettingsProfileDto?> LoadAsync(CancellationToken cancellationToken = default)
    {
        return store.LoadAsync(cancellationToken);
    }

    public NutritionGoalsDto CalculateGoals(
        NutritionSettingsProfileDto profile, NutritionSettingsSelectionDto selection)
    {
        return store.CalculateGoals(profile, Normalize(selection));
    }

    public NutritionGoalsDto MergeGoals(NutritionGoalsDto saved, NutritionGoalsDto calculated)
    {
        return NutritionGoalsDtoMapper.ToDto(
            NutritionGoalsDtoMapper.ToDomain(saved).WithFallbacks(NutritionGoalsDtoMapper.ToDomain(calculated)));
    }

    public bool HasOverrides(NutritionGoalsDto edited, NutritionGoalsDto calculated)
    {
        return NutritionGoalsDtoMapper.ToDomain(edited)
            .OnlyOverridesFrom(NutritionGoalsDtoMapper.ToDomain(calculated)).HasAnyValue;
    }

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

    public NutritionCaloriesTargetsDto? CalculateCalories(NutritionSettingsProfileDto profile,
        float protein, float carbs, float fats)
    {
        var result = ProfileNutritionTargetsRule.CalculateCalories(protein, carbs, fats, profile.Phase);
        return result is null ? null : new NutritionCaloriesTargetsDto(result.Calories, result.Fiber);
    }

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
