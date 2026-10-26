using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public sealed record MixedActivityWeekScheduleContext(
    IReadOnlyList<WorkoutActivityType>? SelectedActivities,
    IReadOnlyList<DayOfWeek>? AvailableWeekdays,
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
    IReadOnlyList<string> Reasons,
    WorkoutActivityType? EffectiveActivity = null,
    string? FallbackReasonCode = null,
    int? MaxSessionsPerWeek = null);

public sealed record MixedActivityWeekScheduleResult(
    IReadOnlyList<MixedActivityScheduledSession> Sessions,
    IReadOnlyList<MixedActivityScheduleDecision> Activities,
    IReadOnlyList<string> InputReasons);

/// <summary>Places selected activities over available weekdays under activity and hard-day safety rules.</summary>
public static class MixedActivityWeekScheduleRule
{
    public const int MinimumAvailableWeekdays = 1;
    public const int MaximumAvailableWeekdays = 7;
    public const int MaximumHardDaysPerWeek = 3;
    public const int MaximumRequestedSessionsPerWeek = MaximumAvailableWeekdays;
    public const int MaximumStrengthSessionsPerDay = 1;
    public const int MaximumStrengthCompanionsPerDay = 1;
    public const int MaximumSessionsPerDay =
        MaximumStrengthSessionsPerDay + MaximumStrengthCompanionsPerDay;

    public const string ActivitySelectionMissing = "ActivitySelectionMissing";
    public const string NoActivitiesSelected = "NoActivitiesSelected";
    public const string AvailableWeekdaysMustContainOneToSevenDistinctDays =
        "AvailableWeekdaysMustContainOneToSevenDistinctDays";
    public const string AvailableWeekdayValueInvalid = "AvailableWeekdayValueInvalid";
    public const string ActivityEligibilityEvidenceMissing = "ActivityEligibilityEvidenceMissing";
    public const string ActivityEligibilityEvidenceDuplicate = "ActivityEligibilityEvidenceDuplicate";
    public const string ActivityNotSupportedByPersistedPlan = "ActivityNotSupportedByPersistedPlan";
    public const string ActivityNotExplicitlySelected = "ActivityNotExplicitlySelected";
    public const string StrengthAlternativeNotChosen = "StrengthAlternativeNotChosen";
    public const string ChosenStrengthActivityNotSelected = "ChosenStrengthActivityNotSelected";
    public const string ChosenStrengthActivityMustBeStrengthActivity =
        "ChosenStrengthActivityMustBeStrengthActivity";
    public const string YogaContentClassificationMissing = "YogaContentClassificationMissing";
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
            if (decision.Activity == WorkoutActivityType.GeneralStrength)
            {
                var gsEvidence = FindUniqueEvidence(context.EligibilityEvidence, WorkoutActivityType.GeneralStrength);
                var gsPolicy = ResolveGeneralStrengthPolicy(gsEvidence, days.Length);
                decision.PolicySessionsPerWeek = gsPolicy.PolicySessionsPerWeek;
                decision.TargetSessionsPerWeek = gsPolicy.TargetSessionsPerWeek;
                decision.MaxSessionsPerWeek = gsPolicy.MaxSessionsPerWeek;
            }
            else
            {
                decision.PolicySessionsPerWeek = ActivityPreferenceDefaults.PolicySessionsPerWeek(decision.Activity);
                decision.TargetSessionsPerWeek = ActivityPreferenceDefaults.SessionsPerWeek(
                    decision.Activity, days.Length) ?? decision.PolicySessionsPerWeek;
            }

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

            PrepareActivity(decision, context, duplicateEvidenceActivities, days);
        }

        ApplyHiitReplacement(decisions, context);

        if (inputReasons.Count == NoSessionsScheduled)
            PlaceSessions(decisions, days);

        var sessions = decisions
            .Where(decision => decision.IsEligible)
            .SelectMany(decision => decision.ScheduledDays.Select(day =>
                new MixedActivityScheduledSession(decision.EffectiveActivity ?? decision.Activity, day, decision.DurationMinutes,
                    IsHardActivity(decision.EffectiveActivity ?? decision.Activity, decision.Prescription))))
            .OrderBy(session => DayIndex(session.Day))
            .ThenBy(session => ActivityOrder(session.Activity))
            .ToArray();

        return new MixedActivityWeekScheduleResult(sessions,
            decisions.Select(decision => decision.ToDecision()).ToArray(), inputReasons);
    }

    private static void PrepareActivity(ActivityDecisionBuilder decision,
        MixedActivityWeekScheduleContext context,
        HashSet<WorkoutActivityType> duplicateEvidenceActivities,
        DayOfWeek[] days)
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

        var isDemandingYoga = false;
        if (decision.Activity == WorkoutActivityType.Yoga)
        {
            var yogaClassification = YogaClassificationRule.Classify(
                evidence.IsDemandingYogaContent,
                evidence.ActivityName,
                evidence.ContentTags);
            if (yogaClassification.IsInconsistent)
            {
                decision.Block(yogaClassification.ReasonCode ?? YogaClassificationRule.YogaClassificationInconsistentCode);
                return;
            }
            isDemandingYoga = yogaClassification.IsDemanding;
        }

        var durationPreference = MixedActivitySessionDurationRule.ReadPreference(
            decision.Activity, context.SessionDurationsMinutes, isDemandingYoga);
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
        decision.PolicySessionsPerWeek = session.FrequencyPerWeek;

        if (decision.Activity == WorkoutActivityType.GeneralStrength)
        {
            var gsPolicy = ResolveGeneralStrengthPolicy(evidence, days.Length);
            decision.PolicySessionsPerWeek = gsPolicy.PolicySessionsPerWeek;
            decision.MaxSessionsPerWeek = gsPolicy.MaxSessionsPerWeek;
            decision.TargetSessionsPerWeek = gsPolicy.TargetSessionsPerWeek;
        }

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
        if (decision.Activity == WorkoutActivityType.HighVolumeStrength &&
            prescription.Reasons.Contains(ActivityPrescriptionRule.HighVolumeConservativeStartCode, StringComparer.Ordinal))
            decision.AddReason(ActivityPrescriptionRule.HighVolumeConservativeStartCode);
    }

    private static bool TryGetTargetFrequency(ActivityDecisionBuilder decision,
        MixedActivityWeekScheduleContext context, int policyFrequency)
    {
        if (decision.Activity == WorkoutActivityType.GeneralStrength)
            return true;

        if (context.RequestedSessionsPerWeek is not null &&
            context.RequestedSessionsPerWeek.TryGetValue(decision.Activity, out var savedFrequency))
        {
            if (savedFrequency < MinimumRequestedSessionsPerWeek ||
                savedFrequency > MaximumRequestedSessionsPerWeek)
            {
                decision.Omit(ActivityFrequencyPreferenceInvalid);
                return false;
            }

            var max = decision.MaxSessionsPerWeek ?? MaximumRequestedSessionsPerWeek;
            decision.TargetSessionsPerWeek = Math.Min(savedFrequency, max);
            return true;
        }

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

        var requestedFrequency = ActivityPreferenceDefaults.SessionsPerWeek(decision.Activity,
            context.AvailableWeekdays?.Count ?? MinimumAvailableWeekdays) ?? policyFrequency;

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

    private static GeneralStrengthFrequencyPolicy ResolveGeneralStrengthPolicy(
        ActivityPrescriptionContext? evidence, int availableDayCount)
    {
        var profileLevel = evidence?.Experience == ActivityExperience.Established
            ? TrainingExperienceLevel.Intermediate
            : TrainingExperienceLevel.Beginner;
        return GeneralStrengthFrequencyResolver.Resolve(
            availableDayCount, evidence?.IsEstablishedResistanceHistory == true, profileLevel);
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

    private static void PlaceSessions(ActivityDecisionBuilder[] decisions, DayOfWeek[] availableDays)
    {
        ReduceTargetsToFitAvailability(decisions, availableDays.Length);
        var dayStates = availableDays.ToDictionary(day => day, _ => new ScheduledDayState());
        var hardDecisions = decisions.Where(decision => decision.IsEligible &&
                IsHardActivity(decision.EffectiveActivity ?? decision.Activity, decision.Prescription))
            .OrderBy(decision => HardActivityOrder(decision.Activity))
            .ThenBy(decision => ActivityOrder(decision.Activity))
            .ToArray();

        foreach (var decision in hardDecisions)
            ScheduleActivity(decision, dayStates, availableDays);

        var softDecisions = decisions.Where(decision => decision.IsEligible &&
                !IsHardActivity(decision.EffectiveActivity ?? decision.Activity, decision.Prescription))
            .OrderBy(decision => CanBeStrengthCompanion(decision)
                ? OneSessionPerOccurrence
                : NoSessionsScheduled)
            .ThenBy(decision => ActivityOrder(decision.Activity))
            .ToArray();
        foreach (var decision in softDecisions)
            ScheduleActivity(decision, dayStates, availableDays);
    }

    private static void ReduceTargetsToFitAvailability(ActivityDecisionBuilder[] decisions, int availableDayCount)
    {
        var candidates = decisions.Where(IsScheduleCandidate).ToArray();
        foreach (var decision in candidates)
            if (IsStrengthChoice(decision.EffectiveActivity ?? decision.Activity) &&
                decision.TargetSessionsPerWeek < GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek)
                decision.TargetSessionsPerWeek = GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek;

        var hardCandidates = candidates.Where(decision =>
            IsHardActivity(decision.EffectiveActivity ?? decision.Activity, decision.Prescription)).ToArray();
        ReduceTargets(hardCandidates, Math.Min(MaximumHardDaysPerWeek, availableDayCount));

        var hardSessionCount = hardCandidates.Sum(GetTargetSessions);
        var occupiedHardDayCount = Math.Min(hardSessionCount, availableDayCount);
        var emptyDaySlots = availableDayCount - occupiedHardDayCount;
        var standaloneSoftCandidates = candidates.Where(decision =>
                !IsHardActivity(decision.EffectiveActivity ?? decision.Activity, decision.Prescription) &&
                !CanBeStrengthCompanion(decision))
            .ToArray();
        ReduceTargets(standaloneSoftCandidates, emptyDaySlots);

        var requestedStandaloneSessions = standaloneSoftCandidates.Sum(GetTargetSessions);
        var remainingEmptyDaySlots = Math.Max(NoSessionsScheduled, emptyDaySlots - requestedStandaloneSessions);
        var strengthSessionCount = candidates
            .Where(decision => IsStrengthChoice(decision.EffectiveActivity ?? decision.Activity))
            .Sum(GetTargetSessions);
        var strengthCompanionSlots = Math.Min(strengthSessionCount, availableDayCount);
        var strengthCompanionCandidates = candidates.Where(CanBeStrengthCompanion).ToArray();
        ReduceTargets(strengthCompanionCandidates, remainingEmptyDaySlots + strengthCompanionSlots);
    }

    private static int GetTargetSessions(ActivityDecisionBuilder decision) =>
        decision.TargetSessionsPerWeek ?? NoSessionsScheduled;

    private static bool CanBeStrengthCompanion(ActivityDecisionBuilder decision)
    {
        var activity = decision.EffectiveActivity ?? decision.Activity;
        if (IsHardActivity(activity, decision.Prescription))
            return false;

        return activity is WorkoutActivityType.Cycling or WorkoutActivityType.Yoga;
    }

    private static void ReduceTargets(ActivityDecisionBuilder[] decisions, int availableSlots)
    {
        var requestedSlots = decisions.Sum(decision => decision.TargetSessionsPerWeek ?? NoSessionsScheduled);
        while (requestedSlots > availableSlots)
        {
            var reducible = decisions
                .Where(decision => decision.TargetSessionsPerWeek > MinimumSessionsForReduction(decision))
                .OrderByDescending(decision => decision.TargetSessionsPerWeek - MinimumSessionsForReduction(decision))
                .ThenBy(decision => IsStrengthChoice(decision.EffectiveActivity ?? decision.Activity))
                .ThenBy(decision => ActivityOrder(decision.Activity))
                .FirstOrDefault();
            if (reducible is null)
                break;

            reducible.TargetSessionsPerWeek--;
            reducible.AddReason(ActivityFrequencyLimitedByAvailableWeekdays);
            requestedSlots--;
        }
    }

    private static int MinimumSessionsForReduction(ActivityDecisionBuilder decision) =>
        IsStrengthChoice(decision.EffectiveActivity ?? decision.Activity)
            ? GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek
            : MinimumRequestedSessionsPerWeek;

    private static void ScheduleActivity(ActivityDecisionBuilder decision,
        IDictionary<DayOfWeek, ScheduledDayState> dayStates,
        DayOfWeek[] availableDays)
    {
        if (decision.TargetSessionsPerWeek is null) return;

        var target = decision.TargetSessionsPerWeek.Value;
        if (decision.MaxSessionsPerWeek is { } maxLimit)
            target = Math.Min(target, maxLimit);
        for (var sessionIndex = NoSessionsScheduled; sessionIndex < target; sessionIndex++)
        {
            var isHard = IsHardActivity(decision.EffectiveActivity ?? decision.Activity, decision.Prescription);
            var hardDays = dayStates.Where(pair => pair.Value.HasHardSession).Select(pair => pair.Key).ToArray();
            if (isHard && hardDays.Length >= MaximumHardDaysPerWeek)
            {
                decision.AddReason(GetHardDayConflictReason(decision.Activity));
                decision.AddReason(HardDayLimitReached);
                break;
            }

            var candidates = availableDays.Where(day =>
            {
                var state = dayStates[day];
                if (state.Activities.Contains(decision.Activity)) return false;
                if (isHard && state.HasHardSession) return false;
                if (decision.Activity == WorkoutActivityType.Hiit &&
                    state.Activities.Any(a => a is WorkoutActivityType.GeneralStrength or
                                                  WorkoutActivityType.HighVolumeStrength or
                                                  WorkoutActivityType.StrengthHighIntensity or
                                                  WorkoutActivityType.RockClimbing))
                    return false;
                return state.Activities.Count == NoSessionsScheduled ||
                       CanShareStrengthDay(state, decision, isHard);
            }).ToArray();

            if (candidates.Length == NoSessionsScheduled)
            {
                if (isHard && hardDays.Length >= Math.Min(MaximumHardDaysPerWeek, availableDays.Length))
                {
                    decision.AddReason(GetHardDayConflictReason(decision.Activity));
                    decision.AddReason(HardDayLimitReached);
                }
                else
                    decision.AddReason(NoSafeDayAvailable);
                break;
            }

            var day = candidates.OrderByDescending(candidate => isHard
                    ? MinimumCircularDistance(candidate, hardDays)
                    : MinimumCircularDistance(candidate, decision.ScheduledDays))
                .ThenBy(candidate => dayStates[candidate].Activities.Count)
                .ThenBy(DayIndex)
                .First();

            dayStates[day].Add(decision.Activity, isHard);
            decision.ScheduledDays.Add(day);
        }

        if (decision.ScheduledDays.Count < target && decision.ScheduledDays.Count == availableDays.Length)
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
            reasons.Add(AvailableWeekdaysMustContainOneToSevenDistinctDays);
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
            reasons.Add(AvailableWeekdaysMustContainOneToSevenDistinctDays);
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

    public static bool IsHardActivity(WorkoutActivityType activity, ActivitySessionPrescription? prescription = null)
    {
        if (activity is WorkoutActivityType.GeneralStrength or WorkoutActivityType.HighVolumeStrength or
            WorkoutActivityType.StrengthHighIntensity or WorkoutActivityType.Hiit or
            WorkoutActivityType.RockClimbing)
            return true;

        if (activity == WorkoutActivityType.Yoga &&
            prescription is not null &&
            (prescription.TrainingStyle == SessionTrainingStyle.Demanding || prescription.Intensity == IntendedIntensity.Vigorous))
            return true;

        return false;
    }

    public static bool IsSupportedSameDayStrengthCompanion(
        WorkoutActivityType firstActivity,
        bool firstIsHard,
        WorkoutActivityType secondActivity,
        bool secondIsHard) =>
        (IsStrengthChoice(firstActivity) && firstIsHard && IsStrengthCompanion(secondActivity, secondIsHard)) ||
        (IsStrengthChoice(secondActivity) && secondIsHard && IsStrengthCompanion(firstActivity, firstIsHard));

    private static bool CanShareStrengthDay(
        ScheduledDayState dayState,
        ActivityDecisionBuilder decision,
        bool isHard)
    {
        if (dayState.Activities.Count != MaximumStrengthSessionsPerDay || !dayState.HasHardSession)
            return false;

        return IsSupportedSameDayStrengthCompanion(
            dayState.Activities.Single(),
            dayState.HasHardSession,
            decision.EffectiveActivity ?? decision.Activity,
            isHard);
    }

    private static bool IsStrengthCompanion(WorkoutActivityType activity, bool isHard) =>
        !isHard && (activity is WorkoutActivityType.Cycling or WorkoutActivityType.Yoga);

    public static string GetHardDayConflictReason(WorkoutActivityType activity) => activity switch
    {
        WorkoutActivityType.GeneralStrength => "GS_HARD_DAY_CONFLICT",
        WorkoutActivityType.HighVolumeStrength => "HVS_HARD_DAY_CONFLICT",
        WorkoutActivityType.StrengthHighIntensity => "SHI_HARD_DAY_CONFLICT",
        WorkoutActivityType.Hiit => "HIIT_HARD_DAY_CONFLICT",
        WorkoutActivityType.RockClimbing => "CLIMBING_HARD_DAY_CONFLICT",
        WorkoutActivityType.Yoga => "YOGA_HARD_DAY_CONFLICT",
        _ => HardDayLimitReached
    };

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
        public bool HasHardSession { get; private set; }
        public HashSet<WorkoutActivityType> Activities { get; } = [];

        public void Add(WorkoutActivityType activity, bool isHard)
        {
            Activities.Add(activity);
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
        public int? MaxSessionsPerWeek { get; set; }
        public decimal DurationMinutes { get; set; }
        public bool IsEligible { get; set; }
        public WorkoutActivityType? EffectiveActivity { get; set; }
        public string? FallbackReasonCode { get; set; }
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

            return new(Activity, disposition, PrescriptionStatus, Prescription,
                PolicySessionsPerWeek,
                TargetSessionsPerWeek,
                ScheduledDays.Count, _reasons.ToArray(),
                EffectiveActivity ?? Activity, FallbackReasonCode,
                MaxSessionsPerWeek);
        }
    }
}
