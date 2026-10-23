namespace MuscleCuties.UseCases.Features.Progress;

public sealed record ProgressSummaryDto(
    int CompletedWorkoutSessions,
    int WorkoutStreakDays,
    int NutritionStreakDays);

public interface IProgressSummaryStore
{
    Task<ProgressSummaryDto> GetSummaryAsync(DateOnly date, int lookbackDays = 90, CancellationToken ct = default);
}

public interface IProgressSummaryUseCase
{
    Task<ProgressSummaryDto> GetSummaryAsync(DateOnly date, int lookbackDays = 90, CancellationToken ct = default);
}

public sealed class ProgressSummaryUseCase : IProgressSummaryUseCase
{
    private readonly IProgressSummaryStore _store;

    public ProgressSummaryUseCase(IProgressSummaryStore store)
    {
        _store = store;
    }

    public Task<ProgressSummaryDto> GetSummaryAsync(DateOnly date, int lookbackDays = 90,
        CancellationToken ct = default)
    {
        var safeLookback = Math.Clamp(lookbackDays, 1, 365);
        return _store.GetSummaryAsync(date, safeLookback, ct);
    }
}
