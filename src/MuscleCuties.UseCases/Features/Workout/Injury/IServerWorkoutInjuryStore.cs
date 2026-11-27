namespace MuscleCuties.UseCases.Features.Workout.Injury;

/// <summary>The server-side injury read.</summary>
public interface IServerWorkoutInjuryStore
{
    Task<WorkoutInjuryImpactDto> GetImpactAsync(Guid userId, CancellationToken cancellationToken = default);
}
