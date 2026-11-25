namespace MuscleCuties.UseCases.Features.Profile.Overview;

/// <summary>Who is signed in, as the purge needs to know.</summary>
public interface IProfileAccountSession
{
    Task<int> GetCurrentUserIdAsync(CancellationToken cancellationToken);
}

/// <summary>The deletion the coordinator drives.</summary>
public interface IProfileAccountDeletion
{
    Task<ProfileAccountDeletionResult> DeleteLocalAccountAsync(int userId, CancellationToken cancellationToken);
}

/// <summary>What the coordinated deletion removed.</summary>
public sealed record ProfileAccountDeletionResult(bool Succeeded, string? FailureMessage = null);

/// <summary>Runs the purge steps in order and clears the session only once they have all succeeded.</summary>
public sealed class ProfilePurgeCoordinator(
    IProfileAccountSession session,
    IProfileAccountDeletion deletion) : IProfilePurgePort
{
    /// <summary>Runs the purge steps in order, stopping at the first failure so nothing is reported as deleted that was not.</summary>
    public async Task<ProfilePurgeResult> PurgeCurrentAccountAsync(CancellationToken cancellationToken)
    {
        var userId = await session.GetCurrentUserIdAsync(cancellationToken).ConfigureAwait(false);
        if (userId <= 0)
            return new ProfilePurgeResult(false, false);

        var result = await deletion.DeleteLocalAccountAsync(userId, cancellationToken)
            .ConfigureAwait(false);
        return new ProfilePurgeResult(result.Succeeded, true, result.FailureMessage);
    }
}
