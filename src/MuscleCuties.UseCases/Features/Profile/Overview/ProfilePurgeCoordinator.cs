namespace MuscleCuties.UseCases.Features.Profile.Overview;

public interface IProfileAccountSession
{
    Task<int> GetCurrentUserIdAsync(CancellationToken cancellationToken);
}

public interface IProfileAccountDeletion
{
    Task<ProfileAccountDeletionResult> DeleteLocalAccountAsync(int userId, CancellationToken cancellationToken);
}

public sealed record ProfileAccountDeletionResult(bool Succeeded, string? FailureMessage = null);

public sealed class ProfilePurgeCoordinator(
    IProfileAccountSession session,
    IProfileAccountDeletion deletion) : IProfilePurgePort
{
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
