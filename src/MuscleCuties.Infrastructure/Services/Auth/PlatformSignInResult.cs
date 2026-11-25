namespace MuscleCuties.Core.Services.Auth;

/// <summary>What a platform sign-in produced: the account, or the reason it did not.</summary>
public sealed record PlatformSignInResult(
    string Provider,
    string UserIdentifier,
    string? Email,
    string? FullName,
    string? IdentityToken = null,
    string? Nonce = null,
    string? NonceChallengeId = null,
    string? AuthorizationCode = null);
