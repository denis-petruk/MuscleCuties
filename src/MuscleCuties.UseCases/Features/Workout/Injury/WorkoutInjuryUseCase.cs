using MuscleCuties.Core.Domain.Workout;

namespace MuscleCuties.UseCases.Features.Workout.Injury;

/// <summary>Injury logging and the contraindication checks the session uses.</summary>
public interface IWorkoutInjuryUseCase : IWorkoutInjuryStore
{
}

/// <summary>Logs injuries and answers safety checks, refusing a swap that would break the session's structure.</summary>
public sealed class WorkoutInjuryUseCase : IWorkoutInjuryUseCase
{
    private readonly IWorkoutInjuryStore _store;

    /// <summary>Takes the injury store.</summary>
    public WorkoutInjuryUseCase(IWorkoutInjuryStore store)
    {
        _store = store;
    }

    /// <summary>What the recorded injuries block, which the session uses to filter exercises.</summary>
    public Task<WorkoutInjuryImpactDto> GetImpactAsync(CancellationToken ct = default)
    {
        return _store.GetImpactAsync(ct);
    }

    /// <summary>Her injury log, newest first.</summary>
    public Task<IReadOnlyList<WorkoutInjuryLogDto>> GetAllInjuriesAsync(CancellationToken ct = default)
    {
        return _store.GetAllInjuriesAsync(ct);
    }

    /// <summary>Stores an injury and marks the plan for regeneration, so safety is never stale.</summary>
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

    /// <summary>Marks the injury healed without deleting its history.</summary>
    public Task ClearInjuryAsync(int injuryId, CancellationToken ct = default)
    {
        if (injuryId <= 0)
            throw new ArgumentOutOfRangeException(nameof(injuryId), "Injury ID must be positive.");

        return _store.ClearInjuryAsync(injuryId, ct);
    }

    /// <summary>Removes the record entirely, for one entered by mistake.</summary>
    public Task DeleteInjuryAsync(int injuryId, CancellationToken ct = default)
    {
        if (injuryId <= 0)
            throw new ArgumentOutOfRangeException(nameof(injuryId), "Injury ID must be positive.");

        return _store.DeleteInjuryAsync(injuryId, ct);
    }

    /// <summary>Makes the planner rebuild, because a changed injury invalidates the plan.</summary>
    public Task RefreshPlanAfterInjuryChangeAsync(CancellationToken ct = default)
    {
        return _store.RefreshPlanAfterInjuryChangeAsync(ct);
    }
}
