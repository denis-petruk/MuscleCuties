namespace MuscleCuties.UseCases.Features.Account.Deletion.Contracts;

/// <summary>The stable codes a refused deletion returns, so the client can act without parsing text.</summary>
public static class AccountDeletionFailureCode
{
    public const string AppleAuthorizationRequired = "appleAuthorizationRequired";
    public const string AppleRevocationFailed = "appleRevocationFailed";
}

/// <summary>A deletion request carrying the confirmation the server demands.</summary>
public sealed record AccountDeletionRequest(string? AuthorizationCode = null);

/// <summary>What a deletion did, including the parts a provider had to confirm.</summary>
public sealed record AccountDeletionResult(
    bool Succeeded,
    int PurgedRecordCount,
    IReadOnlyList<string> CleanedMediaKeys,
    string Message,
    string? FailureCode = null);
