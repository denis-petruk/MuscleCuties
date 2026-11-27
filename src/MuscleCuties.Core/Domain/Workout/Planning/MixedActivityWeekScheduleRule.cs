using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>Everything the week schedule is decided from: activities, availability and the cycle phase.</summary>
public sealed record MixedActivityWeekScheduleContext(
    IReadOnlyList<WorkoutActivityType>? SelectedActivities,
    IReadOnlyList<DayOfWeek>? AvailableWeekdays,
    IReadOnlyDictionary<WorkoutActivityType, decimal>? SessionDurationsMinutes,
    IReadOnlyList<ActivityPrescriptionContext>? EligibilityEvidence,
    IReadOnlyDictionary<WorkoutActivityType, int>? RequestedSessionsPerWeek = null,
    WorkoutActivityType? HiitUnderlyingCardioActivity = null,
    WorkoutActivityType? ChosenStrengthActivity = null);

/// <summary>How an activity ended up in the week: placed, omitted, or blocked for review.</summary>
public enum MixedActivityScheduleDisposition
{
    Scheduled,
    PartiallyScheduled,
    Omitted,
    Blocked,
    NotScheduled
}

/// <summary>One session the rule placed on a day.</summary>
public sealed record MixedActivityScheduledSession(
    WorkoutActivityType Activity,
    DayOfWeek Day,
    decimal DurationMinutes,
    bool IsHardSession,
    int? DayOrder = null,
    ActivitySessionPrescription? OccurrencePrescription = null);

/// <summary>The per-activity decision with the reasons that produced it.</summary>
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

/// <summary>The whole week: the placed sessions and the strength assignments they leave room for.</summary>
public sealed record MixedActivityWeekScheduleResult(
    IReadOnlyList<MixedActivityScheduledSession> Sessions,
    IReadOnlyList<MixedActivityScheduleDecision> Activities,
    IReadOnlyList<string> InputReasons)
{
    /// <summary>The strength slots left free once the other activities took their days.</summary>
    public IReadOnlyList<StrengthScheduleAssignment>? StrengthAssignments { get; init; }
}

/// <summary>Places selected activities over available weekdays under activity and hard-day safety rules.</summary>
public static class MixedActivityWeekScheduleRule
{
    public const int MinimumAvailableWeekdays = 1;
    public const int MaximumAvailableWeekdays = 7;
    public const int MaximumHardDaysPerWeek = 6;
    public const int MaximumRequestedSessionsPerWeek = MaximumAvailableWeekdays;
    public const int MaximumStrengthSessionsPerDay = 1;
    // Owner requirement (2026-11-08): at most two activities per day — one strength session and
    // at most one companion. This supersedes the 3–4 per day planned in ACTIVITY_EXPERIENCE.md.
    public const int MaximumStrengthCompanionsPerDay = 1;
    public const int MaximumHardSessionsPerDay = 2;
    public const int MaximumHardSessionsOnAdjustedDay = 3;
    public const int MinimumHardSessionsForAdjustment = MaximumHardSessionsOnAdjustedDay;
    public const int MaximumSessionsPerDay =
        MaximumStrengthSessionsPerDay + MaximumStrengthCompanionsPerDay;

    public const string ActivitySelectionMissing = "ActivitySelectionMissing";
    public const string NoActivitiesSelected = "NoActivitiesSelected";
    // The value is a serialized client reason code and stays stable; the rule it names is
    // MinimumAvailableWeekdays..MaximumAvailableWeekdays (1–7 distinct days).
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

    /// <summary>Places every activity and refuses the week when hard sessions cannot be spaced safely.</summary>
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
        sessions = ApplyOccurrenceAdjustments(sessions, decisions.Where(decision => decision.IsEligible)
            .GroupBy(decision => decision.EffectiveActivity ?? decision.Activity)
            .ToDictionary(group => group.Key, group => group.First(decision => decision.IsEligible).Prescription));

        sessions = ApplyCardioOnlyDayDurations(sessions, decisions);

        inputReasons.AddRange(WorkoutScheduleValidationRule.Validate(
            sessions.Select(session => new WorkoutScheduleValidationSession(
                session.Day, session.Activity, session.IsHardSession, null, session.DurationMinutes,
                decisions.FirstOrDefault(decision => (decision.EffectiveActivity ?? decision.Activity) == session.Activity)?.Prescription,
                session.OccurrencePrescription)).ToArray(),
            requireStrengthMuscles: false));
        return new MixedActivityWeekScheduleResult(sessions,
            decisions.Select(decision => decision.ToDecision()).ToArray(), inputReasons);
    }

    /// <summary>Applies deloads and optional-session removals to the week's occurrences.</summary>
    public static MixedActivityScheduledSession[] ApplyOccurrenceAdjustments(
        IEnumerable<MixedActivityScheduledSession> source,
        IReadOnlyDictionary<WorkoutActivityType, ActivitySessionPrescription?> originals)
    {
        var sessions = source.Select(session => session with
        {
            DurationMinutes = session.OccurrencePrescription is not null &&
                originals.TryGetValue(session.Activity, out var original) && original is not null
                ? SameDayDemandAdjustmentRule.TotalMinutes(original) : session.DurationMinutes,
            OccurrencePrescription = null
        }).ToArray();
        foreach (var day in sessions.Where(session => session.IsHardSession).GroupBy(session => session.Day))
        {
            if (day.Count() < MinimumHardSessionsForAdjustment) continue;
            foreach (var session in day)
            {
                if (!originals.TryGetValue(session.Activity, out var original)) continue;
                var adjusted = SameDayDemandAdjustmentRule.Adjust(original);
                if (adjusted is null) continue;
                var index = Array.IndexOf(sessions, session);
                sessions[index] = session with
                {
                    DurationMinutes = SameDayDemandAdjustmentRule.TotalMinutes(adjusted),
                    OccurrencePrescription = adjusted
                };
                break;
            }
        }
        return sessions;
    }

    /// <summary>Sets the length of the week's cardio-only days.</summary>
    private static MixedActivityScheduledSession[] ApplyCardioOnlyDayDurations(
        MixedActivityScheduledSession[] sessions, IReadOnlyCollection<ActivityDecisionBuilder> decisions)
    {
        var experienceByActivity = decisions
            .Where(decision => decision.Experience is not null)
            .GroupBy(decision => decision.EffectiveActivity ?? decision.Activity)
            .ToDictionary(group => group.Key, group => group.First().Experience!.Value);
        return sessions.Select(session => experienceByActivity.TryGetValue(session.Activity, out var experience)
            ? ApplyCardioDurationLimits(session, sessions, experience)
            : session).ToArray();
    }

    /// <summary>Caps a cardio session at the length its activity allows.</summary>
    private static MixedActivityScheduledSession ApplyCardioDurationLimits(
        MixedActivityScheduledSession session, IReadOnlyList<MixedActivityScheduledSession> sessions,
        ActivityExperience experience)
    {
        var isContinuousCardio = IsContinuousCardio(session.Activity);
        var isRecovery = IsRecoveryActivity(session.Activity);
        if (!isContinuousCardio && !isRecovery) return session;

        var daySessions = sessions.Where(candidate => candidate.Day == session.Day).ToArray();
        var hasStrengthOnDay = daySessions.Any(candidate => IsStrengthChoice(candidate.Activity));
        if (isRecovery && !hasStrengthOnDay) return session;

        var duration = session.DurationMinutes;
        var isStandaloneCardioDay = isContinuousCardio && !hasStrengthOnDay &&
            daySessions.Count(candidate => IsContinuousCardio(candidate.Activity)) == OneSessionPerOccurrence;
        if (isStandaloneCardioDay)
            duration = Math.Max(duration,
                CardioSessionDurationRule.StandaloneDayMinutes(session.Activity, experience));

        // Owner rule: work that follows a strength session never runs past 30 minutes, while cardio
        // without a strength session is only bounded by its modality ceiling.
        var maximum = isContinuousCardio
            ? CardioSessionDurationRule.MaximumSessionMinutes(session.Activity)
            : CardioSessionDurationRule.PostStrengthCardioOrRecoveryMaximumMinutes;
        if (hasStrengthOnDay)
            maximum = Math.Min(maximum, CardioSessionDurationRule.PostStrengthCardioOrRecoveryMaximumMinutes);

        duration = Math.Min(duration, maximum);
        return duration == session.DurationMinutes ? session : session with { DurationMinutes = duration };
    }

    /// <summary>True when the activity is continuous cardio.</summary>
    private static bool IsContinuousCardio(WorkoutActivityType activity) => activity is
        WorkoutActivityType.Cycling or WorkoutActivityType.Running or WorkoutActivityType.Walking or
        WorkoutActivityType.Rowing or WorkoutActivityType.Elliptical or WorkoutActivityType.Swimming;

    /// <summary>True when the activity is recovery work rather than training.</summary>
    private static bool IsRecoveryActivity(WorkoutActivityType activity) => activity is
        WorkoutActivityType.Yoga or WorkoutActivityType.Mobility or WorkoutActivityType.GentleRecovery;

    /// <summary>Fills the decision builder for one selected activity.</summary>
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

        decision.Experience = evidence.Experience;

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

    /// <summary>Reads the weekly frequency the user asked for, when they gave one.</summary>
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

    /// <summary>The training evidence for an activity when exactly one record exists.</summary>
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

    /// <summary>Strength frequency policy for the profile's strength history.</summary>
    private static GeneralStrengthFrequencyPolicy ResolveGeneralStrengthPolicy(
        ActivityPrescriptionContext? evidence, int availableDayCount)
    {
        var profileLevel = evidence?.Experience == ActivityExperience.Established
            ? TrainingExperienceLevel.Intermediate
            : TrainingExperienceLevel.Beginner;
        return GeneralStrengthFrequencyResolver.Resolve(
            availableDayCount, evidence?.IsEstablishedResistanceHistory == true, profileLevel);
    }

    /// <summary>Activities that carry more than one evidence record.</summary>
    private static HashSet<WorkoutActivityType> FindDuplicateEvidenceActivities(
        IReadOnlyList<ActivityPrescriptionContext>? evidence) =>
        evidence?
            .Where(candidate => candidate is not null)
            .GroupBy(candidate => candidate.Activity)
            .Where(group => group.Count() > OneSessionPerOccurrence)
            .Select(group => group.Key)
            .ToHashSet() ?? [];

    /// <summary>Replaces the underlying cardio session with its HIIT prescription.</summary>
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

    /// <summary>Writes the placed sessions onto their days, keeping hard sessions apart.</summary>
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

    /// <summary>Lowers the weekly targets to the days she actually has, instead of over-scheduling.</summary>
    private static void ReduceTargetsToFitAvailability(ActivityDecisionBuilder[] decisions, int availableDayCount)
    {
        var candidates = decisions.Where(IsScheduleCandidate).ToArray();
        foreach (var decision in candidates)
        {
            if (!IsStrengthChoice(decision.EffectiveActivity ?? decision.Activity))
                continue;

            if (availableDayCount == OneSessionPerOccurrence &&
                decision.TargetSessionsPerWeek > OneSessionPerOccurrence)
            {
                decision.TargetSessionsPerWeek = OneSessionPerOccurrence;
                decision.AddReason(ActivityFrequencyLimitedByAvailableWeekdays);
            }
            else if (availableDayCount > OneSessionPerOccurrence &&
                     decision.TargetSessionsPerWeek < GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek)
            {
                decision.TargetSessionsPerWeek = GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek;
            }
        }

        var hardCandidates = candidates.Where(decision =>
            IsHardActivity(decision.EffectiveActivity ?? decision.Activity, decision.Prescription)).ToArray();
        ReduceTargets(hardCandidates, Math.Min(MaximumHardDaysPerWeek, availableDayCount) * MaximumSessionsPerDay);

        // Every non-hard activity can share a day with another distinct activity.
        // Placement still enforces the per-day and duplicate-activity constraints.
        var hardSessionCount = hardCandidates.Sum(GetTargetSessions);
        var softCandidates = candidates.Where(decision =>
            !IsHardActivity(decision.EffectiveActivity ?? decision.Activity, decision.Prescription)).ToArray();
        var remainingSlots = Math.Max(NoSessionsScheduled,
            availableDayCount * MaximumSessionsPerDay - hardSessionCount);
        ReduceTargets(softCandidates, remainingSlots);
    }

    /// <summary>Sessions per week the decision targets.</summary>
    private static int GetTargetSessions(ActivityDecisionBuilder decision) =>
        decision.TargetSessionsPerWeek ?? NoSessionsScheduled;

    /// <summary>Whether a session may share a day with strength work without breaking the spacing rule.</summary>
    private static bool CanBeStrengthCompanion(ActivityDecisionBuilder decision)
    {
        var activity = decision.EffectiveActivity ?? decision.Activity;
        if (IsHardActivity(activity, decision.Prescription))
            return false;

        return activity is WorkoutActivityType.Cycling or WorkoutActivityType.Yoga;
    }

    /// <summary>Cuts the targets in a fixed order, so the reduction is predictable between runs.</summary>
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

    /// <summary>Fewest sessions the activity can be reduced to.</summary>
    private static int MinimumSessionsForReduction(ActivityDecisionBuilder decision) =>
        IsStrengthChoice(decision.EffectiveActivity ?? decision.Activity)
            ? GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek
            : MinimumRequestedSessionsPerWeek;

    /// <summary>Places the activity's sessions on the available weekdays.</summary>
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
            var candidates = availableDays.Where(day =>
            {
                var state = dayStates[day];
                if (state.Activities.Contains(decision.Activity)) return false;
                if (isHard && !state.HasHardSession && hardDays.Length >= MaximumHardDaysPerWeek)
                    return false;
                return state.Activities.Count == NoSessionsScheduled ||
                       CanShareDay(state, decision, isHard);
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

            dayStates[day].Add(decision, isHard);
            decision.ScheduledDays.Add(day);
        }

        if (decision.ScheduledDays.Count < target && decision.ScheduledDays.Count == availableDays.Length)
            decision.AddReason(ActivityFrequencyLimitedByAvailableWeekdays);
    }

    /// <summary>The gap between hard days measured around the week, so a Sunday/Monday pair counts as close.</summary>
    private static int MinimumCircularDistance(DayOfWeek day, IReadOnlyCollection<DayOfWeek> occupiedDays)
    {
        if (occupiedDays.Count == NoSessionsScheduled) return MissingDistance;
        return occupiedDays.Min(occupiedDay =>
        {
            var directDistance = Math.Abs(DayIndex(day) - DayIndex(occupiedDay));
            return Math.Min(directDistance, DaysPerWeek - directDistance);
        });
    }

    /// <summary>Orders the available days and removes duplicates.</summary>
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

    /// <summary>True when the decision still needs sessions scheduled.</summary>
    private static bool IsScheduleCandidate(ActivityDecisionBuilder decision) =>
        decision.IsEligible && decision.TargetSessionsPerWeek is not null;

    /// <summary>True when the strength activity is dropped from the week.</summary>
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

    /// <summary>True when the activity is a strength choice.</summary>
    private static bool IsStrengthChoice(WorkoutActivityType activity) => activity is
        WorkoutActivityType.GeneralStrength or WorkoutActivityType.HighVolumeStrength or
        WorkoutActivityType.StrengthHighIntensity;

    /// <summary>Whether the activity counts as hard for spacing.</summary>
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

    /// <summary>True when the activity may share a day with a strength session.</summary>
    public static bool IsSupportedSameDayStrengthCompanion(
        WorkoutActivityType firstActivity,
        bool firstIsHard,
        WorkoutActivityType secondActivity,
        bool secondIsHard) =>
        (IsStrengthChoice(firstActivity) && firstIsHard && IsStrengthCompanion(secondActivity, secondIsHard)) ||
        (IsStrengthChoice(secondActivity) && secondIsHard && IsStrengthCompanion(firstActivity, firstIsHard));

    /// <summary>True when both activities can run on the same day.</summary>
    private static bool CanShareDay(
        ScheduledDayState dayState,
        ActivityDecisionBuilder decision,
        bool isHard)
    {
        if (dayState.Activities.Count >= MaximumSessionsPerDay)
            return false;

        var incoming = decision.EffectiveActivity ?? decision.Activity;
        if (dayState.Activities.Contains(incoming))
            return false;
        if (IsStrengthChoice(incoming) && dayState.Activities.Any(IsStrengthChoice))
            return false;
        // Same exclusion WorkoutScheduleValidationRule enforces later: HIIT never shares a day with
        // strength or climbing, so placement moves HIIT to another day or reduces it instead of
        // producing a schedule the validator would reject.
        if (incoming == WorkoutActivityType.Hiit && dayState.Activities.Any(IsStrengthOrClimbing) ||
            IsStrengthOrClimbing(incoming) && dayState.Activities.Contains(WorkoutActivityType.Hiit))
            return false;
        if (!isHard)
            return true;
        if (dayState.HardSessionCount >= MaximumHardSessionsOnAdjustedDay)
            return false;
        // Two hard sessions may share a day at full dose; a third needs one of them reduced.
        if (dayState.HardSessionCount < MaximumHardSessionsPerDay)
            return true;
        return SameDayDemandAdjustmentRule.Adjust(dayState.Decision!.Prescription) is not null ||
               SameDayDemandAdjustmentRule.Adjust(decision.Prescription) is not null;
    }

    /// <summary>True when the activity loads the body like a strength session.</summary>
    private static bool IsStrengthOrClimbing(WorkoutActivityType activity) =>
        IsStrengthChoice(activity) || activity == WorkoutActivityType.RockClimbing;

    /// <summary>True when the activity counts as a companion to strength work.</summary>
    private static bool IsStrengthCompanion(WorkoutActivityType activity, bool isHard) =>
        !isHard && !IsStrengthChoice(activity) && activity != WorkoutActivityType.RestDay;

    /// <summary>Why the activity cannot share a hard day.</summary>
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

    /// <summary>The fixed order hard activities are placed in, so the schedule is stable.</summary>
    private static int HardActivityOrder(WorkoutActivityType activity)
    {
        var index = Array.IndexOf(HardActivityPriority, activity);
        return index == MissingHardPriorityIndex ? HardActivityPriority.Length : index;
    }

    /// <summary>Stable ordering value for an activity.</summary>
    private static int ActivityOrder(WorkoutActivityType activity) => (int)activity;

    /// <summary>Index of the day inside the training week.</summary>
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

    /// <summary>One day's accumulated placements, used while the week is built.</summary>
    private sealed class ScheduledDayState
    {
        /// <summary>Whether a hard session already sits on this day.</summary>
        public bool HasHardSession { get; private set; }
        /// <summary>How many hard sessions the day holds, which the spacing rule limits.</summary>
        public int HardSessionCount { get; private set; }
        /// <summary>Activities the decision covers.</summary>
        public HashSet<WorkoutActivityType> Activities { get; } = [];
        /// <summary>The activity being placed on this day, absent for a rest day.</summary>
        public ActivityDecisionBuilder? Decision { get; private set; }

        /// <summary>Records a placement and marks the day hard when the activity is.</summary>
        public void Add(ActivityDecisionBuilder decision, bool isHard)
        {
            Activities.Add(decision.EffectiveActivity ?? decision.Activity);
            Decision ??= decision;
            HasHardSession |= isHard;
            if (isHard) HardSessionCount++;
        }
    }

    /// <summary>Builds one activity's decision while the week is arranged.</summary>
    private sealed class ActivityDecisionBuilder(WorkoutActivityType activity)
    {
        private readonly List<string> _reasons = [];

        /// <summary>Activity this decision is about.</summary>
        public WorkoutActivityType Activity { get; } = activity;
        /// <summary>The status the decision carries once the prescription is known.</summary>
        public ActivityPrescriptionStatus? PrescriptionStatus { get; set; }
        /// <summary>The prescription attached to the session, when one was resolved.</summary>
        public ActivitySessionPrescription? Prescription { get; set; }
        /// <summary>The count the policy allows, which bounds her target.</summary>
        public int? PolicySessionsPerWeek { get; set; }
        /// <summary>The count she asked for, which the week tries to place.</summary>
        public int? TargetSessionsPerWeek { get; set; }
        /// <summary>The ceiling the week may not exceed, whatever she asked for.</summary>
        public int? MaxSessionsPerWeek { get; set; }
        /// <summary>The minutes each session of this activity takes.</summary>
        public decimal DurationMinutes { get; set; }
        /// <summary>The experience the activity's allowance was resolved at.</summary>
        public ActivityExperience? Experience { get; set; }
        /// <summary>Whether the activity cleared eligibility at all.</summary>
        public bool IsEligible { get; set; }
        /// <summary>The activity actually scheduled, after a fallback replaced it.</summary>
        public WorkoutActivityType? EffectiveActivity { get; set; }
        /// <summary>The code explaining the fallback, so the substitution is visible to her.</summary>
        public string? FallbackReasonCode { get; set; }
        /// <summary>Weekdays the activity's sessions are placed on.</summary>
        public List<DayOfWeek> ScheduledDays { get; } = [];
        /// <summary>A disposition forced by a later rule, overriding the placement outcome.</summary>
        public MixedActivityScheduleDisposition? DispositionOverride { get; set; }

        /// <summary>Marks the activity blocked with its reason, withholding it from the week.</summary>
        public void Block(string reason)
        {
            IsEligible = false;
            DispositionOverride = MixedActivityScheduleDisposition.Blocked;
            AddReason(reason);
        }

        /// <summary>Marks the activity omitted with its reason, for one that fits neither.</summary>
        public void Omit(string reason)
        {
            IsEligible = false;
            DispositionOverride = MixedActivityScheduleDisposition.Omitted;
            AddReason(reason);
        }

        /// <summary>Attaches one reason, keeping duplicates out.</summary>
        public void AddReason(string reason)
        {
            if (!_reasons.Contains(reason, StringComparer.Ordinal)) _reasons.Add(reason);
        }

        /// <summary>Attaches several reasons at once.</summary>
        public void AddReasons(IEnumerable<string> reasons)
        {
            foreach (var reason in reasons) AddReason(reason);
        }

        /// <summary>Freezes the builder into the decision the result carries.</summary>
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
