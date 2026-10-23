namespace MuscleCuties.Core.Services.Auth;

public enum AppleCredentialState
{
    Authorized = 1,
    Revoked = 2,
    NotFound = 3,
    Transferred = 4,
    Unsupported = 5,
    Error = 6
}
