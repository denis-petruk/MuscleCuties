

namespace MuscleCuties.Infrastructure.Cloud;

/// <summary>The signed-in account's identity as the profile pages need it.</summary>
public sealed class CloudProfileAccountSession(
    IBackendSessionTokenStore tokenStore) : IProfileAccountSession
{
    /// <summary>The current account's local id, which is what every profile store is keyed by.</summary>
    public async Task<int> GetCurrentUserIdAsync(CancellationToken cancellationToken)
    {
        var tokens = await tokenStore.ReadTokensAsync(cancellationToken).ConfigureAwait(false);
        return tokens is not null && tokens.AccountId != Guid.Empty
            ? Math.Abs(tokens.AccountId.GetHashCode())
            : 0;
    }
}
