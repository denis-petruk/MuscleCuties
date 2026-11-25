namespace MuscleCuties.Core.Services.Auth;

/// <summary>The states Apple reports for a stored credential, which decide whether a sign-in is still valid.</summary>
public enum AppleCredentialState
{
    Authorized = 1,
    Revoked = 2,
    NotFound = 3,
    Transferred = 4,
    Unsupported = 5,
    Error = 6
}
