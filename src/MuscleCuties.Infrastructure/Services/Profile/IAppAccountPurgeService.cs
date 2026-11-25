namespace MuscleCuties.Core.Services.Profile;

/// <summary>What an account purge removed, so the UI can confirm it rather than assume.</summary>
public sealed record AccountPurgeResult(
    bool DatabaseRecordsDeleted,
    bool MediaFilesDeleted,
    bool SessionCleared,
    IReadOnlyList<string> Errors)
{
    /// <summary>True when every record, file and session was cleared.</summary>
    public bool Succeeded => DatabaseRecordsDeleted && MediaFilesDeleted && SessionCleared && Errors.Count == 0;
}

/// <summary>Deletes an account's local data, which is what deletion must do before it is reported as done.</summary>
public interface IAppAccountPurgeService
{
    Task<AccountPurgeResult> PurgeAccountDataAsync(int userId, CancellationToken ct = default);
    Task PurgeAllAccountDataAndKeysAsync();
}
