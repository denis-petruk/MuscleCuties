namespace MuscleCuties.UseCases.Features.Workout.Injury;

/// <summary>The injury persistence.</summary>
public interface IWorkoutInjuryStore
{
    Task<WorkoutInjuryImpactDto> GetImpactAsync(CancellationToken ct = default);
    Task<IReadOnlyList<WorkoutInjuryLogDto>> GetAllInjuriesAsync(CancellationToken ct = default);
    Task SaveInjuryAsync(SaveWorkoutInjuryInput input, CancellationToken ct = default);
    Task ClearInjuryAsync(int injuryId, CancellationToken ct = default);
    Task DeleteInjuryAsync(int injuryId, CancellationToken ct = default);
    Task RefreshPlanAfterInjuryChangeAsync(CancellationToken ct = default);
}
