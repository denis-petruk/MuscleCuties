using MuscleCuties.Infrastructure.ApiClient;

namespace MuscleCuties.Infrastructure.Cloud;

/// <summary>
/// The authenticated-but-incomplete onboarding account: which account the session token belongs to, and
/// the staging reset that must run exactly once per account so a second account never inherits the first
/// account's staged profile. Registration setup and the quiz both asked the token store themselves before.
/// </summary>
internal sealed class OnboardingAccountScope(IBackendSessionTokenStore? tokenStore)
{
    private readonly object _sync = new();
    private Guid? _claimedAccountId;

    public async Task<Guid?> ReadAsync(CancellationToken cancellationToken)
    {
        if (tokenStore is null)
            return null;

        var tokens = await tokenStore.ReadTokensAsync(cancellationToken).ConfigureAwait(false);
        return tokens is not null &&
               tokens.AccountId != Guid.Empty &&
               !string.IsNullOrWhiteSpace(tokens.AccessToken) &&
               !tokens.IsOnboardingComplete
            ? tokens.AccountId
            : null;
    }

    /// <summary>
    /// Claims the account for this session. Returns true when the caller must reset its staged state first;
    /// a repeat read for the same account returns false so staged answers survive a page revisit.
    /// </summary>
    public bool ClaimForReset(Guid accountId, Action resetStagedState)
    {
        ArgumentNullException.ThrowIfNull(resetStagedState);

        lock (_sync)
        {
            if (_claimedAccountId == accountId)
                return false;

            resetStagedState();
            _claimedAccountId = accountId;
            return true;
        }
    }
}
