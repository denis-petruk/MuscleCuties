namespace MuscleCuties.Core.Services.Profile;

public sealed record AccountPurgeResult(
    bool DatabaseRecordsDeleted,
    bool MediaFilesDeleted,
    bool SessionCleared,
    IReadOnlyList<string> Errors)
{
    public bool Succeeded => DatabaseRecordsDeleted && MediaFilesDeleted && SessionCleared && Errors.Count == 0;
}

public interface IAppAccountPurgeService
{
    Task<AccountPurgeResult> PurgeAccountDataAsync(int userId, CancellationToken ct = default);
    Task PurgeAllAccountDataAndKeysAsync();
}
