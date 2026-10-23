namespace MuscleCuties.UseCases.Features.Profile.NutritionSettings;

public interface IProfileNutritionSettingsStore
{
    Task<NutritionSettingsProfileDto?> LoadAsync(CancellationToken cancellationToken);
    NutritionGoalsDto CalculateGoals(NutritionSettingsProfileDto profile, NutritionSettingsSelectionDto selection);

    Task<bool> SaveAsync(NutritionSettingsSelectionDto selection, NutritionGoalsDto? goalOverrides,
        CancellationToken cancellationToken);
}
