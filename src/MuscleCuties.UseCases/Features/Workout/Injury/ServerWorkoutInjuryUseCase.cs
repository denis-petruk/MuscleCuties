using MuscleCuties.UseCases.Features.Workout.Session.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Injury;

public sealed class ServerWorkoutInjuryUseCase(IServerWorkoutInjuryStore store)
{
    public Task<WorkoutInjuryImpactDto> GetImpactAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentOutOfRangeException(nameof(userId), "A valid owner ID is required.");
        return store.GetImpactAsync(userId, cancellationToken);
    }

    public Task<ExerciseContraindicationCheckDto> CheckContraindicationAsync(
        Guid userId, int exerciseDefinitionId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentOutOfRangeException(nameof(userId), "A valid owner ID is required.");
        if (exerciseDefinitionId <= 0)
            throw new ArgumentOutOfRangeException(nameof(exerciseDefinitionId), "Exercise definition ID must be positive.");

        return store.CheckContraindicationAsync(userId, exerciseDefinitionId, cancellationToken);
    }

    public Task<IReadOnlyList<WorkoutSwapCandidateDto>> GetSafeSwapCandidatesAsync(
        Guid userId, int exerciseDefinitionId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentOutOfRangeException(nameof(userId), "A valid owner ID is required.");
        if (exerciseDefinitionId <= 0)
            throw new ArgumentOutOfRangeException(nameof(exerciseDefinitionId), "Exercise definition ID must be positive.");

        return store.GetSafeSwapCandidatesAsync(userId, exerciseDefinitionId, cancellationToken);
    }

    public Task<bool> SwapExerciseAsync(
        Guid userId, Guid workoutDayExerciseId, int newExerciseDefinitionId, bool savePreference, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentOutOfRangeException(nameof(userId), "A valid owner ID is required.");
        if (workoutDayExerciseId == Guid.Empty)
            throw new ArgumentOutOfRangeException(nameof(workoutDayExerciseId), "Workout day exercise ID is required.");
        if (newExerciseDefinitionId <= 0)
            throw new ArgumentOutOfRangeException(nameof(newExerciseDefinitionId), "New exercise definition ID must be positive.");

        return store.SwapExerciseAsync(userId, workoutDayExerciseId, newExerciseDefinitionId, savePreference, cancellationToken);
    }
}
