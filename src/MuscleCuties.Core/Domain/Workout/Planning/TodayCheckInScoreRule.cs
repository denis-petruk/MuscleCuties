namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum CheckInScoreStatus
{
    Available,
    FeatureDisabled,
    NeedsProfessionalReview,
    InvalidTimeZone,
    WrongTimeZoneAssociation,
    Stale,
    InvalidInput,
    Incomplete
}

public enum CheckInScoreBand
{
    FeelingReady,
    ConsiderLighterSession,
    ConsiderEasierDay
}

public enum PlanningReviewSection
{
    ReadinessScoreDisplay,
    ReadinessSuggestions,
    SymptomStopLogic,
    TrainingPrescriptions,
    AutomatedProgression,
    InjurySpecificSubstitutions
}

/// <summary>Trusted release configuration; a feature flag alone does not establish review coverage.</summary>
public sealed record PlanningReviewApproval(
    string RulesetVersion,
    string ApprovalReference,
    DateTimeOffset ApprovedAtUtc,
    IReadOnlySet<PlanningReviewSection> CoveredSections,
    string? ReviewedTrainingPolicyVersion = null);

public sealed record TodayCheckInObservation(
    DateOnly LocalDate,
    string TimeZoneId,
    DateTimeOffset RecordedAtUtc,
    long Revision,
    decimal? SleepQuality,
    decimal? Energy,
    decimal? Soreness,
    decimal? Stress);

public sealed record CheckInScoreResult(
    CheckInScoreStatus Status,
    decimal? RawScore,
    CheckInScoreBand? Band,
    TodayCheckInObservation? Observation);

/// <summary>
/// MC-PLANNING-0.2 prototype arithmetic. The score describes a product heuristic, not a
/// recovery percentage. This rule never changes a workout or evaluates symptoms.
/// </summary>
public static class TodayCheckInScoreRule
{
    public const string RulesetVersion = "MC-PLANNING-0.2";
    private const decimal ScaleMinimum = 1m;
    private const decimal ScaleMaximum = 5m;
    private const decimal MaximumScore = 100m;
    private const decimal ScoreDenominator = 16m;
    private const decimal FeelingReadyThreshold = 75m;
    private const decimal ConsiderLighterThreshold = 50m;

    public static CheckInScoreResult Evaluate(
        TodayCheckInObservation? observation,
        DateTimeOffset nowUtc,
        string activeTimeZoneId,
        bool scoreDisplayEnabled,
        PlanningReviewApproval? approval)
    {
        if (!scoreDisplayEnabled)
            return Unavailable(CheckInScoreStatus.FeatureDisabled, observation);
        if (!HasApproval(approval))
            return Unavailable(CheckInScoreStatus.NeedsProfessionalReview, observation);
        if (observation is null)
            return Unavailable(CheckInScoreStatus.Incomplete, null);
        if (string.IsNullOrWhiteSpace(activeTimeZoneId) || string.IsNullOrWhiteSpace(observation.TimeZoneId))
            return Unavailable(CheckInScoreStatus.InvalidTimeZone, observation);

        TimeZoneInfo activeZone;
        try
        {
            activeZone = TimeZoneInfo.FindSystemTimeZoneById(activeTimeZoneId);
            _ = TimeZoneInfo.FindSystemTimeZoneById(observation.TimeZoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return Unavailable(CheckInScoreStatus.InvalidTimeZone, observation);
        }

        if (!string.Equals(observation.TimeZoneId, activeTimeZoneId, StringComparison.Ordinal))
            return Unavailable(CheckInScoreStatus.WrongTimeZoneAssociation, observation);
        if (nowUtc.Offset != TimeSpan.Zero || observation.RecordedAtUtc.Offset != TimeSpan.Zero ||
            observation.RecordedAtUtc > nowUtc || observation.Revision <= 0)
            return Unavailable(CheckInScoreStatus.InvalidInput, observation);

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, activeZone).DateTime);
        var recordedLocalDate = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(observation.RecordedAtUtc, activeZone).DateTime);
        if (observation.LocalDate != today || recordedLocalDate != observation.LocalDate)
            return Unavailable(CheckInScoreStatus.Stale, observation);

        if (observation.SleepQuality is null || observation.Energy is null ||
            observation.Soreness is null || observation.Stress is null)
            return Unavailable(CheckInScoreStatus.Incomplete, observation);
        if (!IsValidScaleValue(observation.SleepQuality.Value) || !IsValidScaleValue(observation.Energy.Value) ||
            !IsValidScaleValue(observation.Soreness.Value) || !IsValidScaleValue(observation.Stress.Value))
            return Unavailable(CheckInScoreStatus.InvalidInput, observation);

        var rawScore = CalculatePrototype(observation.SleepQuality.Value, observation.Energy.Value,
            observation.Soreness.Value, observation.Stress.Value);
        var band = rawScore >= FeelingReadyThreshold
            ? CheckInScoreBand.FeelingReady
            : rawScore >= ConsiderLighterThreshold
                ? CheckInScoreBand.ConsiderLighterSession
                : CheckInScoreBand.ConsiderEasierDay;
        return new CheckInScoreResult(CheckInScoreStatus.Available, rawScore, band, observation);
    }

    internal static decimal CalculatePrototype(decimal sleepQuality, decimal energy, decimal soreness, decimal stress) =>
        MaximumScore * ((sleepQuality - ScaleMinimum) + (energy - ScaleMinimum) +
                        (ScaleMaximum - soreness) + (ScaleMaximum - stress)) / ScoreDenominator;

    private static bool HasApproval(PlanningReviewApproval? approval) =>
        approval is not null &&
        approval.RulesetVersion == RulesetVersion &&
        !string.IsNullOrWhiteSpace(approval.ApprovalReference) &&
        approval.ApprovedAtUtc.Offset == TimeSpan.Zero &&
        approval.CoveredSections?.Contains(PlanningReviewSection.ReadinessScoreDisplay) == true;

    private static bool IsValidScaleValue(decimal value) =>
        value >= ScaleMinimum && value <= ScaleMaximum && value == decimal.Truncate(value);

    private static CheckInScoreResult Unavailable(CheckInScoreStatus status, TodayCheckInObservation? observation) =>
        new(status, null, null, observation);
}
