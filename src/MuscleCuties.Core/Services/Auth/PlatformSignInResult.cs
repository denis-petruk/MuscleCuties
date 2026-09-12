namespace MuscleCuties.Core.Services.Auth;

public sealed record PlatformSignInResult(
    string Provider,
    string UserIdentifier,
    string? Email,
    string? FullName);
