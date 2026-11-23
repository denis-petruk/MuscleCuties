

namespace MuscleCuties.Infrastructure.Cloud;

public sealed class CloudProfileAccountSession(
    IBackendSessionTokenStore tokenStore) : IProfileAccountSession
{
    public async Task<int> GetCurrentUserIdAsync(CancellationToken cancellationToken)
    {
        var tokens = await tokenStore.ReadTokensAsync(cancellationToken).ConfigureAwait(false);
        return tokens is not null && tokens.AccountId != Guid.Empty
            ? Math.Abs(tokens.AccountId.GetHashCode())
            : 0;
    }
}
