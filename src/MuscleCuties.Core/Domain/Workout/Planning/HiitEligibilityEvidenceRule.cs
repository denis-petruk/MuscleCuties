using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public sealed record HiitEligibilityEvidence(
    bool ExplicitOptIn,
    WorkoutActivityType? UnderlyingCardioActivity,
    ActivityExperience UnderlyingCardioExperience,
    IReadOnlyList<HiitAerobicWeekEvidence> PreviousFourWeeksAerobicEvidence,
    bool HasTrustedRestrictionOrCurrentSymptom,
    bool ExistingClearanceScreeningSatisfied);

/// <summary>Captures the count of manageable aerobic sessions of at least twenty minutes in one week.</summary>
public sealed record HiitAerobicWeekEvidence(
    int WeekOffsetFromCurrent,
    int QualifyingManageableAerobicSessionCount);

/// <summary>Checks the evidence required before a HIIT prescription can use selected cardio modality.</summary>
public static class HiitEligibilityEvidenceRule
{
    private static readonly IReadOnlyList<WorkoutActivityType> SupportedUnderlyingActivities =
        Array.AsReadOnly<WorkoutActivityType>(
        [
            WorkoutActivityType.Cycling,
            WorkoutActivityType.Running,
            WorkoutActivityType.Walking,
            WorkoutActivityType.Rowing,
            WorkoutActivityType.Elliptical
        ]);

    public const string EvidenceNotEstablishedByRequiredEvidence =
        "HiitEligibilityNotEstablishedByRequiredEvidence";

    public static IReadOnlyList<WorkoutActivityType> SupportedUnderlyingCardioActivities =>
        SupportedUnderlyingActivities;

    public static bool IsSupportedUnderlyingCardioActivity(WorkoutActivityType? activity) =>
        activity is { } selectedActivity && SupportedUnderlyingActivities.Contains(selectedActivity);

    public static bool IsEligible(HiitEligibilityEvidence? evidence)
    {
        if (evidence is null || !evidence.ExplicitOptIn ||
            !IsSupportedUnderlyingCardioActivity(evidence.UnderlyingCardioActivity) ||
            evidence.UnderlyingCardioExperience != ActivityExperience.Established ||
            evidence.HasTrustedRestrictionOrCurrentSymptom || !evidence.ExistingClearanceScreeningSatisfied ||
            evidence.PreviousFourWeeksAerobicEvidence is null)
            return false;

        var weeks = evidence.PreviousFourWeeksAerobicEvidence;
        if (weeks.Any(week => week is null || week.WeekOffsetFromCurrent is < 1 or >
                ActivityPrescriptionRule.HiitRequiredPriorWeeks ||
            week.QualifyingManageableAerobicSessionCount < 0))
            return false;

        return Enumerable.Range(1, ActivityPrescriptionRule.HiitRequiredPriorWeeks).All(weekOffset =>
        {
            var matchingWeeks = weeks.Where(week => week.WeekOffsetFromCurrent == weekOffset).ToArray();
            return matchingWeeks.Length == 1 &&
                   matchingWeeks[0].QualifyingManageableAerobicSessionCount >=
                   ActivityPrescriptionRule.HiitRequiredAerobicSessionsPerWeek;
        });
    }
}
