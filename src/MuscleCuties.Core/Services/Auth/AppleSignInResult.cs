namespace MuscleCuties.Core.Services.Auth;

public sealed record AppleSignInResult(
    string UserIdentifier,
    string? Email,
    string? FullName);
