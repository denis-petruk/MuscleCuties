using MuscleCuties.UseCases.Features.Account.SignIn.Contracts;

namespace MuscleCuties.UseCases.Features.Account.SignIn;

public sealed class AccountSignInUseCase(IAccountSignInStore store) : IAccountSignInUseCase
{
    public async Task<AccountSignInResult> SignInWithPasswordAsync(
        PasswordSignInRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.EmailOrUsername) || string.IsNullOrWhiteSpace(request.Password))
            return new AccountSignInResult(false);

        var account = await store.SignInWithPasswordAsync(
            request.EmailOrUsername, request.Password, cancellationToken).ConfigureAwait(false);
        return account is null
            ? new AccountSignInResult(false)
            : new AccountSignInResult(true, account.IsOnboardingComplete);
    }

    public async Task<AccountSignInResult> SignInWithPlatformAsync(
        PlatformSignInRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.UserIdentifier))
            return new AccountSignInResult(false);

        var outcome = await store.SignInWithPlatformAsync(
            request.Provider,
            request.UserIdentifier,
            request.Email,
            request.FullName,
            request.IdentityToken,
            request.Nonce,
            request.NonceChallengeId,
            cancellationToken,
            request.AuthorizationCode).ConfigureAwait(false);
        return new AccountSignInResult(
            outcome.Succeeded,
            outcome.IsOnboardingComplete,
            outcome.IsPendingOnboarding,
            outcome.ErrorMessage);
    }
}
