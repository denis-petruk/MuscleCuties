namespace MuscleCuties.UseCases.Features.Workout.Planning;

/// <summary>An offer to switch to a strength-focused style, with what it would change.</summary>
public sealed record StrengthStyleOfferDto(Guid Id, Guid PlanId, Guid RevisionId, int CurrentStyleId,
    IReadOnlyList<int> AlternativeStyleIds);

/// <summary>The offer persistence.</summary>
public interface IStrengthStyleOfferStore
{
    Task<StrengthStyleOfferDto?> GetCurrentAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> DismissAsync(Guid userId, Guid offerId, CancellationToken cancellationToken);
}

/// <summary>The offer to show, if one applies.</summary>
public sealed record StrengthStyleOfferResponse(StrengthStyleOfferDto? Offer);
/// <summary>Whether the offer was dismissed for good.</summary>
public sealed record StrengthStyleOfferDismissResult(bool Dismissed);
/// <summary>The client-side offer operations.</summary>
public interface IStrengthStyleOfferClient
{
    Task<StrengthStyleOfferResponse> GetCurrentAsync(CancellationToken cancellationToken = default);
    Task<StrengthStyleOfferDismissResult> DismissAsync(Guid offerId, CancellationToken cancellationToken = default);
}
