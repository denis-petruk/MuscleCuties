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
}
