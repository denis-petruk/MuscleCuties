using MuscleCuties.UseCases.Features.Account.Deletion.Contracts;

namespace MuscleCuties.UseCases.Features.Account.Deletion;

/// <summary>Deletes every row an account owns and proves none remain.</summary>
public interface IServerAccountPurgeStore
{
    Task<AccountDeletionResult> PurgeAccountDataAsync(Guid userId, CancellationToken cancellationToken = default);
}
