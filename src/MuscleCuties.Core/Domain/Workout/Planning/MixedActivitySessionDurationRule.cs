using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

internal sealed record UserSelectedSessionDuration(int Minutes);

internal sealed record SessionDurationPreferenceReadResult(
    bool IsValid,
    UserSelectedSessionDuration? Preference,
    string? ReasonCode);

internal sealed record MixedActivitySessionDurationResolution(
    bool IsResolved,
    decimal DurationMinutes,
    string? ReasonCode,
    bool SelectedDurationAppliedToPrescription);

/// <summary>Resolves each scheduled activity's duration from its saved preference or prescription.</summary>
internal static class MixedActivitySessionDurationRule
{
    public const string SessionDurationMissing = "SessionDurationMissing";
    public const string SessionDurationMustBePositive = "SessionDurationMustBePositive";
    public const string SessionDurationExceedsSupportedRange = "SessionDurationExceedsSupportedRange";
    public const string SessionDurationDoesNotMatchPrescription = "SessionDurationDoesNotMatchPrescription";
    public const string SessionDurationOutsideSavedPreferenceRange = "SessionDurationOutsideSavedPreferenceRange";
    public const string SessionDurationMustUseSavedPreferenceIncrement =
        "SessionDurationMustUseSavedPreferenceIncrement";
    public const string SelectedDurationAppliedToPrescription = "SelectedDurationAppliedToPrescription";

    private const int SecondsPerMinute = 60;
    private const int NoIntervals = 0;
    private const decimal NoDurationMinutes = decimal.Zero;

    public static SessionDurationPreferenceReadResult ReadPreference(
        WorkoutActivityType activity,
        IReadOnlyDictionary<WorkoutActivityType, decimal>? savedDurations,
        bool isDemandingYoga = false)
    {
        if (ActivitySessionDurationRule.IsCardioActivity(activity))
            return new SessionDurationPreferenceReadResult(true, null, null);

        var duration = decimal.Zero;
        if (savedDurations is null || !savedDurations.TryGetValue(activity, out duration))
        {
            if (WorkoutPreferencesRule.RequiresSessionDuration(activity))
                return Invalid(SessionDurationMissing);

            if (activity == WorkoutActivityType.Yoga)
            {
                var defaultMinutes = isDemandingYoga
                    ? ActivitySessionDurationRule.DemandingYogaDurationMinutesDefault
                    : ActivitySessionDurationRule.YogaDurationMinutesDefault;
                return new SessionDurationPreferenceReadResult(true, new UserSelectedSessionDuration(defaultMinutes), null);
            }

            if (activity == WorkoutActivityType.Mobility)
            {
                return new SessionDurationPreferenceReadResult(true,
                    new UserSelectedSessionDuration(ActivitySessionDurationRule.MobilityDurationMinutesDefault), null);
            }

            if (activity == WorkoutActivityType.GentleRecovery)
            {
                return new SessionDurationPreferenceReadResult(true,
                    new UserSelectedSessionDuration(ActivitySessionDurationRule.GentleRecoveryDurationMinutesDefault), null);
            }

            return new SessionDurationPreferenceReadResult(true, null, null);
        }

        if (duration <= NoDurationMinutes)
            return Invalid(SessionDurationMustBePositive);
        if (decimal.Truncate(duration) != duration || duration > int.MaxValue)
            return Invalid(SessionDurationExceedsSupportedRange);

        var minutes = decimal.ToInt32(duration);
        var selectionIssue = ActivitySessionDurationRule.GetSelectionIssue(activity, minutes, isDemandingYoga);
        if (selectionIssue is not null)
            return Invalid(selectionIssue switch
            {
                ActivitySessionDurationSelectionIssue.BelowMinimum or
                    ActivitySessionDurationSelectionIssue.AboveMaximum =>
                    SessionDurationOutsideSavedPreferenceRange,
                ActivitySessionDurationSelectionIssue.InvalidIncrement =>
                    SessionDurationMustUseSavedPreferenceIncrement,
                _ => SessionDurationMissing
            });

        return new SessionDurationPreferenceReadResult(true, new UserSelectedSessionDuration(minutes), null);
    }

    public static ActivityPrescriptionContext ApplyPreference(
        ActivityPrescriptionContext evidence,
        UserSelectedSessionDuration? preference) =>
        preference is null
            ? evidence
            : evidence with { UserSelectedDurationMinutes = preference.Minutes };

    public static MixedActivitySessionDurationResolution Resolve(
        WorkoutActivityType activity,
        UserSelectedSessionDuration? userSelectedDuration,
        ActivitySessionPrescription prescription)
    {
        ArgumentNullException.ThrowIfNull(prescription);

        if (WorkoutPreferencesRule.RequiresSessionDuration(activity))
            return ResolveUserPreferenceDuration(activity, userSelectedDuration, prescription);

        return TryGetPrescribedDuration(prescription, out var prescribedMinutes)
            ? new MixedActivitySessionDurationResolution(true, prescribedMinutes, null, false)
            : Unresolved(SessionDurationMissing);
    }

    private static MixedActivitySessionDurationResolution ResolveUserPreferenceDuration(
        WorkoutActivityType activity,
        UserSelectedSessionDuration? userSelectedDuration,
        ActivitySessionPrescription prescription)
    {
        if (userSelectedDuration is null)
            return Unresolved(SessionDurationMissing);

        if (!TryGetPrescribedDuration(prescription, out var prescribedMinutes))
        {
            if (prescription.Modality != ActivityModality.Strength)
                return Unresolved(SessionDurationMissing);

            return new MixedActivitySessionDurationResolution(true, userSelectedDuration.Minutes, null, false);
        }
        if (prescribedMinutes != userSelectedDuration.Minutes)
            return Unresolved(SessionDurationDoesNotMatchPrescription);

        return new MixedActivitySessionDurationResolution(true, prescribedMinutes,
            SelectedDurationAppliedToPrescription, true);
    }

    private static bool TryGetPrescribedDuration(
        ActivitySessionPrescription prescription,
        out decimal durationMinutes)
    {
        var hasIntervalShape = prescription.IntervalWorkCount is not null ||
                               prescription.IntervalWorkSeconds is not null ||
                               prescription.IntervalRecoveryCount is not null ||
                               prescription.IntervalRecoverySeconds is not null;

        if (!hasIntervalShape)
        {
            durationMinutes = (prescription.WarmupMinutes ?? NoDurationMinutes) +
                              (prescription.MainMinutes ?? NoDurationMinutes) +
                              (prescription.CooldownMinutes ?? NoDurationMinutes);
            return durationMinutes > NoDurationMinutes;
        }

        if (!HasValidIntervalPair(prescription.IntervalWorkCount, prescription.IntervalWorkSeconds, required: true) ||
            !HasValidIntervalPair(prescription.IntervalRecoveryCount, prescription.IntervalRecoverySeconds,
                required: false))
        {
            durationMinutes = NoDurationMinutes;
            return false;
        }

        var intervalSeconds =
            prescription.IntervalWorkCount!.Value * prescription.IntervalWorkSeconds!.Value +
            (prescription.IntervalRecoveryCount ?? NoIntervals) *
            (prescription.IntervalRecoverySeconds ?? NoDurationMinutes);
        durationMinutes = (prescription.WarmupMinutes ?? NoDurationMinutes) +
                          (prescription.CooldownMinutes ?? NoDurationMinutes) +
                          intervalSeconds / SecondsPerMinute;
        return durationMinutes > NoDurationMinutes;
    }

    private static bool HasValidIntervalPair(int? count, decimal? seconds, bool required)
    {
        if (count is null && seconds is null)
            return !required;
        return count is > NoIntervals && seconds is > NoDurationMinutes;
    }

    private static MixedActivitySessionDurationResolution Unresolved(string reasonCode) =>
        new(false, NoDurationMinutes, reasonCode, false);

    private static SessionDurationPreferenceReadResult Invalid(string reasonCode) =>
        new(false, null, reasonCode);
}
