using MuscleCuties.UseCases.Features.Workout.Session.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Injury;

/// <summary>The server-side injury read.</summary>
public interface IServerWorkoutInjuryStore
{
    Task<WorkoutInjuryImpactDto> GetImpactAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ExerciseContraindicationCheckDto> CheckContraindicationAsync(Guid userId, int exerciseDefinitionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkoutSwapCandidateDto>> GetSafeSwapCandidatesAsync(Guid userId, int exerciseDefinitionId, CancellationToken cancellationToken = default);
    Task<bool> SwapExerciseAsync(Guid userId, Guid workoutDayExerciseId, int newExerciseDefinitionId, bool savePreference, CancellationToken cancellationToken = default);
}
