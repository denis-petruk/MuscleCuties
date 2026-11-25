namespace MuscleCuties.UseCases.Features.Account.Deletion;

/// <summary>What a replayed deletion still had to do.</summary>
public sealed record DeletionReplayResult(
    int AccountsEvaluated,
    int AccountsRePurged,
    int TotalResurrectedRecordsPurged,
    IReadOnlyList<Guid> RePurgedUserIds);

/// <summary>Completes a deletion that was recorded but may not have finished.</summary>
public interface IDeletionReplayService
{
    Task<DeletionReplayResult> ReplayPendingDeletionsAsync(CancellationToken cancellationToken = default);
}
