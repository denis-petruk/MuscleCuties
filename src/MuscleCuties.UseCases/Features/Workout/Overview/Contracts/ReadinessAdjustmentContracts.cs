namespace MuscleCuties.UseCases.Features.Workout.Overview.Contracts;

public sealed record ReviewReadinessAdjustmentRequest(DateOnly LocalDate, string TimeZoneId);

public sealed record ReadinessAdjustmentChange(string Kind, string Description, int? Before, int? After);

public sealed record ReadinessAdjustmentProposalDto(
    Guid ProposalId,
    string Band,
    string Summary,
    IReadOnlyList<ReadinessAdjustmentChange> Changes,
    bool CanApply);

public sealed record DecideReadinessAdjustmentRequest(Guid ProposalId, DateOnly LocalDate, string TimeZoneId);

public sealed record ReadinessAdjustmentDecisionDto(bool Applied, bool Stale, string Message);
