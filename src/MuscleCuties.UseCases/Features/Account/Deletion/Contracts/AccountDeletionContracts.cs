namespace MuscleCuties.UseCases.Features.Account.Deletion.Contracts;

public sealed record AccountDeletionResult(
    bool Succeeded,
    int PurgedRecordCount,
    IReadOnlyList<string> CleanedMediaKeys,
    string Message);
