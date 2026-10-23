namespace MuscleCuties.UseCases.Features.Profile.Overview;

public interface IProfileAccountSession
{
    Task<int> GetCurrentUserIdAsync(CancellationToken cancellationToken);
}

public interface IProfileAccountDeletion
{
    Task<bool> DeleteLocalAccountAsync(int userId, CancellationToken cancellationToken);
}

public sealed class ProfilePurgeCoordinator(
    IProfileAccountSession session,
    IProfileAccountDeletion deletion) : IProfilePurgePort
{
    public async Task<ProfilePurgeResult> PurgeCurrentAccountAsync(CancellationToken cancellationToken)
    {
        var userId = await session.GetCurrentUserIdAsync(cancellationToken).ConfigureAwait(false);
        if (userId <= 0)
            return new ProfilePurgeResult(false, false);

        var succeeded = await deletion.DeleteLocalAccountAsync(userId, cancellationToken)
            .ConfigureAwait(false);
        return new ProfilePurgeResult(succeeded, true);
    }
}
