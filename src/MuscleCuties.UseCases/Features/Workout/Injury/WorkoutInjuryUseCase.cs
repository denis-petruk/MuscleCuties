using MuscleCuties.Core.Domain.Workout;

namespace MuscleCuties.UseCases.Features.Workout.Injury;

public interface IWorkoutInjuryUseCase
{
    Task<WorkoutInjuryImpactDto> GetImpactAsync(CancellationToken ct = default);
    Task<IReadOnlyList<WorkoutInjuryLogDto>> GetAllInjuriesAsync(CancellationToken ct = default);
    Task SaveInjuryAsync(SaveWorkoutInjuryInput input, CancellationToken ct = default);
    Task ClearInjuryAsync(int injuryId, CancellationToken ct = default);
    Task DeleteInjuryAsync(int injuryId, CancellationToken ct = default);
    Task RefreshPlanAfterInjuryChangeAsync(CancellationToken ct = default);
}

public sealed class WorkoutInjuryUseCase : IWorkoutInjuryUseCase
{
    private readonly IWorkoutInjuryStore _store;

    public WorkoutInjuryUseCase(IWorkoutInjuryStore store)
    {
        _store = store;
    }

    public Task<WorkoutInjuryImpactDto> GetImpactAsync(CancellationToken ct = default)
    {
        return _store.GetImpactAsync(ct);
    }

    public Task<IReadOnlyList<WorkoutInjuryLogDto>> GetAllInjuriesAsync(CancellationToken ct = default)
    {
        return _store.GetAllInjuriesAsync(ct);
    }

    public Task SaveInjuryAsync(SaveWorkoutInjuryInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!WorkoutInjuryEntryRule.TryNormalize(
                (int)input.Site,
                input.Pain,
                input.Since,
                DateOnly.FromDateTime(DateTime.Today),
                out var normalizedEntry))
            throw new ArgumentException("Injury site is required.");

        var normalizedInput = input with
        {
            Pain = normalizedEntry.Pain,
            Since = normalizedEntry.Since
        };

        return _store.SaveInjuryAsync(normalizedInput, ct);
    }

    public Task ClearInjuryAsync(int injuryId, CancellationToken ct = default)
    {
        if (injuryId <= 0)
            throw new ArgumentOutOfRangeException(nameof(injuryId), "Injury ID must be positive.");

        return _store.ClearInjuryAsync(injuryId, ct);
    }

    public Task DeleteInjuryAsync(int injuryId, CancellationToken ct = default)
    {
        if (injuryId <= 0)
            throw new ArgumentOutOfRangeException(nameof(injuryId), "Injury ID must be positive.");

        return _store.DeleteInjuryAsync(injuryId, ct);
    }

    public Task RefreshPlanAfterInjuryChangeAsync(CancellationToken ct = default)
    {
        return _store.RefreshPlanAfterInjuryChangeAsync(ct);
    }
}
