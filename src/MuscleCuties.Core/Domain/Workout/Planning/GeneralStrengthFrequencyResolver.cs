using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public sealed record GeneralStrengthFrequencyPolicy(
    int PolicySessionsPerWeek,
    int TargetSessionsPerWeek,
    int MaxSessionsPerWeek);

/// <summary>
/// Centralized resolver for General Strength frequency across direct prescriptions,
/// HVS-to-GS fallback, strength plan preview, and UI display data.
/// </summary>
public static class GeneralStrengthFrequencyResolver
{
    public const int MinimumStrengthSessionsPerWeek = 2;
    public const int BaselinePolicySessionsPerWeek = MinimumStrengthSessionsPerWeek;
    public const int BaselineMaxSessionsPerWeek = 2;
    public const int BaselineTargetMaxLimit = 2;

    public const int EstablishedPolicySessionsPerWeek = MinimumStrengthSessionsPerWeek;
    public const int EstablishedMaxSessionsPerWeek = 3;
    public const int EstablishedTargetMaxLimit = 3;

    public const int MinimumConsistentWeeksForEstablished = 12;
    public const decimal MinimumAverageSessionsPerWeekForEstablished = 2.0m;
    public const int MaximumDaysSinceLastSessionForCurrentEstablished = 28; // < 28 days

    public static bool IsEstablishedResistanceHistory(
        int? weeksConsistent,
        decimal? avgSessionsLast4Weeks,
        int? daysSinceLastSession)
    {
        if (weeksConsistent is null || avgSessionsLast4Weeks is null || daysSinceLastSession is null)
            return false;

        return weeksConsistent.Value >= MinimumConsistentWeeksForEstablished &&
               avgSessionsLast4Weeks.Value >= MinimumAverageSessionsPerWeekForEstablished &&
               daysSinceLastSession.Value < MaximumDaysSinceLastSessionForCurrentEstablished;
    }

    public static bool IsEstablishedResistanceHistory(
        ActivityTrainingHistory? history,
        DateOnly localDate)
    {
        if (history is null ||
            history.ConsistentParticipationWeeks is null ||
            history.AverageSessionsPerWeekInRecentFourWeeks is null ||
            history.LastCompletedSessionDate is null)
            return false;

        var daysSinceLast = localDate.DayNumber - history.LastCompletedSessionDate.Value.DayNumber;
        if (daysSinceLast < 0)
            return false;

        return IsEstablishedResistanceHistory(
            history.ConsistentParticipationWeeks.Value,
            history.AverageSessionsPerWeekInRecentFourWeeks.Value,
            daysSinceLast);
    }

    public static int ResolveAvailableWeekdaysCount(
        IReadOnlyCollection<DayOfWeek>? availableWeekdays,
        int? generalDaysPerWeek = null)
    {
        if (availableWeekdays is null)
            return 0;

        var distinctCount = availableWeekdays.Distinct().Count();
        if (generalDaysPerWeek.HasValue)
        {
            var clampedGeneral = Math.Max(0, generalDaysPerWeek.Value);
            return Math.Min(distinctCount, clampedGeneral);
        }

        return distinctCount;
    }

    public static GeneralStrengthFrequencyPolicy Resolve(
        int availableWeekdaysCount,
        bool isEstablishedResistanceHistory,
        TrainingExperienceLevel? profileLevel)
    {
        var validAvailableCount = Math.Max(0, availableWeekdaysCount);

        var isEstablishedTier = isEstablishedResistanceHistory &&
            profileLevel is TrainingExperienceLevel.Intermediate or TrainingExperienceLevel.Advanced;

        if (isEstablishedTier)
        {
            return new GeneralStrengthFrequencyPolicy(
                PolicySessionsPerWeek: EstablishedPolicySessionsPerWeek,
                TargetSessionsPerWeek: Math.Max(MinimumStrengthSessionsPerWeek,
                    Math.Min(EstablishedTargetMaxLimit, validAvailableCount)),
                MaxSessionsPerWeek: EstablishedMaxSessionsPerWeek);
        }

        return new GeneralStrengthFrequencyPolicy(
            PolicySessionsPerWeek: BaselinePolicySessionsPerWeek,
            TargetSessionsPerWeek: MinimumStrengthSessionsPerWeek,
            MaxSessionsPerWeek: BaselineMaxSessionsPerWeek);
    }
}
