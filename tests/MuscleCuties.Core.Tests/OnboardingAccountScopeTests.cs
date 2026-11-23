using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.Infrastructure.Cloud;
using Xunit;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// The setup store and the quiz store used to read the session token and reset staged state themselves.
/// These assertions pin the one owner both now use.
/// </summary>
public sealed class OnboardingAccountScopeTests
{
    [Fact]
    public async Task Incomplete_onboarding_account_is_returned()
    {
        var accountId = Guid.NewGuid();
        var scope = new OnboardingAccountScope(new TokenStore(Tokens(accountId, onboardingComplete: false)));

        Assert.Equal(accountId, await scope.ReadAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData(true)]
    public async Task Completed_onboarding_account_is_not_an_incomplete_scope(bool onboardingComplete)
    {
        var scope = new OnboardingAccountScope(new TokenStore(Tokens(Guid.NewGuid(), onboardingComplete)));

        Assert.Null(await scope.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Empty_account_or_missing_access_token_is_not_an_incomplete_scope()
    {
        Assert.Null(await new OnboardingAccountScope(new TokenStore(Tokens(Guid.Empty, false)))
            .ReadAsync(CancellationToken.None));
        Assert.Null(await new OnboardingAccountScope(
                new TokenStore(Tokens(Guid.NewGuid(), false) with { AccessToken = string.Empty }))
            .ReadAsync(CancellationToken.None));
        Assert.Null(await new OnboardingAccountScope(new TokenStore(null)).ReadAsync(CancellationToken.None));
        Assert.Null(await new OnboardingAccountScope(null).ReadAsync(CancellationToken.None));
    }

    [Fact]
    public void Staged_state_resets_once_per_account_and_again_for_a_new_account()
    {
        var scope = new OnboardingAccountScope(null);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var resets = 0;

        Assert.True(scope.ClaimForReset(first, () => resets++));
        Assert.False(scope.ClaimForReset(first, () => resets++));
        Assert.Equal(1, resets);

        Assert.True(scope.ClaimForReset(second, () => resets++));
        Assert.Equal(2, resets);
    }

    private static BackendSessionTokens Tokens(Guid accountId, bool onboardingComplete) =>
        new(accountId, "access-token", "refresh-token", DateTimeOffset.UtcNow.AddHours(1), onboardingComplete);

    private sealed class TokenStore(BackendSessionTokens? tokens) : IBackendSessionTokenStore
    {
        public Task<BackendSessionTokens?> ReadTokensAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(tokens);

        public Task WriteTokensAsync(BackendSessionTokens newTokens, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ClearTokensAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
