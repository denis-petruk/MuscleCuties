using MuscleCuties.UseCases.Features.Account.Deletion.Contracts;

namespace MuscleCuties.UseCases.Features.Account.Deletion;

public interface IServerAccountPurgeStore
{
    Task<AccountDeletionResult> PurgeAccountDataAsync(Guid userId, CancellationToken cancellationToken = default);
}
