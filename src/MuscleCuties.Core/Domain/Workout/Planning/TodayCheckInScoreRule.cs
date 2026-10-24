namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum CheckInScoreStatus
{
    Available,
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
/// MC-PLANNING-0.2 product arithmetic. The score describes a planning estimate, not a
/// recovery measurement or medical assessment. This rule never changes a workout or evaluates symptoms.
/// </summary>
public static class TodayCheckInScoreRule
{
    public const string RulesetVersion = StrengthPlanPreviewRule.RulesetVersion;
    public static bool ScoreDisplayEnabled => false;
    public const int ReadinessScaleMinimum = 1;
    public const int ReadinessScaleMaximum = 5;
    public const int ReadinessScoreMaximum = 100;
    public const int FeelingReadyThreshold = 75;
    public const int ConsiderLighterSessionThreshold = 50;
    private const decimal ScoreDenominator = 16m;

    public static CheckInScoreResult Evaluate(
        TodayCheckInObservation? observation,
        DateTimeOffset nowUtc,
        string activeTimeZoneId)
    {
        if (observation is null)
            return Unavailable(CheckInScoreStatus.Incomplete, null);
        if (observation.SleepQuality is null || observation.Energy is null ||
            observation.Soreness is null || observation.Stress is null)
            return Unavailable(CheckInScoreStatus.Incomplete, observation);
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

        if (!IsValidScaleValue(observation.SleepQuality.Value) || !IsValidScaleValue(observation.Energy.Value) ||
            !IsValidScaleValue(observation.Soreness.Value) || !IsValidScaleValue(observation.Stress.Value))
            return Unavailable(CheckInScoreStatus.InvalidInput, observation);

        var rawScore = CalculatePrototype(observation.SleepQuality.Value, observation.Energy.Value,
            observation.Soreness.Value, observation.Stress.Value);
        var band = rawScore >= TodayCheckInScoreRule.FeelingReadyThreshold
            ? CheckInScoreBand.FeelingReady
            : rawScore >= TodayCheckInScoreRule.ConsiderLighterSessionThreshold
                ? CheckInScoreBand.ConsiderLighterSession
                : CheckInScoreBand.ConsiderEasierDay;
        return new CheckInScoreResult(CheckInScoreStatus.Available, rawScore, band, observation);
    }

    internal static decimal CalculatePrototype(decimal sleepQuality, decimal energy, decimal soreness, decimal stress) =>
        TodayCheckInScoreRule.ReadinessScoreMaximum * ((sleepQuality - TodayCheckInScoreRule.ReadinessScaleMinimum) +
            (energy - TodayCheckInScoreRule.ReadinessScaleMinimum) +
            (TodayCheckInScoreRule.ReadinessScaleMaximum - soreness) +
            (TodayCheckInScoreRule.ReadinessScaleMaximum - stress)) / ScoreDenominator;

    private static bool IsValidScaleValue(decimal value) =>
        value >= TodayCheckInScoreRule.ReadinessScaleMinimum &&
        value <= TodayCheckInScoreRule.ReadinessScaleMaximum && value == decimal.Truncate(value);

    private static CheckInScoreResult Unavailable(CheckInScoreStatus status, TodayCheckInObservation? observation) =>
        new(status, null, null, observation);
}
