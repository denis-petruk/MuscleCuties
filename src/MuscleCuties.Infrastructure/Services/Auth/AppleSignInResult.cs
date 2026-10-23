namespace MuscleCuties.Core.Services.Auth;

public sealed record AppleSignInResult(
    string UserIdentifier,
    string? Email,
    string? FullName,
    string? IdentityToken = null,
    string? Nonce = null,
    string? NonceChallengeId = null,
    string? AuthorizationCode = null);
