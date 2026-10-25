using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum StrengthPlanPreviewStatus
{
    Feasible,
    Limited,
    InvalidInput
}

public sealed record StrengthPlanPreviewChoices(
    WorkoutActivityType Activity,
    DateOnly LocalDate,
    ActivityTrainingHistory? ActivityHistory,
    IReadOnlySet<int>? ExcludedExerciseIds = null,
    IReadOnlySet<int>? UserConfirmedFamiliarExerciseIds = null,
    IReadOnlySet<int>? AcknowledgedExerciseConditionIds = null,
    bool UnknownExperienceAssessmentCompleted = false,
    IReadOnlySet<int>? UserConfirmedManageableLoadExerciseIds = null,
    IReadOnlySet<string>? AcknowledgedSubstitutionPairKeys = null,
    int? WarmupMinutes = null,
    int? CooldownMinutes = null,
    IReadOnlySet<DayOfWeek>? RequestedAvailableWeekdays = null);

/// <summary>Trusted server profile, activity preferences, and reviewed catalog snapshot used by the planner.</summary>
public sealed record StrengthPlanPreviewData(
    UserGoal Goal,
    Equipment Equipment,
    InjuryFlag ActiveInjuryFlags,
    bool HasCurrentSymptoms,
    IReadOnlySet<WorkoutActivityType>? SelectedActivities,
    IReadOnlySet<DayOfWeek>? AvailableWeekdays,
    IReadOnlyDictionary<WorkoutActivityType, int>? ActivitySessionDurationsMinutes,
    TrainingExperienceLevel? ProfileTrainingExperience,
    IReadOnlyList<StrengthExerciseCatalogEntry>? Catalog,
    IReadOnlyDictionary<int, int>? PreferredExerciseByOriginalId,
    IReadOnlySet<int>? AcceptedExerciseIds,
    IReadOnlyDictionary<int, int>? CompletedExerciseExposureCounts,
    IReadOnlySet<int>? KnownManageableLoadExerciseIds,
    IReadOnlyDictionary<int, int>? SecondsPerRepByExerciseId,
    IReadOnlyDictionary<int, string>? PrimaryMuscleByExerciseId,
    IReadOnlyDictionary<string, IReadOnlySet<int>>? ReviewedAllowedExerciseIdsByTemplateSlot,
    IReadOnlyDictionary<string, string>? TemplateSlotReviewConditions)
{
    public IReadOnlyDictionary<int, int>? GoalPriorityByExerciseId { get; init; }
}

public sealed record StrengthPlanPreviewSession(
    string TemplateId,
    string TemplateVersion,
    int SessionIndex,
    decimal? EstimatedMinutes,
    IReadOnlyList<StrengthSlotSelection> Slots,
    IReadOnlyList<string> LimitationCodes,
    IReadOnlyDictionary<string, string> SlotReviewConditions,
    DayOfWeek? PlannedWeekday = null);

public sealed record StrengthExerciseReviewCondition(
    int ExerciseId,
    string ExerciseName,
    string ExperienceConditions,
    string GeneralConditions);

public sealed record StrengthPlanPreviewResult(
    StrengthPlanPreviewStatus Status,
    ActivityExperience Experience,
    IReadOnlyList<StrengthPlanPreviewSession> Sessions,
    IReadOnlyList<string> LimitationCodes,
    IReadOnlyList<StrengthExerciseReviewCondition> ExerciseReviewConditions,
    IReadOnlyList<string> AdvisoryCodes,
    WorkoutActivityType? EffectiveActivity = null,
    string? FallbackSource = null,
    string? FallbackReasonCode = null);

/// <summary>
/// Evaluates a strength proposal as one Core decision using activity policy, reviewed templates,
/// exercise eligibility, selected activity duration, and shared weekday availability.
/// </summary>
public static class StrengthPlanPreviewRule
{
    public const string PolicyId = "MC-WORKOUT-POLICY";
    public const string Version = "0.2.0";
    public const string RulesetVersion = "MC-PLANNING-0.2";
    public const string InvalidInputCode = "InvalidStrengthPreviewInput";
    public const string PlanningProfileInvalidCode = "PlanningProfileDataInvalid";
    public const string ActivityNotSelectedCode = "ActivityNotSelectedInProfile";
    public const string WeekdaysMissingCode = "AvailableWeekdaysMissing";
    public const string SessionDurationMissingCode = "SessionDurationMissing";
    public const string SessionDurationInvalidCode = "SelectedActivityDurationInvalid";
    public const string ExperienceHistoryMissingCode = "ActivityExperienceHistoryMissing";
    public const string BeginnerProfileRequiresGeneralStrengthCode =
        "BeginnerProfileRequiresGeneralStrength";
    public const string HighVolumeRequiresEstablishedResistanceHistoryCode =
        "HighVolumeRequiresEstablishedResistanceHistory";
    public const string GeneralStrengthFallbackSource =
        "GeneralStrengthFallback";
    public const string SymptomsNeedReviewCode = "RestrictionOrSymptomRequiresReview";
    public const string MissingWarmupOrCooldownCode = "StrengthWarmupOrCooldownDurationMissing";

    private const int NoMinutes = 0;
    private const int FirstPositiveIdentifier = 1;
    private const int OneSession = 1;
    private const int NoSessions = 0;
    private const int NoCompletedExposures = 0;
    private const int NoSetupMinutes = 0;
    private const int NoSecondsPerRep = 0;
    private const int SecondsPerMinute = 60;
    private const int NoDirectSets = 0;
    private const int UnilateralTimeMultiplier = 2;
    private const int StandardTimeMultiplier = 1;
    private const string MissingTemplateSlotReview = "Template slot review missing";
    private static readonly IReadOnlyList<string> NoCodes = Array.Empty<string>();
    private static readonly IReadOnlySet<int> EmptyIds = new HashSet<int>();

    public static StrengthPlanPreviewResult Evaluate(
        StrengthPlanPreviewChoices? choices,
        StrengthPlanPreviewData? data) =>
        EvaluateWithFallback(choices, data);

    public static StrengthPlanPreviewResult EvaluateDirect(
        StrengthPlanPreviewChoices? choices,
        StrengthPlanPreviewData? data)
    {
        if (!ValidChoices(choices))
            return Failure(StrengthPlanPreviewStatus.InvalidInput, ActivityExperience.Unknown,
                InvalidInputCode);
        if (data is null || !ValidProfileData(data))
            return Failure(StrengthPlanPreviewStatus.InvalidInput, ActivityExperience.Unknown,
                PlanningProfileInvalidCode);

        var activity = choices!.Activity;
        if (data.SelectedActivities is null || !data.SelectedActivities.Contains(activity))
            return Failure(StrengthPlanPreviewStatus.Limited, ActivityExperience.Unknown,
                ActivityNotSelectedCode);

        if (data.ActivitySessionDurationsMinutes is null)
            return Failure(StrengthPlanPreviewStatus.Limited, ActivityExperience.Unknown,
                SessionDurationMissingCode);
        if (data.ActivitySessionDurationsMinutes.Any(pair => !Enum.IsDefined(pair.Key) ||
                pair.Key == activity && !IsValidActivityDuration(pair.Key, pair.Value)))
            return Failure(StrengthPlanPreviewStatus.InvalidInput, ActivityExperience.Unknown,
                SessionDurationInvalidCode);

        var weekdays = data.AvailableWeekdays ?? choices.RequestedAvailableWeekdays;
        if (!ValidWeekdays(weekdays))
            return Failure(StrengthPlanPreviewStatus.Limited, ActivityExperience.Unknown,
                WeekdaysMissingCode);
        if (choices.RequestedAvailableWeekdays is not null && data.AvailableWeekdays is not null &&
            !choices.RequestedAvailableWeekdays.SetEquals(data.AvailableWeekdays))
            return Failure(StrengthPlanPreviewStatus.Limited, ActivityExperience.Unknown,
                "SavedWorkoutAvailabilityChanged");

        if (choices.WarmupMinutes is null || choices.CooldownMinutes is null)
            return Failure(StrengthPlanPreviewStatus.Limited, ActivityExperience.Unknown,
                MissingWarmupOrCooldownCode);

        if (!data.ActivitySessionDurationsMinutes!.TryGetValue(activity, out var sessionMinutes))
            return Failure(StrengthPlanPreviewStatus.Limited, ActivityExperience.Unknown,
                SessionDurationMissingCode);
        if (!IsValidActivityDuration(activity, sessionMinutes))
            return Failure(StrengthPlanPreviewStatus.InvalidInput, ActivityExperience.Unknown,
                SessionDurationInvalidCode);

        var experience = ActivityExperienceRule.Classify(activity.ToString(), choices.ActivityHistory,
            choices.LocalDate).Experience;

        if (data.ProfileTrainingExperience == TrainingExperienceLevel.Beginner &&
            activity is WorkoutActivityType.HighVolumeStrength or WorkoutActivityType.StrengthHighIntensity)
        {
            return Failure(StrengthPlanPreviewStatus.Limited, experience,
                BeginnerProfileRequiresGeneralStrengthCode);
        }

        if (activity == WorkoutActivityType.HighVolumeStrength)
        {
            var isEstablishedResistance = GeneralStrengthFrequencyResolver.IsEstablishedResistanceHistory(
                choices.ActivityHistory, choices.LocalDate);

            if (!isEstablishedResistance || experience != ActivityExperience.Established)
            {
                return Failure(StrengthPlanPreviewStatus.Limited, experience,
                    HighVolumeRequiresEstablishedResistanceHistoryCode);
            }
        }
        else if (activity != WorkoutActivityType.GeneralStrength &&
                 experience == ActivityExperience.Unknown &&
                 !choices.UnknownExperienceAssessmentCompleted)
        {
            return Failure(StrengthPlanPreviewStatus.Limited, experience, ExperienceHistoryMissingCode);
        }

        var profileExperienceDecision = ProfileTrainingExperiencePlanningRule.Resolve(
            experience, data.ProfileTrainingExperience, choices.ActivityHistory,
            allowProfileFallbackWhenHistoryMissing: true);
        var prescriptionExperience = profileExperienceDecision.PrescriptionExperience;

        if (activity == WorkoutActivityType.GeneralStrength)
        {
            if (prescriptionExperience == ActivityExperience.Unknown)
                prescriptionExperience = ActivityExperience.Beginner;
            if (experience == ActivityExperience.Unknown)
                experience = ActivityExperience.Beginner;
        }

        if (profileExperienceDecision.UsesConservativeProfileCap &&
            activity is WorkoutActivityType.HighVolumeStrength or WorkoutActivityType.StrengthHighIntensity)
            return Failure(StrengthPlanPreviewStatus.Limited, experience,
                BeginnerProfileRequiresGeneralStrengthCode);

        var availableEquipment = EquipmentFor(data.Equipment);
        var eligibilityContext = new ExerciseEligibilityContext(availableEquipment, prescriptionExperience,
            choices.ExcludedExerciseIds ?? EmptyIds, new HashSet<MovementPattern>(),
            new HashSet<ActivityDemandTag>(), false, data.ActiveInjuryFlags,
            choices.AcknowledgedExerciseConditionIds,
            choices.UnknownExperienceAssessmentCompleted, choices.AcknowledgedSubstitutionPairKeys);
        var isEstablishedResistanceHistory = GeneralStrengthFrequencyResolver.IsEstablishedResistanceHistory(
            choices.ActivityHistory, choices.LocalDate);
        var prescriptionContext = new ActivityPrescriptionContext(activity, prescriptionExperience, null,
            data.HasCurrentSymptoms, true, isEstablishedResistanceHistory,
            NoCompletedExposures, false, false);
        var scheduledSessionsPerWeek = ScheduledSessionsPerWeekFor(activity, data, choices);
        var templates = StrengthTemplateCatalog.ForSchedule(scheduledSessionsPerWeek);
        if (templates.Count == NoSessions)
            return Failure(StrengthPlanPreviewStatus.InvalidInput, experience, InvalidInputCode);

        var sessions = new List<StrengthPlanPreviewSession>(templates.Count);
        var weeklySetsByMuscle = new Dictionary<string, int>(StringComparer.Ordinal);
        var limitations = new List<string>();

        foreach (var template in templates)
        {
            var resolution = StrengthTemplateResolver.Resolve(
                new StrengthTemplateSelectionInput(template, activity, data.Goal,
                    prescriptionContext, data.Catalog!, eligibilityContext,
                    data.PreferredExerciseByOriginalId!,
                    data.CompletedExerciseExposureCounts!.Keys.ToHashSet(),
                    data.CompletedExerciseExposureCounts.Keys.ToHashSet(),
                    data.AcceptedExerciseIds,
                    CompletedExerciseExposureCounts: data.CompletedExerciseExposureCounts,
                    KnownManageableLoadExerciseIds: data.KnownManageableLoadExerciseIds,
                    UserConfirmedFamiliarityExerciseIds:
                        choices.UserConfirmedFamiliarExerciseIds ?? EmptyIds,
                    ReviewedAllowedExerciseIdsByTemplateSlot: data.ReviewedAllowedExerciseIdsByTemplateSlot,
                    UserConfirmedManageableLoadExerciseIds:
                        choices.UserConfirmedManageableLoadExerciseIds ?? EmptyIds,
                    GoalPriorityByExerciseId: data.GoalPriorityByExerciseId),
                MaximumWorkingExercises(activity, template));

            if (resolution.Status != "Resolved")
            {
                limitations.AddRange(resolution.LimitationCodes);
                sessions.Add(new StrengthPlanPreviewSession(template.TemplateId, template.Version,
                    template.SessionIndex, null, resolution.Slots, resolution.LimitationCodes,
                    SlotConditions(template, data)));
                continue;
            }

            var selected = resolution.Slots.Where(slot => slot.ExerciseId.HasValue).ToList();
            var estimatedMinutes = EstimateAndTrimOptional(selected, data, sessionMinutes,
                choices.WarmupMinutes!.Value, choices.CooldownMinutes!.Value);

            var applyDirectSetCeiling = activity == WorkoutActivityType.HighVolumeStrength ||
                activity == WorkoutActivityType.GeneralStrength &&
                prescriptionExperience == ActivityExperience.Beginner;
            if (applyDirectSetCeiling)
            {
                var directSetCeiling = activity == WorkoutActivityType.GeneralStrength
                    ? WeeklyHypertrophyVolumeRule.Tier1HardCapSets
                    : data.ProfileTrainingExperience is null or TrainingExperienceLevel.Unknown
                        ? ActivityPrescriptionRule.HighVolumeInitialDirectSetsPerPrimaryMusclePerWeek
                        : profileExperienceDecision.MaximumWeeklySetsPerMuscle;
                var provisionalSetsByMuscle = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var slot in selected.ToArray())
                {
                    var exerciseId = slot.ExerciseId!.Value;
                    if (!data.PrimaryMuscleByExerciseId!.TryGetValue(exerciseId, out var muscle) ||
                        string.IsNullOrWhiteSpace(muscle))
                    {
                        limitations.Add($"PrimaryMuscleMissing:{exerciseId}");
                        continue;
                    }

                    var projectedSets = weeklySetsByMuscle.GetValueOrDefault(muscle) +
                                        provisionalSetsByMuscle.GetValueOrDefault(muscle) +
                                        slot.Sets.GetValueOrDefault();
                    if (projectedSets > directSetCeiling &&
                        !slot.IsRequired)
                    {
                        selected.Remove(slot);
                        continue;
                    }

                    provisionalSetsByMuscle[muscle] = provisionalSetsByMuscle.GetValueOrDefault(muscle) +
                                                  slot.Sets.GetValueOrDefault();
                    if (projectedSets > directSetCeiling)
                        limitations.Add($"DirectSetCeilingExceeded:{muscle}");
                }

                estimatedMinutes = EstimateAndTrimOptional(selected, data, sessionMinutes,
                    choices.WarmupMinutes.Value, choices.CooldownMinutes.Value);
                if (estimatedMinutes.HasValue)
                    foreach (var slot in selected)
                    {
                        var exerciseId = slot.ExerciseId!.Value;
                        if (data.PrimaryMuscleByExerciseId!.TryGetValue(exerciseId, out var muscle) &&
                            !string.IsNullOrWhiteSpace(muscle))
                            weeklySetsByMuscle[muscle] = weeklySetsByMuscle.GetValueOrDefault(muscle) +
                                slot.Sets.GetValueOrDefault();
                    }
            }

            IReadOnlyList<string> sessionLimitations = estimatedMinutes.HasValue
                ? NoCodes
                : ["RequiredMovementWorkExceedsSessionDurationOrTimingMissing"];
            if (!estimatedMinutes.HasValue)
                limitations.AddRange(sessionLimitations);
            sessions.Add(new StrengthPlanPreviewSession(template.TemplateId, template.Version,
                template.SessionIndex, estimatedMinutes, selected, sessionLimitations,
                SlotConditions(template, data)));
        }

        var assignments = StrengthWeekdayAssignmentRule.Assign(sessions.Count, weekdays!);
        if (assignments.Status != StrengthWeekdayAssignmentStatus.Assigned)
            limitations.Add(assignments.LimitationCode ?? "StrengthWeekdayAssignmentUnavailable");
        else
        {
            var weekdayBySession = assignments.Assignments.ToDictionary(item => item.SessionIndex,
                item => item.Weekday);
            for (var index = NoSessions; index < sessions.Count; index++)
                sessions[index] = sessions[index] with { PlannedWeekday = weekdayBySession[index] };
        }

        var limitationCodes = limitations.Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var reviewConditions = ExerciseReviewConditions(templates, data);
        var advisories = new List<string>(profileExperienceDecision.ReasonCodes);
        if (scheduledSessionsPerWeek == OneSession)
            advisories.Add("OneDayFullBodyHasLimitedWeeklyCoverage");
        if (activity == WorkoutActivityType.GeneralStrength &&
            (data.ProfileTrainingExperience == TrainingExperienceLevel.Beginner ||
             profileExperienceDecision.EffectiveExperienceLevel == TrainingExperienceLevel.Beginner ||
             prescriptionExperience == ActivityExperience.Beginner))
        {
            advisories.Add("DirectBeginnerGeneralStrengthSettingsApplied");
        }
        return new StrengthPlanPreviewResult(
            limitationCodes.Length == NoSessions
                ? StrengthPlanPreviewStatus.Feasible : StrengthPlanPreviewStatus.Limited,
            experience, sessions, limitationCodes, reviewConditions,
            advisories.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            activity);
    }

    private static bool ValidChoices(StrengthPlanPreviewChoices? choices) =>
        choices is not null && Enum.IsDefined(choices.Activity) &&
        (choices.Activity is WorkoutActivityType.GeneralStrength or WorkoutActivityType.HighVolumeStrength or
            WorkoutActivityType.StrengthHighIntensity) &&
        choices.LocalDate != default &&
        (choices.ExcludedExerciseIds is null || choices.ExcludedExerciseIds.All(IsPositiveIdentifier)) &&
        (choices.UserConfirmedFamiliarExerciseIds is null ||
         choices.UserConfirmedFamiliarExerciseIds.All(IsPositiveIdentifier)) &&
        (choices.AcknowledgedExerciseConditionIds is null ||
         choices.AcknowledgedExerciseConditionIds.All(IsPositiveIdentifier)) &&
        (choices.UserConfirmedManageableLoadExerciseIds is null ||
         choices.UserConfirmedManageableLoadExerciseIds.All(IsPositiveIdentifier)) &&
        (choices.AcknowledgedSubstitutionPairKeys is null ||
         choices.AcknowledgedSubstitutionPairKeys.All(key => !string.IsNullOrWhiteSpace(key))) &&
        (choices.WarmupMinutes is null or >= NoMinutes) &&
        (choices.CooldownMinutes is null or >= NoMinutes) &&
        (choices.RequestedAvailableWeekdays is null || ValidWeekdays(choices.RequestedAvailableWeekdays));

    private static bool ValidProfileData(StrengthPlanPreviewData data) =>
        Enum.IsDefined(data.Goal) && Enum.IsDefined(data.Equipment) &&
        (data.ProfileTrainingExperience is null || Enum.IsDefined(data.ProfileTrainingExperience.Value)) &&
        (data.SelectedActivities is null || data.SelectedActivities.All(activity => Enum.IsDefined(activity))) &&
        data.Catalog is not null && data.Catalog.All(entry => entry is not null && entry.Candidate is not null) &&
        data.Catalog.Select(entry => entry.Candidate.ExerciseId).Distinct().Count() == data.Catalog.Count &&
        data.PreferredExerciseByOriginalId is not null &&
        data.PreferredExerciseByOriginalId.All(pair => IsPositiveIdentifier(pair.Key) &&
                                                        IsPositiveIdentifier(pair.Value)) &&
        data.AcceptedExerciseIds is not null && data.AcceptedExerciseIds.All(IsPositiveIdentifier) &&
        data.CompletedExerciseExposureCounts is not null &&
        data.CompletedExerciseExposureCounts.All(pair => IsPositiveIdentifier(pair.Key) && pair.Value >= NoCompletedExposures) &&
        data.KnownManageableLoadExerciseIds is not null &&
        data.KnownManageableLoadExerciseIds.All(IsPositiveIdentifier) &&
        data.SecondsPerRepByExerciseId is not null &&
        data.SecondsPerRepByExerciseId.All(pair => IsPositiveIdentifier(pair.Key) && pair.Value > NoSecondsPerRep) &&
        data.PrimaryMuscleByExerciseId is not null &&
        data.PrimaryMuscleByExerciseId.All(pair => IsPositiveIdentifier(pair.Key) &&
                                                     !string.IsNullOrWhiteSpace(pair.Value)) &&
        data.ReviewedAllowedExerciseIdsByTemplateSlot is not null &&
        data.ReviewedAllowedExerciseIdsByTemplateSlot.All(pair => !string.IsNullOrWhiteSpace(pair.Key) &&
            pair.Value is not null && pair.Value.All(IsPositiveIdentifier)) &&
        data.TemplateSlotReviewConditions is not null &&
        data.TemplateSlotReviewConditions.All(pair => !string.IsNullOrWhiteSpace(pair.Key) &&
                                                       !string.IsNullOrWhiteSpace(pair.Value));

    private static bool ValidWeekdays(IReadOnlySet<DayOfWeek>? weekdays) =>
        weekdays is { Count: >= MixedActivityWeekScheduleRule.MinimumAvailableWeekdays and
            <= WorkoutPreferencesRule.MaximumDaysPerWeek } && weekdays.All(Enum.IsDefined);

    private static bool IsValidActivityDuration(WorkoutActivityType activity, int minutes) =>
        WorkoutPreferencesRule.IsValidSessionDuration(activity, minutes);

    private static bool IsPositiveIdentifier(int value) => value >= FirstPositiveIdentifier;

    private static int ScheduledSessionsPerWeekFor(
        WorkoutActivityType activity,
        StrengthPlanPreviewData data,
        StrengthPlanPreviewChoices choices)
    {
        if (activity == WorkoutActivityType.GeneralStrength)
        {
            var isEstablished = GeneralStrengthFrequencyResolver.IsEstablishedResistanceHistory(
                choices.ActivityHistory, choices.LocalDate);
            var weekdaysCount = GeneralStrengthFrequencyResolver.ResolveAvailableWeekdaysCount(
                choices.RequestedAvailableWeekdays ?? data.AvailableWeekdays);
            var gsPolicy = GeneralStrengthFrequencyResolver.Resolve(
                weekdaysCount, isEstablished, data.ProfileTrainingExperience);
            return Math.Min(gsPolicy.PolicySessionsPerWeek, gsPolicy.TargetSessionsPerWeek);
        }

        return activity switch
        {
            WorkoutActivityType.HighVolumeStrength => ActivityPrescriptionRule.HighVolumeStrengthSessionsPerWeek,
            WorkoutActivityType.StrengthHighIntensity =>
                ActivityPrescriptionRule.StrengthHighIntensityMaximumSessionsPerWeekInitially,
            _ => NoSessions
        };
    }

    public static StrengthPlanPreviewResult EvaluateWithFallback(
        StrengthPlanPreviewChoices? choices,
        StrengthPlanPreviewData? data)
    {
        if (choices is null || data is null)
            return EvaluateDirect(choices, data);

        var originalResult = EvaluateDirect(choices, data);

        if (choices.Activity != WorkoutActivityType.GeneralStrength)
        {
            var fallbackCode = originalResult.LimitationCodes
                .FirstOrDefault(code => code is BeginnerProfileRequiresGeneralStrengthCode
                                             or HighVolumeRequiresEstablishedResistanceHistoryCode);

            if (fallbackCode is not null)
            {
                var fallbackReason = fallbackCode;
                var gsHistory = choices.ActivityHistory is not null
                    ? choices.ActivityHistory with { ActivityId = WorkoutActivityType.GeneralStrength.ToString() }
                    : null;
                var gsChoices = choices with
                {
                    Activity = WorkoutActivityType.GeneralStrength,
                    ActivityHistory = gsHistory
                };

                var selected = data.SelectedActivities is not null
                    ? new HashSet<WorkoutActivityType>(data.SelectedActivities)
                    : new HashSet<WorkoutActivityType>();
                selected.Add(WorkoutActivityType.GeneralStrength);

                var durations = data.ActivitySessionDurationsMinutes is not null
                    ? new Dictionary<WorkoutActivityType, int>(data.ActivitySessionDurationsMinutes)
                    : new Dictionary<WorkoutActivityType, int>();

                if (!durations.ContainsKey(WorkoutActivityType.GeneralStrength))
                {
                    durations[WorkoutActivityType.GeneralStrength] =
                        durations.TryGetValue(choices.Activity, out var origDur)
                            ? origDur
                            : WorkoutPreferencesRule.DefaultActivitySessionDurationMinutes;
                }

                var gsData = data with
                {
                    SelectedActivities = selected,
                    ActivitySessionDurationsMinutes = durations
                };

                var gsResult = EvaluateDirect(gsChoices, gsData);

                return gsResult with
                {
                    EffectiveActivity = WorkoutActivityType.GeneralStrength,
                    FallbackSource = GeneralStrengthFallbackSource,
                    FallbackReasonCode = fallbackReason
                };
            }
        }

        return originalResult;
    }

    private static int MaximumWorkingExercises(WorkoutActivityType activity,
        StrengthTemplateDefinition template) => activity switch
    {
        WorkoutActivityType.HighVolumeStrength =>
            ActivityPrescriptionRule.HighVolumeMaximumWorkingExercisesPerSession,
        WorkoutActivityType.StrengthHighIntensity =>
            ActivityPrescriptionRule.StrengthHighIntensityMaximumWorkingExercisesPerSession,
        _ => template.Slots.Count
    };

    private static HashSet<ExerciseEquipmentKind> EquipmentFor(Equipment equipment) => equipment switch
    {
        Equipment.FullGym => Enum.GetValues<ExerciseEquipmentKind>().ToHashSet(),
        Equipment.HomeDumbbellsBands => new HashSet<ExerciseEquipmentKind>
        {
            ExerciseEquipmentKind.Bodyweight,
            ExerciseEquipmentKind.Dumbbell,
            ExerciseEquipmentKind.ResistanceBand
        },
        Equipment.Bodyweight => new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Bodyweight },
        _ => new HashSet<ExerciseEquipmentKind>()
    };

    private static decimal? EstimateAndTrimOptional(List<StrengthSlotSelection> selected,
        StrengthPlanPreviewData data, int availableMinutes, int warmupMinutes, int cooldownMinutes)
    {
        decimal? Estimate()
        {
            decimal totalMinutes = warmupMinutes + cooldownMinutes;
            foreach (var slot in selected)
            {
                if (slot.ExerciseId is not { } exerciseId || slot.Sets is not > NoDirectSets ||
                    slot.MinimumRepetitions is not > NoDirectSets || slot.RestSeconds is not >= NoMinutes ||
                    !data.SecondsPerRepByExerciseId!.TryGetValue(exerciseId, out var secondsPerRep) ||
                    secondsPerRep <= NoSecondsPerRep)
                    return null;

                var entry = data.Catalog!.SingleOrDefault(item => item.Candidate.ExerciseId == exerciseId);
                if (entry?.Candidate.SetupMinutes is not { } setupMinutes || setupMinutes < NoSetupMinutes)
                    return null;

                var unilateralFactor = entry.Candidate.UnilateralDoublesTime
                    ? UnilateralTimeMultiplier
                    : StandardTimeMultiplier;
                totalMinutes += setupMinutes +
                    (decimal)slot.Sets.Value * slot.MinimumRepetitions.Value * secondsPerRep *
                    unilateralFactor / SecondsPerMinute +
                    (decimal)(slot.Sets.Value - OneSession) * slot.RestSeconds.Value / SecondsPerMinute;
            }

            return totalMinutes;
        }

        var estimate = Estimate();
        while ((estimate is null || estimate > availableMinutes) &&
               selected.LastOrDefault(slot => !slot.IsRequired) is { } optional)
        {
            selected.Remove(optional);
            estimate = Estimate();
        }

        return estimate is > NoMinutes && estimate <= availableMinutes ? estimate : null;
    }

    private static Dictionary<string, string> SlotConditions(
        StrengthTemplateDefinition template, StrengthPlanPreviewData data) =>
        template.Slots.ToDictionary(slot => slot.SlotId,
            slot => data.TemplateSlotReviewConditions!.GetValueOrDefault(
                $"{template.TemplateId}|{slot.SlotId}") ?? MissingTemplateSlotReview,
            StringComparer.Ordinal);

    private static StrengthExerciseReviewCondition[] ExerciseReviewConditions(
        IReadOnlyList<StrengthTemplateDefinition> templates, StrengthPlanPreviewData data)
    {
        var relevantIds = data.ReviewedAllowedExerciseIdsByTemplateSlot!
            .Where(pair => templates.Any(template => pair.Key.StartsWith(
                template.TemplateId + "|", StringComparison.Ordinal)))
            .SelectMany(pair => pair.Value).ToHashSet();
        return data.Catalog!
            .Where(entry => relevantIds.Contains(entry.Candidate.ExerciseId) &&
                            entry.Candidate.ReviewEvidencePresent)
            .OrderBy(entry => entry.Candidate.ExerciseId)
            .Select(entry => new StrengthExerciseReviewCondition(entry.Candidate.ExerciseId,
                entry.Name, entry.Candidate.ExperienceConditions ?? string.Empty,
                entry.Candidate.ReviewConditions ?? string.Empty))
            .ToArray();
    }

    private static StrengthPlanPreviewResult Failure(StrengthPlanPreviewStatus status,
        ActivityExperience experience, string reason) =>
        new(status, experience, [], [reason], [], []);
}
