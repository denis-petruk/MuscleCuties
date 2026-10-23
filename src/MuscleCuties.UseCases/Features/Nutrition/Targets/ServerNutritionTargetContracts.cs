using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.UseCases.Features.Nutrition.Targets;

public sealed record ServerNutritionTargetProjection(
    float Calories,
    float Protein,
    float Carbs,
    float Fats,
    string Source,
    string SourceCode,
    float? StartingEnergyCalories,
    float? GoalAdjustmentCalories,
    float? PhaseAdjustmentCalories,
    float? CalculatedCalories,
    float? SavedRequestedCalories,
    bool HasSavedMacroOverride,
    bool SavedRequestDiffersFromShownTarget,
    CyclePhase Phase,
    string PhaseCode,
    string AvailabilityCode);

public interface IServerNutritionTargetProjectionStore
{
    Task<ServerNutritionTargetProjection> LoadAsync(Guid userId, DateOnly date, CancellationToken cancellationToken);
}

public sealed class ServerNutritionTargetProjectionUseCase(IServerNutritionTargetProjectionStore store)
{
    private static readonly DateOnly MinimumDate = new(1970, 1, 1);
    private static readonly DateOnly MaximumDate = new(2100, 1, 1);

    public Task<ServerNutritionTargetProjection> LoadAsync(Guid userId, DateOnly date, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || date < MinimumDate || date > MaximumDate)
            throw new ArgumentOutOfRangeException(nameof(date));
        return store.LoadAsync(userId, date, cancellationToken);
    }
}
