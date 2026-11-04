namespace MuscleCuties.UseCases.Features.Account.Deletion;

public sealed record AppleTokenRevocationResult(
    bool Succeeded,
    bool WasAppleAccount,
    string? FailureReason = null,
    string? FailureCode = null);

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
