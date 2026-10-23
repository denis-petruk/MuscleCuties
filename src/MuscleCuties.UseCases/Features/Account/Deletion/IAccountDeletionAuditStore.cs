namespace MuscleCuties.UseCases.Features.Account.Deletion;

public sealed record AccountDeletionAuditEntry(
    Guid TombstoneId,
    Guid UserId,
    DateTime PurgedAtUtc,
    string Reason,
    int PurgedRecordCount,
    IReadOnlyList<string> CleanedMediaKeys);

public interface IAccountDeletionAuditStore
{
    Task RecordDeletionAuditAsync(AccountDeletionAuditEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountDeletionAuditEntry>> GetDeletionAuditsAsync(CancellationToken cancellationToken = default);
}
