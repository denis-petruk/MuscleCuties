using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum PhaseReviewProvenance
{
    Estimated,
    UserConfirmed,
    Unknown,
    TrackingDisabled
}

public enum PhaseBaselineScale
{
    Unknown,
    QuizMappedOneToFive
}

public enum PhaseReviewGateStatus
{
    InvalidInput,
    NoActivePlan,
    TrackingDisabled,
    UnknownPhase,
    NeedsPhaseConfirmation,
    MissingBaseline,
    AlreadyReviewed,
    Dismissed,
    CurrentSafetyReviewRequired,
    NoEligibleFutureSessions,
    ReadyForAdjustmentPreview
}

public sealed record PhaseReviewContext(
    Guid ContextId,
    CyclePhase Phase,
    PhaseReviewProvenance Provenance,
    DateOnly? NextEstimatedTransitionDate);

public sealed record PhaseReviewSession(
    Guid SessionId,
    DateOnly LocalDate,
    DateTimeOffset ScheduledStartUtc,
    PlannedSessionState State,
    bool HasStarted,
    bool HasRecordedWork);

public sealed record PhaseReviewDismissalKey(
    Guid ContextId,
    CyclePhase Phase,
    int Discomfort,
    int Energy,
    string PolicyVersion);

public sealed record PhaseReviewGateInput(
    Guid PlanId,
    Guid SourceRevisionId,
    DateOnly PlanEndDate,
    DateTimeOffset ReviewAtUtc,
    DateOnly LocalToday,
    PhaseReviewContext Context,
    PhaseBaseline? Baseline,
    PhaseBaselineScale BaselineScale,
    CyclePhase? PreviousReviewedPhase,
    Guid? PreviousReviewedContextId,
    PhaseBaseline? PreviousReviewedBaseline,
    bool ExplicitReviewRequested,
    bool PhaseReviewPromptsEnabled,
    IReadOnlyList<PhaseReviewSession> Sessions,
    IReadOnlySet<PhaseReviewDismissalKey> Dismissals,
    bool HasCurrentSymptomsOrRestrictions = false);

public sealed record PhaseReviewGateResult(
    PhaseReviewGateStatus Status,
    DateOnly HorizonStart,
    DateOnly HorizonEnd,
    IReadOnlyList<Guid> EligibleSessionIds,
    PhaseReviewDismissalKey? DismissalKey);

/// <summary>
/// Screens a confirmed phase change before any adjustment rules run. This rule never
/// changes a plan or infers a prescription from a phase or a baseline rating.
/// </summary>
public static class PhaseReviewRule
{
    public const string PolicyId = "MC-PHASE-REVIEW";
    public const string Version = "0.1.0";
    public const int ReviewCalendarDates = 7;
    public const int FollowingCalendarDates = ReviewCalendarDates - 1;
    public const int TransitionBoundaryExcludedDates = 1;
    public const int MinimumDiscomfortRating = 1;
    public const int MaximumDiscomfortRating = 5;
    public const int MinimumEnergyRating = 1;
    public const int MaximumEnergyRating = 5;
    public const int NoEligibleFutureSessions = 0;

    public static PhaseReviewGateResult Evaluate(PhaseReviewGateInput? input)
    {
        if (input is null || input.Context is null || input.Sessions is null ||
            input.Dismissals is null || input.ReviewAtUtc.Offset != TimeSpan.Zero ||
            input.PlanEndDate < input.LocalToday ||
            !Enum.IsDefined(input.Context.Phase) ||
            !Enum.IsDefined(input.Context.Provenance) ||
            !Enum.IsDefined(input.BaselineScale) ||
            input.PreviousReviewedPhase is { } priorPhase && !Enum.IsDefined(priorPhase) ||
            input.Sessions.Any(session => session is null || session.SessionId == Guid.Empty ||
                session.ScheduledStartUtc.Offset != TimeSpan.Zero ||
                !Enum.IsDefined(session.State)) ||
            input.Sessions.Select(session => session.SessionId).Distinct().Count() != input.Sessions.Count)
            return Result(PhaseReviewGateStatus.InvalidInput, input?.LocalToday ?? default);

        var remainingCalendarDates = DateOnly.MaxValue.DayNumber - input.LocalToday.DayNumber;
        var horizonEnd = input.LocalToday.AddDays(Math.Min(
            PhaseReviewRule.FollowingCalendarDates, remainingCalendarDates));
        if (horizonEnd > input.PlanEndDate)
            horizonEnd = input.PlanEndDate;
        if (input.Context.NextEstimatedTransitionDate is { } transitionDate &&
            transitionDate <= horizonEnd)
            horizonEnd = transitionDate == DateOnly.MinValue ? DateOnly.MinValue :
                transitionDate.AddDays(-PhaseReviewRule.TransitionBoundaryExcludedDates);

        PhaseReviewGateResult Gate(PhaseReviewGateStatus status) =>
            new(status, input.LocalToday, horizonEnd, Array.Empty<Guid>(), null);

        if (input.PlanId == Guid.Empty || input.SourceRevisionId == Guid.Empty)
            return Gate(PhaseReviewGateStatus.NoActivePlan);
        if (!input.PhaseReviewPromptsEnabled ||
            input.Context.Provenance == PhaseReviewProvenance.TrackingDisabled)
            return Gate(PhaseReviewGateStatus.TrackingDisabled);
        if (input.HasCurrentSymptomsOrRestrictions)
            return Gate(PhaseReviewGateStatus.CurrentSafetyReviewRequired);
        if (input.Context.Provenance == PhaseReviewProvenance.Unknown ||
            input.Context.Phase == CyclePhase.Unknown)
            return Gate(PhaseReviewGateStatus.UnknownPhase);
        if (input.Context.Provenance == PhaseReviewProvenance.Estimated)
            return Gate(PhaseReviewGateStatus.NeedsPhaseConfirmation);
        if (input.Context.ContextId == Guid.Empty)
            return Gate(PhaseReviewGateStatus.InvalidInput);
        if (input.BaselineScale != PhaseBaselineScale.QuizMappedOneToFive ||
            input.Baseline is null ||
            input.Baseline.Pain is < PhaseReviewRule.MinimumDiscomfortRating or
                > PhaseReviewRule.MaximumDiscomfortRating ||
            input.Baseline.Energy is < PhaseReviewRule.MinimumEnergyRating or
                > PhaseReviewRule.MaximumEnergyRating)
            return Gate(PhaseReviewGateStatus.MissingBaseline);
        if (!input.ExplicitReviewRequested &&
            input.PreviousReviewedPhase == input.Context.Phase &&
            input.PreviousReviewedContextId == input.Context.ContextId &&
            input.PreviousReviewedBaseline == input.Baseline)
            return Gate(PhaseReviewGateStatus.AlreadyReviewed);

        var dismissalKey = new PhaseReviewDismissalKey(input.Context.ContextId,
            input.Context.Phase, input.Baseline.Pain,
            input.Baseline.Energy, PhaseReviewRule.Version);
        if (!input.ExplicitReviewRequested && input.Dismissals.Contains(dismissalKey))
            return Gate(PhaseReviewGateStatus.Dismissed);

        var eligible = input.Sessions.Where(session =>
                session.State == PlannedSessionState.Planned &&
                !session.HasStarted && !session.HasRecordedWork &&
                session.ScheduledStartUtc > input.ReviewAtUtc &&
                session.LocalDate >= input.LocalToday && session.LocalDate <= horizonEnd)
            .Select(session => session.SessionId)
            .ToArray();
        return new PhaseReviewGateResult(
            eligible.Length == PhaseReviewRule.NoEligibleFutureSessions
                ? PhaseReviewGateStatus.NoEligibleFutureSessions :
                PhaseReviewGateStatus.ReadyForAdjustmentPreview,
            input.LocalToday, horizonEnd, Array.AsReadOnly(eligible), dismissalKey);
    }

    private static PhaseReviewGateResult Result(PhaseReviewGateStatus status, DateOnly today) =>
        new(status, today, today, Array.Empty<Guid>(), null);
}
