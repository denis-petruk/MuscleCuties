namespace MuscleCuties.UseCases.Features.Account.Deletion;

/// <summary>Whether Apple confirmed the token revocation, and why not when it did not.</summary>
public sealed record AppleTokenRevocationResult(
    bool Succeeded,
    bool WasAppleAccount,
    string? FailureReason = null,
    string? FailureCode = null);

/// <summary>Revokes the Apple tokens of a deleted account.</summary>
public interface IAppleAccountTokenRevoker
{
    Task<string?> ExchangeAuthorizationCodeForRefreshTokenAsync(
        string? authorizationCode,
        CancellationToken cancellationToken = default);

    Task<AppleTokenRevocationResult> RevokeAccountTokenAsync(
        Guid userId,
        string? authorizationCode = null,
        CancellationToken cancellationToken = default);
}
