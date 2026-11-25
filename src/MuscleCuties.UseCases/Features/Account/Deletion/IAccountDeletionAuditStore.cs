namespace MuscleCuties.UseCases.Features.Account.Deletion;

/// <summary>One audit line about a deletion, deliberately free of personal content.</summary>
public sealed record AccountDeletionAuditEntry(
    Guid TombstoneId,
    Guid UserId,
    DateTime PurgedAtUtc,
    string Reason,
    int PurgedRecordCount,
    IReadOnlyList<string> CleanedMediaKeys);

/// <summary>Where deletion outcomes are recorded without the account's data.</summary>
public interface IAccountDeletionAuditStore
{
    Task RecordDeletionAuditAsync(AccountDeletionAuditEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountDeletionAuditEntry>> GetDeletionAuditsAsync(CancellationToken cancellationToken = default);
}
