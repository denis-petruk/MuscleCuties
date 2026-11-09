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
/// Check-in score arithmetic (<see cref="RulesetVersion"/>). The score describes a planning estimate,
/// not a recovery measurement or medical assessment. This rule never changes a workout or evaluates symptoms.
/// <para>
/// Energy carries two scale shares. Stress is optional: when it is reported it can only lower the second
/// energy share (high stress), never compensate for low energy; when it is not reported the energy share is
/// used unchanged. Low energy therefore always lowers the score (MC-BETA-WORKOUT-1 decision 4), and for any
/// reported stress the score is never higher than the 0.2 arithmetic it replaces.
/// </para>
/// </summary>
public static class TodayCheckInScoreRule
{
    /// <summary>
    /// Version of this score arithmetic. It is independent of the plan-preview ruleset and is only
    /// compared in memory against the release approval; it is not persisted.
    /// </summary>
    public const string RulesetVersion = "MC-CHECKIN-SCORE-0.3";
    public static bool ScoreDisplayEnabled => true;
    public const int ReadinessScaleMinimum = 1;
    public const int ReadinessScaleMaximum = 5;
    public const int ReadinessScoreMaximum = 100;
    public const int FeelingReadyThreshold = 75;
    public const int ConsiderLighterSessionThreshold = 50;
    private const decimal ScoreDenominator = 16m;

    /// <summary>Scale shares carried by energy: the energy term itself plus the share stress may lower.</summary>
    public const int EnergyScaleShares = 2;

    public static CheckInScoreResult Evaluate(
        TodayCheckInObservation? observation,
        DateTimeOffset nowUtc,
        string activeTimeZoneId)
    {
        if (observation is null)
            return Unavailable(CheckInScoreStatus.Incomplete, null);
        if (observation.SleepQuality is null || observation.Energy is null ||
            observation.Soreness is null)
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
            !IsValidScaleValue(observation.Soreness.Value) ||
            observation.Stress is { } stress && !IsValidScaleValue(stress))
            return Unavailable(CheckInScoreStatus.InvalidInput, observation);

        var rawScore = CalculatePrototype(observation.SleepQuality.Value, observation.Energy.Value,
            observation.Soreness.Value, observation.Stress);
        var band = rawScore >= TodayCheckInScoreRule.FeelingReadyThreshold
            ? CheckInScoreBand.FeelingReady
            : rawScore >= TodayCheckInScoreRule.ConsiderLighterSessionThreshold
                ? CheckInScoreBand.ConsiderLighterSession
                : CheckInScoreBand.ConsiderEasierDay;
        return new CheckInScoreResult(CheckInScoreStatus.Available, rawScore, band, observation);
    }

    internal static decimal CalculatePrototype(decimal sleepQuality, decimal energy, decimal soreness, decimal? stress)
    {
        var energyShare = energy - ReadinessScaleMinimum;
        // Reported stress may only lower the second energy share; it never offsets low energy.
        var stressLimitedEnergyShare = stress is { } reportedStress
            ? Math.Min(energyShare, ReadinessScaleMaximum - reportedStress)
            : energyShare;
        return ReadinessScoreMaximum * ((sleepQuality - ReadinessScaleMinimum) +
            energyShare * (EnergyScaleShares - 1) + stressLimitedEnergyShare +
            (ReadinessScaleMaximum - soreness)) / ScoreDenominator;
    }

    private static bool IsValidScaleValue(decimal value) =>
        value >= TodayCheckInScoreRule.ReadinessScaleMinimum &&
        value <= TodayCheckInScoreRule.ReadinessScaleMaximum && value == decimal.Truncate(value);

    private static CheckInScoreResult Unavailable(CheckInScoreStatus status, TodayCheckInObservation? observation) =>
        new(status, null, null, observation);
}
