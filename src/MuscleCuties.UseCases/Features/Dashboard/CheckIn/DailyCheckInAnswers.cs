using MuscleCuties.UseCases.Features.Dashboard.CheckIn.Contracts;

namespace MuscleCuties.UseCases.Features.Dashboard.CheckIn;

/// <summary>
/// The answers a person actually gave in today's check-in. Every self-report starts unanswered;
/// nothing is preselected, so an untouched control can never be submitted as "no pain",
/// "no bloating", a sleep duration or a soreness rating. Body weight stays optional.
/// </summary>
public sealed record DailyCheckInAnswers(
    double? SleepHours = null,
    int? SleepQuality = null,
    int? Energy = null,
    int? Soreness = null,
    int? PainLevel = null,
    bool? HasBloating = null,
    double? BodyWeightKg = null)
{
    public const int MinimumRating = 1;
    public const int MaximumRating = 5;
    public const int MinimumPainLevel = 0;
    public const int MaximumPainLevel = 3;
    public const double MinimumSleepHours = 0;
    public const double MaximumSleepHours = 14;

    public bool IsComplete =>
        SleepHours is >= MinimumSleepHours and <= MaximumSleepHours &&
        IsRating(SleepQuality) && IsRating(Energy) && IsRating(Soreness) &&
        PainLevel is >= MinimumPainLevel and <= MaximumPainLevel &&
        HasBloating.HasValue;

    /// <summary>
    /// Builds the request, or returns null while any required answer is missing.
    /// </summary>
    /// <remarks>
    /// The check-in contract (client store and server validation) still requires a planning stress value,
    /// but no screen asks about stress. The energy answer is sent in that field only for wire compatibility;
    /// the score rule (MC-CHECKIN-SCORE-0.3) lets a stress value lower readiness but never offset low energy,
    /// so this copy cannot hide fatigue. Send null here once the contract accepts an unreported stress.
    /// </remarks>
    public SubmitDailyCheckInRequest? ToRequest(DateOnly date, string timeZoneId)
    {
        if (!IsComplete)
            return null;

        return new SubmitDailyCheckInRequest(
            date, SleepHours!.Value, Energy!.Value, PainLevel!.Value, HasBloating!.Value, BodyWeightKg,
            SleepQuality, Soreness, PlanningStressCompatibilityValue, timeZoneId);
    }

    /// <summary>Contract-required stress field; see <see cref="ToRequest"/>. Not a self-report.</summary>
    public int? PlanningStressCompatibilityValue => Energy;

    private static bool IsRating(int? value) => value is >= MinimumRating and <= MaximumRating;
}
