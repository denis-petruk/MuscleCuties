namespace MuscleCuties.UseCases.Features.Workout.Planning;

public sealed record StrengthStyleOfferDto(Guid Id, Guid PlanId, Guid RevisionId, int CurrentStyleId,
    IReadOnlyList<int> AlternativeStyleIds);

public interface IStrengthStyleOfferStore
{
    Task<StrengthStyleOfferDto?> GetCurrentAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> DismissAsync(Guid userId, Guid offerId, CancellationToken cancellationToken);
}

public sealed record StrengthStyleOfferResponse(StrengthStyleOfferDto? Offer);
public sealed record StrengthStyleOfferDismissResult(bool Dismissed);
public interface IStrengthStyleOfferClient
{
    Task<StrengthStyleOfferResponse> GetCurrentAsync(CancellationToken cancellationToken = default);
    Task<StrengthStyleOfferDismissResult> DismissAsync(Guid offerId, CancellationToken cancellationToken = default);
}
