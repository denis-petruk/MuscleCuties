using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public sealed record MixedActivityWeekScheduleContext(
    IReadOnlyList<WorkoutActivityType>? SelectedActivities,
    IReadOnlyList<DayOfWeek>? AvailableWeekdays,
    decimal? DailyCapacityMinutes,
    IReadOnlyDictionary<WorkoutActivityType, decimal>? SessionDurationsMinutes,
    IReadOnlyList<ActivityPrescriptionContext>? EligibilityEvidence,
    IReadOnlyDictionary<WorkoutActivityType, int>? RequestedSessionsPerWeek = null,
    WorkoutActivityType? HiitUnderlyingCardioActivity = null,
    WorkoutActivityType? ChosenStrengthActivity = null);

public enum MixedActivityScheduleDisposition
{
    Scheduled,
    PartiallyScheduled,
    Omitted,
    Blocked,
    NotScheduled
}

public sealed record MixedActivityScheduledSession(
    WorkoutActivityType Activity,
    DayOfWeek Day,
    decimal DurationMinutes,
    bool IsHardSession);

public sealed record MixedActivityScheduleDecision(
    WorkoutActivityType Activity,
    MixedActivityScheduleDisposition Disposition,
    ActivityPrescriptionStatus? PrescriptionStatus,
    ActivitySessionPrescription? Prescription,
    int? PolicySessionsPerWeek,
    int? TargetSessionsPerWeek,
    int ScheduledSessionsPerWeek,
    IReadOnlyList<string> Reasons);

public sealed record MixedActivityWeekScheduleResult(
    IReadOnlyList<MixedActivityScheduledSession> Sessions,
    IReadOnlyList<MixedActivityScheduleDecision> Activities,
    IReadOnlyList<string> InputReasons);

/// <summary>Places selected activities over available weekdays under activity and daily-capacity rules.</summary>
public static class MixedActivityWeekScheduleRule
{
    public const int MinimumAvailableWeekdays = 2;
    public const int MaximumAvailableWeekdays = 7;
    public const int MaximumHardDaysPerWeek = 3;
    public const int MaximumRequestedSessionsPerWeek = MaximumAvailableWeekdays;

    public const string ActivitySelectionMissing = "ActivitySelectionMissing";
    public const string NoActivitiesSelected = "NoActivitiesSelected";
    public const string AvailableWeekdaysMustContainTwoToSevenDistinctDays =
        "AvailableWeekdaysMustContainTwoToSevenDistinctDays";
    public const string AvailableWeekdayValueInvalid = "AvailableWeekdayValueInvalid";
    public const string DailyCapacityMissingSingleActivityPerDayFallback =
        "DailyCapacityMissingSingleActivityPerDayFallback";
    public const string DailyCapacityMustBePositive = "DailyCapacityMustBePositive";
    public const string ActivityEligibilityEvidenceMissing = "ActivityEligibilityEvidenceMissing";
    public const string ActivityEligibilityEvidenceDuplicate = "ActivityEligibilityEvidenceDuplicate";
    public const string ActivityNotSupportedByPersistedPlan = "ActivityNotSupportedByPersistedPlan";
    public const string ActivityNotExplicitlySelected = "ActivityNotExplicitlySelected";
    public const string StrengthAlternativeNotChosen = "StrengthAlternativeNotChosen";
    public const string ChosenStrengthActivityNotSelected = "ChosenStrengthActivityNotSelected";
    public const string ChosenStrengthActivityMustBeStrengthActivity =
        "ChosenStrengthActivityMustBeStrengthActivity";
    public const string ActivityPrescriptionNotEligible = "ActivityPrescriptionNotEligible";
    public const string ClimbingCompetenceNotConfirmed = "ClimbingCompetenceNotConfirmed";
    public const string SessionDurationMissing = MixedActivitySessionDurationRule.SessionDurationMissing;
    public const string SessionDurationMustBePositive = MixedActivitySessionDurationRule.SessionDurationMustBePositive;
    public const string SessionDurationExceedsSupportedRange =
        MixedActivitySessionDurationRule.SessionDurationExceedsSupportedRange;
    public const string SessionDurationDoesNotMatchPrescription =
        MixedActivitySessionDurationRule.SessionDurationDoesNotMatchPrescription;
    public const string SessionDurationOutsideSavedPreferenceRange =
        MixedActivitySessionDurationRule.SessionDurationOutsideSavedPreferenceRange;
    public const string SessionDurationMustUseSavedPreferenceIncrement =
        MixedActivitySessionDurationRule.SessionDurationMustUseSavedPreferenceIncrement;
    public const string SelectedDurationAppliedToPrescription =
        MixedActivitySessionDurationRule.SelectedDurationAppliedToPrescription;
    public const string ActivityFrequencyPreferenceMissing = "ActivityFrequencyPreferenceMissing";
    public const string ActivityFrequencyPreferenceInvalid = "ActivityFrequencyPreferenceInvalid";
    public const string ActivityFrequencyLimitedByAvailableWeekdays =
        "ActivityFrequencyLimitedByAvailableWeekdays";
    public const string DailyCapacityTooSmallForSession = "DailyCapacityTooSmallForSession";
    public const string DailyCapacityLimitReached = "DailyCapacityLimitReached";
    public const string HardDayLimitReached = "HardDayLimitReached";
    public const string NoSafeDayAvailable = "NoSafeDayAvailable";
    public const string HiitUnderlyingCardioActivityMissing = "HiitUnderlyingCardioActivityMissing";
    public const string HiitUnderlyingCardioActivityUnsupported = "HiitUnderlyingCardioActivityUnsupported";
    public const string HiitRequiresSelectedEstablishedCardio = "HiitRequiresSelectedEstablishedCardio";
    public const string OneContinuousSessionReplacedByHiit = "OneContinuousSessionReplacedByHiit";
    public const string RestDayIsNotAWorkoutSession = "RestDayIsNotAWorkoutSession";
    public const string InvalidActivityValue = "InvalidActivityValue";

    private const int DaysPerWeek = MaximumAvailableWeekdays;
    private const int InvalidDayIndex = -1;
    private const int MissingHardPriorityIndex = -1;
    private const int MissingDistance = DaysPerWeek;
    private const int MinimumRequestedSessionsPerWeek = 1;
    private const int NoSessionsScheduled = 0;
    private const int OneSessionPerOccurrence = 1;
    private const decimal NoScheduledMinutes = decimal.Zero;
    private const int MondayOrder = 0;
    private const int TuesdayOrder = 1;
    private const int WednesdayOrder = 2;
    private const int ThursdayOrder = 3;
    private const int FridayOrder = 4;
    private const int SaturdayOrder = 5;
    private const int SundayOrder = 6;

    private static readonly WorkoutActivityType[] PreferenceFrequencyActivities =
    [
        WorkoutActivityType.Yoga,
        WorkoutActivityType.Mobility,
        WorkoutActivityType.GentleRecovery
    ];

    private static readonly WorkoutActivityType[] HardActivityPriority =
    [
        WorkoutActivityType.GeneralStrength,
        WorkoutActivityType.HighVolumeStrength,
        WorkoutActivityType.StrengthHighIntensity,
        WorkoutActivityType.RockClimbing,
        WorkoutActivityType.Hiit
    ];

    public static MixedActivityWeekScheduleResult Evaluate(MixedActivityWeekScheduleContext? context)
    {
        if (context is null)
            return new([], [], [ActivitySelectionMissing]);

        if (context.SelectedActivities is null)
            return new([], [], [ActivitySelectionMissing]);

        var selectedActivities = context.SelectedActivities.Distinct().OrderBy(ActivityOrder).ToArray();
        if (selectedActivities.Length == NoSessionsScheduled)
            return new([], [], [NoActivitiesSelected]);

        var decisions = selectedActivities.Select(activity => new ActivityDecisionBuilder(activity)).ToArray();
        var inputReasons = new List<string>();
        var days = NormalizeDays(context.AvailableWeekdays, inputReasons);
        var duplicateEvidenceActivities = FindDuplicateEvidenceActivities(context.EligibilityEvidence);

        foreach (var decision in decisions)
        {
            if (!Enum.IsDefined(decision.Activity))
            {
                decision.Block(InvalidActivityValue);
                continue;
            }

            if (decision.Activity == WorkoutActivityType.RestDay)
            {
                decision.DispositionOverride = MixedActivityScheduleDisposition.NotScheduled;
                decision.AddReason(RestDayIsNotAWorkoutSession);
                continue;
            }

            if (ShouldSkipStrengthActivity(decision, context, selectedActivities))
                continue;

            if (inputReasons.Count > NoSessionsScheduled)
            {
                decision.Block(inputReasons[NoSessionsScheduled]);
                continue;
            }

            PrepareActivity(decision, context, duplicateEvidenceActivities);
        }

        if (context.DailyCapacityMinutes is null)
        {
            foreach (var decision in decisions.Where(IsScheduleCandidate))
                decision.AddReason(DailyCapacityMissingSingleActivityPerDayFallback);
        }
        else if (context.DailyCapacityMinutes <= NoScheduledMinutes)
        {
            foreach (var decision in decisions.Where(IsScheduleCandidate))
                decision.Block(DailyCapacityMustBePositive);
        }

        ApplyHiitReplacement(decisions, context);

        if (inputReasons.Count == NoSessionsScheduled &&
            (context.DailyCapacityMinutes is null || context.DailyCapacityMinutes > NoScheduledMinutes))
            PlaceSessions(decisions, days, context.DailyCapacityMinutes);

        var sessions = decisions.SelectMany(decision => decision.ScheduledDays.Select(day =>
                new MixedActivityScheduledSession(decision.Activity, day, decision.DurationMinutes,
                    IsHardActivity(decision.Activity))))
            .OrderBy(session => DayIndex(session.Day))
            .ThenBy(session => ActivityOrder(session.Activity))
            .ToArray();

        return new MixedActivityWeekScheduleResult(sessions,
            decisions.Select(decision => decision.ToDecision()).ToArray(), inputReasons);
    }

    private static void PrepareActivity(ActivityDecisionBuilder decision,
        MixedActivityWeekScheduleContext context,
        IReadOnlySet<WorkoutActivityType> duplicateEvidenceActivities)
    {
        if (decision.Activity == WorkoutActivityType.Other)
        {
            decision.Omit(ActivityNotSupportedByPersistedPlan);
            return;
        }

        if (duplicateEvidenceActivities.Contains(decision.Activity))
        {
            decision.Block(ActivityEligibilityEvidenceDuplicate);
            return;
        }

        if (context.EligibilityEvidence is null)
        {
            decision.Block(ActivityEligibilityEvidenceMissing);
            return;
        }

        if (FindUniqueEvidence(context.EligibilityEvidence, decision.Activity) is not { } evidence)
        {
            decision.Block(ActivityEligibilityEvidenceMissing);
            return;
        }

        if (!evidence.IsExplicitlySelected)
        {
            decision.Block(ActivityNotExplicitlySelected);
            return;
        }

        if (decision.Activity == WorkoutActivityType.RockClimbing &&
            evidence.HasRopeClimbingCompetence is not true)
        {
            decision.Block(ClimbingCompetenceNotConfirmed);
            return;
        }

        var durationPreference = MixedActivitySessionDurationRule.ReadPreference(
            decision.Activity, context.SessionDurationsMinutes);
        if (!durationPreference.IsValid)
        {
            decision.Block(durationPreference.ReasonCode ?? SessionDurationMissing);
            return;
        }

        var prescriptionContext = MixedActivitySessionDurationRule.ApplyPreference(evidence,
            durationPreference.Preference);

        var prescription = ActivityPrescriptionRule.Evaluate(prescriptionContext);

        decision.PrescriptionStatus = prescription.Status;
        if (prescription.Status != ActivityPrescriptionStatus.Eligible || prescription.Sessions.Count != OneSessionPerOccurrence)
        {
            decision.Block(ActivityPrescriptionNotEligible);
            decision.AddReasons(prescription.Reasons);
            return;
        }

        var session = prescription.Sessions[0];
        decision.PolicySessionsPerWeek = PreferenceFrequencyActivities.Contains(decision.Activity)
            ? null
            : session.FrequencyPerWeek;

        if (!TryGetTargetFrequency(decision, context, session.FrequencyPerWeek))
            return;

        var duration = MixedActivitySessionDurationRule.Resolve(
            decision.Activity, durationPreference.Preference, session);
        if (!duration.IsResolved)
        {
            decision.Block(duration.ReasonCode ?? SessionDurationMissing);
            return;
        }

        decision.DurationMinutes = duration.DurationMinutes;
        if (duration.SelectedDurationAppliedToPrescription)
            decision.AddReason(SelectedDurationAppliedToPrescription);

        decision.Prescription = session;
        decision.IsEligible = true;
    }

    private static bool TryGetTargetFrequency(ActivityDecisionBuilder decision,
        MixedActivityWeekScheduleContext context, int policyFrequency)
    {
        if (!PreferenceFrequencyActivities.Contains(decision.Activity))
        {
            if (policyFrequency < MinimumRequestedSessionsPerWeek ||
                policyFrequency > MaximumRequestedSessionsPerWeek)
            {
                decision.Block(ActivityPrescriptionNotEligible);
                return false;
            }

            decision.TargetSessionsPerWeek = policyFrequency;
            return true;
        }

        if (context.RequestedSessionsPerWeek is null ||
            !context.RequestedSessionsPerWeek.TryGetValue(decision.Activity, out var requestedFrequency))
        {
            decision.Omit(ActivityFrequencyPreferenceMissing);
            return false;
        }

        if (requestedFrequency < MinimumRequestedSessionsPerWeek ||
            requestedFrequency > MaximumRequestedSessionsPerWeek)
        {
            decision.Omit(ActivityFrequencyPreferenceInvalid);
            return false;
        }

        decision.TargetSessionsPerWeek = requestedFrequency;
        return true;
    }

    private static ActivityPrescriptionContext? FindUniqueEvidence(
        IReadOnlyList<ActivityPrescriptionContext>? evidence,
        WorkoutActivityType activity)
    {
        if (evidence is null)
            return null;

        var matchingEvidence = evidence
            .Where(candidate => candidate is not null && candidate.Activity == activity)
            .Take(2)
            .ToArray();
        return matchingEvidence.Length == OneSessionPerOccurrence ? matchingEvidence[0] : null;
    }

    private static HashSet<WorkoutActivityType> FindDuplicateEvidenceActivities(
        IReadOnlyList<ActivityPrescriptionContext>? evidence) =>
        evidence?
            .Where(candidate => candidate is not null)
            .GroupBy(candidate => candidate.Activity)
            .Where(group => group.Count() > OneSessionPerOccurrence)
            .Select(group => group.Key)
            .ToHashSet() ?? [];

    private static void ApplyHiitReplacement(ActivityDecisionBuilder[] decisions,
        MixedActivityWeekScheduleContext context)
    {
        var hiit = decisions.FirstOrDefault(decision => decision.Activity == WorkoutActivityType.Hiit);
        if (hiit is null || !hiit.IsEligible) return;

        if (context.HiitUnderlyingCardioActivity is null)
        {
            hiit.Block(HiitUnderlyingCardioActivityMissing);
            return;
        }

        var underlyingActivity = context.HiitUnderlyingCardioActivity.Value;
        var cardioEvidence = FindUniqueEvidence(context.EligibilityEvidence, underlyingActivity);
        var hiitEvidence = FindUniqueEvidence(context.EligibilityEvidence, WorkoutActivityType.Hiit)?.HiitEvidence;
        if (!HiitEligibilityEvidenceRule.IsSupportedUnderlyingCardioActivity(underlyingActivity) ||
            hiitEvidence?.UnderlyingCardioActivity != underlyingActivity)
        {
            hiit.Block(HiitUnderlyingCardioActivityUnsupported);
            return;
        }

        var underlying = decisions.FirstOrDefault(decision => decision.Activity == underlyingActivity);
        if (underlying is null || !underlying.IsEligible || underlying.TargetSessionsPerWeek is null ||
            cardioEvidence?.Experience != ActivityExperience.Established)
        {
            hiit.Block(HiitRequiresSelectedEstablishedCardio);
            return;
        }

        if (underlying.TargetSessionsPerWeek <= MinimumRequestedSessionsPerWeek)
        {
            hiit.Block(HiitRequiresSelectedEstablishedCardio);
            return;
        }

        underlying.TargetSessionsPerWeek--;
        underlying.AddReason(OneContinuousSessionReplacedByHiit);
    }

    private static void PlaceSessions(ActivityDecisionBuilder[] decisions,
        IReadOnlyList<DayOfWeek> availableDays, decimal? dailyCapacityMinutes)
    {
        var dayStates = availableDays.ToDictionary(day => day, _ => new ScheduledDayState());
        var hardDecisions = decisions.Where(decision => decision.IsEligible && IsHardActivity(decision.Activity))
            .OrderBy(decision => HardActivityOrder(decision.Activity))
            .ThenBy(decision => ActivityOrder(decision.Activity))
            .ToArray();

        foreach (var decision in hardDecisions)
            ScheduleActivity(decision, dayStates, dailyCapacityMinutes, availableDays);

        var softDecisions = decisions.Where(decision => decision.IsEligible && !IsHardActivity(decision.Activity))
            .OrderBy(decision => ActivityOrder(decision.Activity))
            .ToArray();
        foreach (var decision in softDecisions)
            ScheduleActivity(decision, dayStates, dailyCapacityMinutes, availableDays);
    }

    private static void ScheduleActivity(ActivityDecisionBuilder decision,
        IDictionary<DayOfWeek, ScheduledDayState> dayStates,
        decimal? dailyCapacityMinutes,
        IReadOnlyList<DayOfWeek> availableDays)
    {
        if (decision.TargetSessionsPerWeek is null) return;
        if (dailyCapacityMinutes is { } capacity && decision.DurationMinutes > capacity)
        {
            decision.AddReason(DailyCapacityTooSmallForSession);
            return;
        }

        var target = decision.TargetSessionsPerWeek.Value;
        for (var sessionIndex = NoSessionsScheduled; sessionIndex < target; sessionIndex++)
        {
            var isHard = IsHardActivity(decision.Activity);
            var hardDays = dayStates.Where(pair => pair.Value.HasHardSession).Select(pair => pair.Key).ToArray();
            if (isHard && hardDays.Length >= MaximumHardDaysPerWeek)
            {
                decision.AddReason(HardDayLimitReached);
                break;
            }

            var candidates = availableDays.Where(day =>
            {
                var state = dayStates[day];
                if (state.Activities.Contains(decision.Activity)) return false;
                if (isHard && state.HasHardSession) return false;
                if (dailyCapacityMinutes is null)
                    return state.Activities.Count == NoSessionsScheduled;
                return decision.DurationMinutes <= dailyCapacityMinutes.Value - state.TotalMinutes;
            }).ToArray();

            if (candidates.Length == NoSessionsScheduled)
            {
                if (isHard && hardDays.Length >= Math.Min(MaximumHardDaysPerWeek, availableDays.Count))
                    decision.AddReason(HardDayLimitReached);
                else if (dailyCapacityMinutes is { } capacityLimit && availableDays.Any(day =>
                             decision.DurationMinutes > capacityLimit - dayStates[day].TotalMinutes))
                    decision.AddReason(DailyCapacityLimitReached);
                else
                    decision.AddReason(NoSafeDayAvailable);
                break;
            }

            var day = candidates.OrderByDescending(candidate => isHard
                    ? MinimumCircularDistance(candidate, hardDays)
                    : MinimumCircularDistance(candidate, decision.ScheduledDays))
                .ThenBy(candidate => dayStates[candidate].Activities.Count)
                .ThenBy(candidate => dayStates[candidate].TotalMinutes)
                .ThenBy(DayIndex)
                .First();

            dayStates[day].Add(decision.Activity, decision.DurationMinutes, isHard);
            decision.ScheduledDays.Add(day);
        }

        if (decision.ScheduledDays.Count < target && decision.ScheduledDays.Count == availableDays.Count)
            decision.AddReason(ActivityFrequencyLimitedByAvailableWeekdays);
    }

    private static int MinimumCircularDistance(DayOfWeek day, IReadOnlyCollection<DayOfWeek> occupiedDays)
    {
        if (occupiedDays.Count == NoSessionsScheduled) return MissingDistance;
        return occupiedDays.Min(occupiedDay =>
        {
            var directDistance = Math.Abs(DayIndex(day) - DayIndex(occupiedDay));
            return Math.Min(directDistance, DaysPerWeek - directDistance);
        });
    }

    private static DayOfWeek[] NormalizeDays(IReadOnlyList<DayOfWeek>? suppliedDays,
        List<string> reasons)
    {
        if (suppliedDays is null)
        {
            reasons.Add(AvailableWeekdaysMustContainTwoToSevenDistinctDays);
            return [];
        }

        var invalidDay = suppliedDays.Any(day => !Enum.IsDefined(day) || DayIndex(day) == InvalidDayIndex);
        if (invalidDay)
        {
            reasons.Add(AvailableWeekdayValueInvalid);
            return [];
        }

        var days = suppliedDays.Distinct().OrderBy(DayIndex).ToArray();
        if (days.Length is < MinimumAvailableWeekdays or > MaximumAvailableWeekdays)
            reasons.Add(AvailableWeekdaysMustContainTwoToSevenDistinctDays);
        return days;
    }

    private static bool IsScheduleCandidate(ActivityDecisionBuilder decision) =>
        decision.IsEligible && decision.TargetSessionsPerWeek is not null;

    private static bool ShouldSkipStrengthActivity(ActivityDecisionBuilder decision,
        MixedActivityWeekScheduleContext context, IReadOnlyCollection<WorkoutActivityType> selectedActivities)
    {
        if (context.ChosenStrengthActivity is not { } chosenStrengthActivity ||
            !IsStrengthChoice(decision.Activity))
            return false;

        if (!IsStrengthChoice(chosenStrengthActivity))
        {
            decision.Block(ChosenStrengthActivityMustBeStrengthActivity);
            return true;
        }

        if (!selectedActivities.Contains(chosenStrengthActivity))
        {
            decision.DispositionOverride = MixedActivityScheduleDisposition.NotScheduled;
            decision.AddReason(ChosenStrengthActivityNotSelected);
            return true;
        }

        if (decision.Activity == chosenStrengthActivity)
            return false;

        decision.DispositionOverride = MixedActivityScheduleDisposition.NotScheduled;
        decision.AddReason(StrengthAlternativeNotChosen);
        return true;
    }

    private static bool IsStrengthChoice(WorkoutActivityType activity) => activity is
        WorkoutActivityType.GeneralStrength or WorkoutActivityType.HighVolumeStrength or
        WorkoutActivityType.StrengthHighIntensity;

    private static bool IsHardActivity(WorkoutActivityType activity) => activity is
        WorkoutActivityType.GeneralStrength or WorkoutActivityType.HighVolumeStrength or
        WorkoutActivityType.StrengthHighIntensity or WorkoutActivityType.Hiit or
        WorkoutActivityType.RockClimbing;

    private static int HardActivityOrder(WorkoutActivityType activity)
    {
        var index = Array.IndexOf(HardActivityPriority, activity);
        return index == MissingHardPriorityIndex ? HardActivityPriority.Length : index;
    }

    private static int ActivityOrder(WorkoutActivityType activity) => (int)activity;

    private static int DayIndex(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => MondayOrder,
        DayOfWeek.Tuesday => TuesdayOrder,
        DayOfWeek.Wednesday => WednesdayOrder,
        DayOfWeek.Thursday => ThursdayOrder,
        DayOfWeek.Friday => FridayOrder,
        DayOfWeek.Saturday => SaturdayOrder,
        DayOfWeek.Sunday => SundayOrder,
        _ => InvalidDayIndex
    };

    private sealed class ScheduledDayState
    {
        public decimal TotalMinutes { get; private set; }
        public bool HasHardSession { get; private set; }
        public HashSet<WorkoutActivityType> Activities { get; } = [];

        public void Add(WorkoutActivityType activity, decimal durationMinutes, bool isHard)
        {
            Activities.Add(activity);
            TotalMinutes += durationMinutes;
            HasHardSession |= isHard;
        }
    }

    private sealed class ActivityDecisionBuilder(WorkoutActivityType activity)
    {
        private readonly List<string> _reasons = [];

        public WorkoutActivityType Activity { get; } = activity;
        public ActivityPrescriptionStatus? PrescriptionStatus { get; set; }
        public ActivitySessionPrescription? Prescription { get; set; }
        public int? PolicySessionsPerWeek { get; set; }
        public int? TargetSessionsPerWeek { get; set; }
        public decimal DurationMinutes { get; set; }
        public bool IsEligible { get; set; }
        public List<DayOfWeek> ScheduledDays { get; } = [];
        public MixedActivityScheduleDisposition? DispositionOverride { get; set; }

        public void Block(string reason)
        {
            IsEligible = false;
            DispositionOverride = MixedActivityScheduleDisposition.Blocked;
            AddReason(reason);
        }

        public void Omit(string reason)
        {
            IsEligible = false;
            DispositionOverride = MixedActivityScheduleDisposition.Omitted;
            AddReason(reason);
        }

        public void AddReason(string reason)
        {
            if (!_reasons.Contains(reason, StringComparer.Ordinal)) _reasons.Add(reason);
        }

        public void AddReasons(IEnumerable<string> reasons)
        {
            foreach (var reason in reasons) AddReason(reason);
        }

        public MixedActivityScheduleDecision ToDecision()
        {
            var disposition = DispositionOverride ??
                (ScheduledDays.Count == TargetSessionsPerWeek
                    ? MixedActivityScheduleDisposition.Scheduled
                    : ScheduledDays.Count > NoSessionsScheduled
                        ? MixedActivityScheduleDisposition.PartiallyScheduled
                        : MixedActivityScheduleDisposition.Omitted);

            return new(Activity, disposition, PrescriptionStatus, Prescription, PolicySessionsPerWeek,
                TargetSessionsPerWeek, ScheduledDays.Count, _reasons.ToArray());
        }
    }
}
