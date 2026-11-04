using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Profile;
using MuscleCuties.UseCases.Features.Profile.Overview;

namespace MuscleCuties.UI.Services.Profile;

public sealed class LocalProfileAccountSession(IServiceScopeFactory scopeFactory) : IProfileAccountSession
{
    public async Task<int> GetCurrentUserIdAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAuthService>()
            .GetCurrentUserIdAsync().ConfigureAwait(false);
    }
}

public sealed class LocalProfileAccountDeletion(
    IAppAccountPurgeService purgeService,
    IProfileAccountSession session) : IProfileAccountDeletion
{
    public async Task<ProfileAccountDeletionResult> DeleteLocalAccountAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (userId <= 0 || await session.GetCurrentUserIdAsync(cancellationToken).ConfigureAwait(false) != userId)
            return new ProfileAccountDeletionResult(false,
                "Could not confirm the signed-in account. Sign in again, then retry account deletion.");

        var result = await purgeService.PurgeAccountDataAsync(userId, cancellationToken)
            .ConfigureAwait(false);
        return new ProfileAccountDeletionResult(result.Succeeded,
            result.Errors.Count > 0 ? result.Errors[0] : null);
    }
}
