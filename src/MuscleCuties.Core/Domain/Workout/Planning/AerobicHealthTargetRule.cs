namespace MuscleCuties.Core.Domain.Workout.Planning;

public sealed record AerobicSessionMinutes(
    decimal MinutesModerate,
    decimal MinutesVigorous);

public sealed record AerobicHealthEvaluationResult(
    decimal TotalModerateEquivalentMinutes,
    int ResistanceTrainingDaysPerWeek,
    bool MeetsAerobicTarget,
    bool MeetsResistanceTarget,
    bool MeetsAllTargets,
    string AerobicGuidelineStatus,
    string ResistanceGuidelineStatus,
    IReadOnlyList<string> ReasonCodes);

public static class AerobicHealthTargetRule
{
    public const decimal VigorousIntensityMultiplier = 2m;
    public const decimal MinimumModerateEquivalentMinutesPerWeek = 150m;
    public const decimal OptimalModerateEquivalentMinutesPerWeek = 300m;
    public const int MinimumResistanceTrainingDaysPerWeek = 2;
    public const decimal NoMinutes = 0m;
    public const int NoDays = 0;

    public const string StatusBelowGuideline = "BelowGuideline";
    public const string StatusMeetsGuideline = "MeetsGuideline";
    public const string StatusExceedsGuideline = "ExceedsGuideline";

    public const string ResistanceTargetMet = "ResistanceTargetMet";
    public const string ResistanceTargetNotMet = "ResistanceTargetNotMet";

    public const string ReasonAerobicBelow150 = "AerobicMinutesBelow150Threshold";
    public const string ReasonAerobicMeets150To300 = "AerobicMinutesMeets150To300Guideline";
    public const string ReasonAerobicExceeds300 = "AerobicMinutesExceeds300Optimal";
    public const string ReasonResistanceTargetMet = "ResistanceTrainingAtLeastTwoDaysMet";
    public const string ReasonResistanceTargetBelowTwoDays = "ResistanceTrainingBelowTwoDaysTarget";

    public static decimal CalculateSessionModerateEquivalentMinutes(decimal minutesModerate, decimal minutesVigorous)
    {
        var safeModerate = Math.Max(NoMinutes, minutesModerate);
        var safeVigorous = Math.Max(NoMinutes, minutesVigorous);
        return safeModerate + (safeVigorous * VigorousIntensityMultiplier);
    }

    public static decimal CalculateWeeklyModerateEquivalentMinutes(IEnumerable<AerobicSessionMinutes>? sessions)
    {
        if (sessions is null)
            return NoMinutes;

        return sessions.Sum(s => CalculateSessionModerateEquivalentMinutes(s.MinutesModerate, s.MinutesVigorous));
    }

    public static AerobicHealthEvaluationResult Evaluate(
        IEnumerable<AerobicSessionMinutes>? aerobicSessions,
        int resistanceTrainingDaysPerWeek)
    {
        var meWeek = CalculateWeeklyModerateEquivalentMinutes(aerobicSessions);
        var safeResistanceDays = Math.Max(NoDays, resistanceTrainingDaysPerWeek);

        var meetsAerobic = meWeek >= MinimumModerateEquivalentMinutesPerWeek;
        var meetsResistance = safeResistanceDays >= MinimumResistanceTrainingDaysPerWeek;
        var meetsAll = meetsAerobic && meetsResistance;

        string aerobicStatus;
        string aerobicReason;
        if (meWeek < MinimumModerateEquivalentMinutesPerWeek)
        {
            aerobicStatus = StatusBelowGuideline;
            aerobicReason = ReasonAerobicBelow150;
        }
        else if (meWeek <= OptimalModerateEquivalentMinutesPerWeek)
        {
            aerobicStatus = StatusMeetsGuideline;
            aerobicReason = ReasonAerobicMeets150To300;
        }
        else
        {
            aerobicStatus = StatusExceedsGuideline;
            aerobicReason = ReasonAerobicExceeds300;
        }

        var resistanceStatus = meetsResistance ? ResistanceTargetMet : ResistanceTargetNotMet;
        var resistanceReason = meetsResistance ? ReasonResistanceTargetMet : ReasonResistanceTargetBelowTwoDays;

        return new AerobicHealthEvaluationResult(
            meWeek,
            safeResistanceDays,
            meetsAerobic,
            meetsResistance,
            meetsAll,
            aerobicStatus,
            resistanceStatus,
            [aerobicReason, resistanceReason]);
    }
}
