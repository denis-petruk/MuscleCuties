using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.UseCases.Features.Nutrition.Targets;

/// <summary>The targets projected for a day, with where each came from.</summary>
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

/// <summary>The projection's read.</summary>
public interface IServerNutritionTargetProjectionStore
{
    Task<ServerNutritionTargetProjection> LoadAsync(Guid userId, DateOnly date, CancellationToken cancellationToken);
}

/// <summary>Projects a day's targets from the profile and the phase.</summary>
public sealed class ServerNutritionTargetProjectionUseCase(IServerNutritionTargetProjectionStore store)
{
    private static readonly DateOnly MinimumDate = new(1970, 1, 1);
    private static readonly DateOnly MaximumDate = new(2100, 1, 1);

    /// <summary>Projects the day's targets from the stored profile and the cycle phase in force.</summary>
    public Task<ServerNutritionTargetProjection> LoadAsync(Guid userId, DateOnly date, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || date < MinimumDate || date > MaximumDate)
            throw new ArgumentOutOfRangeException(nameof(date));
        return store.LoadAsync(userId, date, cancellationToken);
    }
}
