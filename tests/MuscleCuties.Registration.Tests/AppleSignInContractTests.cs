using MuscleCuties.UseCases.Features.Account.Deletion;
using MuscleCuties.UseCases.Features.Account.Deletion.Contracts;
using MuscleCuties.UseCases.Features.Account.SignIn;
using MuscleCuties.UseCases.Features.Account.SignIn.Contracts;
using Xunit;

namespace MuscleCuties.Registration.Tests;

public sealed class AppleSignInContractTests
{
    [Fact]
    public async Task AppleCredentialFieldsReachTheSignInStore()
    {
        var store = new RecordingSignInStore();
        var useCase = new AccountSignInUseCase(store);
        var request = new PlatformSignInRequest("apple", "synthetic-apple-user", null, null,
            "synthetic-identity", "synthetic-nonce", Guid.NewGuid().ToString("D"), "synthetic-auth-code");

        var result = await useCase.SignInWithPlatformAsync(request);

        Assert.True(result.Succeeded);
        Assert.Equal(request.IdentityToken, store.IdentityToken);
        Assert.Equal(request.Nonce, store.Nonce);
        Assert.Equal(request.NonceChallengeId, store.NonceChallengeId);
        Assert.Equal(request.AuthorizationCode, store.AuthorizationCode);
    }

    [Fact]
    public async Task AccountDeletionAbortsBeforePurgeWhenAppleTokenRevocationFails()
    {
        var purgeStore = new SpyPurgeStore();
        var revoker = new StubAppleRevoker(new AppleTokenRevocationResult(false, true, "Revocation rejected",
            AccountDeletionFailureCode.AppleAuthorizationRequired));
        var useCase = new ServerAccountDeletionUseCase(purgeStore, appleTokenRevoker: revoker);

        var result = await useCase.DeleteAccountAsync(Guid.CreateVersion7(), appleAuthorizationCode: "synthetic-auth-code");

        Assert.False(result.Succeeded);
        Assert.False(purgeStore.Purged);
        Assert.Equal("synthetic-auth-code", revoker.LastAuthorizationCode);
        Assert.Equal(AccountDeletionFailureCode.AppleAuthorizationRequired, result.FailureCode);
    }

    private sealed class RecordingSignInStore : IAccountSignInStore
    {
        public string? IdentityToken { get; private set; }
        public string? Nonce { get; private set; }
        public string? NonceChallengeId { get; private set; }
        public string? AuthorizationCode { get; private set; }

        public Task<SignedInAccount?> SignInWithPasswordAsync(
            string emailOrUsername, string password, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PlatformAccountOutcome> SignInWithPlatformAsync(
            string provider, string userIdentifier, string? email, string? fullName,
            string? identityToken, string? nonce, string? nonceChallengeId,
            CancellationToken cancellationToken = default,
            string? authorizationCode = null)
        {
            IdentityToken = identityToken;
            Nonce = nonce;
            NonceChallengeId = nonceChallengeId;
            AuthorizationCode = authorizationCode;
            return Task.FromResult(new PlatformAccountOutcome(true, false, true));
        }
    }

    private sealed class SpyPurgeStore : IServerAccountPurgeStore
    {
        public bool Purged { get; private set; }

        public Task<AccountDeletionResult> PurgeAccountDataAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            Purged = true;
            return Task.FromResult(new AccountDeletionResult(true, 1, [], "Purged"));
        }
    }

    private sealed class StubAppleRevoker(AppleTokenRevocationResult result) : IAppleAccountTokenRevoker
    {
        public string? LastAuthorizationCode { get; private set; }

        public Task<string?> ExchangeAuthorizationCodeForRefreshTokenAsync(string? authorizationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>("synthetic-refresh-token");

        public Task<AppleTokenRevocationResult> RevokeAccountTokenAsync(Guid userId, string? authorizationCode = null, CancellationToken cancellationToken = default)
        {
            LastAuthorizationCode = authorizationCode;
            return Task.FromResult(result);
        }
    }
}
