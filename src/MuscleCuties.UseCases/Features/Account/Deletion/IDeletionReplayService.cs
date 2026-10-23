namespace MuscleCuties.UseCases.Features.Account.Deletion;

public sealed record DeletionReplayResult(
    int AccountsEvaluated,
    int AccountsRePurged,
    int TotalResurrectedRecordsPurged,
    IReadOnlyList<Guid> RePurgedUserIds);

public interface IDeletionReplayService
{
    Task<DeletionReplayResult> ReplayPendingDeletionsAsync(CancellationToken cancellationToken = default);
}
