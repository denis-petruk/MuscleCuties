using MuscleCuties.UseCases.Features.Workout.Session.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Injury;

/// <summary>The server's injury read.</summary>
public sealed class ServerWorkoutInjuryUseCase(IServerWorkoutInjuryStore store)
{
    /// <summary>What the account's recorded injuries currently block.</summary>
    public Task<WorkoutInjuryImpactDto> GetImpactAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentOutOfRangeException(nameof(userId), "A valid owner ID is required.");
        return store.GetImpactAsync(userId, cancellationToken);
    }

    /// <summary>Checks whether an injury rules out the exercise.</summary>
    public Task<ExerciseContraindicationCheckDto> CheckContraindicationAsync(
        Guid userId, int exerciseDefinitionId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentOutOfRangeException(nameof(userId), "A valid owner ID is required.");
        if (exerciseDefinitionId <= 0)
            throw new ArgumentOutOfRangeException(nameof(exerciseDefinitionId), "Exercise definition ID must be positive.");

        return store.CheckContraindicationAsync(userId, exerciseDefinitionId, cancellationToken);
    }

    /// <summary>The swap candidates that are safe for the user's injuries.</summary>
    public Task<IReadOnlyList<WorkoutSwapCandidateDto>> GetSafeSwapCandidatesAsync(
        Guid userId, int exerciseDefinitionId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentOutOfRangeException(nameof(userId), "A valid owner ID is required.");
        if (exerciseDefinitionId <= 0)
            throw new ArgumentOutOfRangeException(nameof(exerciseDefinitionId), "Exercise definition ID must be positive.");

        return store.GetSafeSwapCandidatesAsync(userId, exerciseDefinitionId, cancellationToken);
    }

    /// <summary>Swaps the exercise for a safe candidate.</summary>
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
