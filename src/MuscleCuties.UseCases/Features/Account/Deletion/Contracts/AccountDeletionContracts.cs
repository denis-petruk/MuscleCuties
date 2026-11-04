namespace MuscleCuties.UseCases.Features.Account.Deletion.Contracts;

public static class AccountDeletionFailureCode
{
    public const string AppleAuthorizationRequired = "appleAuthorizationRequired";
    public const string AppleRevocationFailed = "appleRevocationFailed";
}

public sealed record AccountDeletionRequest(string? AuthorizationCode = null);

public sealed record AccountDeletionResult(
    bool Succeeded,
    int PurgedRecordCount,
    IReadOnlyList<string> CleanedMediaKeys,
    string Message,
    string? FailureCode = null);
