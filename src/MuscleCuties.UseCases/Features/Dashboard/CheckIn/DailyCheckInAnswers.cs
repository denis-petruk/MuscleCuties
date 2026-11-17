using MuscleCuties.UseCases.Features.Dashboard.CheckIn.Contracts;

namespace MuscleCuties.UseCases.Features.Dashboard.CheckIn;

/// <summary>
/// The answers a person actually gave in today's check-in. Every self-report starts unanswered;
/// nothing is preselected, so an untouched control can never be submitted as "no pain",
/// "no bloating", a sleep duration or an unreported rating. Soreness and body weight stay optional.
/// </summary>
public sealed record DailyCheckInAnswers(
    double? SleepHours = null,
    int? SleepQuality = null,
    int? Energy = null,
    int? Soreness = null,
    int? PainLevel = null,
    bool? HasBloating = null,
    double? BodyWeightKg = null,
    int? Fatigue = null)
{
    public const int MinimumRating = 1;
    public const int MaximumRating = 5;
    public const int MinimumPainLevel = 0;
    public const int MaximumPainLevel = 3;
    public const double MinimumSleepHours = 0;
    public const double MaximumSleepHours = 14;

    /// <summary>Full observation completeness retained for callers requesting supplementary answers.</summary>
    public bool IsComplete =>
        SleepHours is >= MinimumSleepHours and <= MaximumSleepHours &&
        IsRating(SleepQuality) && IsRating(Energy) && IsRating(Fatigue) &&
        (Soreness is null || IsRating(Soreness)) &&
        PainLevel is >= MinimumPainLevel and <= MaximumPainLevel &&
        HasBloating.HasValue;

    /// <summary>The three required readiness ratings, with every supplied optional value validated.</summary>
    public bool IsReadyForPlanning => IsRating(SleepQuality) && IsRating(Energy) && IsRating(Fatigue) &&
        (SleepHours is null or >= MinimumSleepHours and <= MaximumSleepHours) &&
        (Soreness is null || IsRating(Soreness)) &&
        (PainLevel is null or >= MinimumPainLevel and <= MaximumPainLevel);

    /// <summary>
    /// Builds the request, or returns null while any required answer is missing.
    /// </summary>
    /// <remarks>
    /// Fatigue is its own report. An unanswered stress field remains absent; it is never copied from energy.
    /// </remarks>
    public SubmitDailyCheckInRequest? ToRequest(DateOnly date, string timeZoneId, bool requireSupplementaryAnswers = true)
    {
        if (requireSupplementaryAnswers ? !IsComplete : !IsReadyForPlanning)
            return null;

        return new SubmitDailyCheckInRequest(
            date, SleepHours, Energy!.Value, PainLevel, HasBloating, BodyWeightKg,
            SleepQuality, Soreness, null, timeZoneId, Fatigue);
    }

    private static bool IsRating(int? value) => value is >= MinimumRating and <= MaximumRating;
}
