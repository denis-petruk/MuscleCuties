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
    public IReadOnlyDictionary<int, IReadOnlySet<string>>? MuscleGroupsByExerciseId { get; init; }
    public IReadOnlyDictionary<string, TemplateVolumeFloor>? TemplateVolumeFloors { get; init; }
}

public sealed record StrengthPlanPreviewSession(
    string TemplateId,
    string TemplateVersion,
    int SessionIndex,
    decimal? EstimatedMinutes,
    IReadOnlyList<StrengthSlotSelection> Slots,
    IReadOnlyList<string> LimitationCodes,
    IReadOnlyDictionary<string, string> SlotReviewConditions,
    DayOfWeek? PlannedWeekday = null,
    int? OriginalPlannedWorkingSets = null,
    int? ActualWorkingSets = null);

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
    string? FallbackReasonCode = null,
    int? StrengthSessionLimitMinutes = null,
    bool RequiresReducedVolumeAcknowledgment = false,
    int? OriginalPlannedWorkingSets = null,
    int? ActualWorkingSets = null);

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
    public const string ReducedVolumeNotice = "StrengthVolumeReducedToFitDurationOrSchedule";

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
            activity == WorkoutActivityType.StrengthHighIntensity)
        {
            return Failure(StrengthPlanPreviewStatus.Limited, experience,
                BeginnerProfileRequiresGeneralStrengthCode);
        }

        if (activity != WorkoutActivityType.GeneralStrength &&
                 activity != WorkoutActivityType.HighVolumeStrength &&
                 experience == ActivityExperience.Unknown &&
                 !choices.UnknownExperienceAssessmentCompleted)
        {
            return Failure(StrengthPlanPreviewStatus.Limited, experience, ExperienceHistoryMissingCode);
        }

        var profileExperienceDecision = ProfileTrainingExperiencePlanningRule.Resolve(
            experience, data.ProfileTrainingExperience, choices.ActivityHistory,
            allowProfileFallbackWhenHistoryMissing: true);
        var prescriptionExperience = profileExperienceDecision.PrescriptionExperience;

        if (activity is WorkoutActivityType.GeneralStrength or WorkoutActivityType.HighVolumeStrength)
        {
            if (prescriptionExperience == ActivityExperience.Unknown)
                prescriptionExperience = ActivityExperience.Beginner;
            if (experience == ActivityExperience.Unknown)
                experience = ActivityExperience.Beginner;
        }

        if (profileExperienceDecision.UsesConservativeProfileCap &&
            activity == WorkoutActivityType.StrengthHighIntensity)
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
        var movementOmissions = new List<string>();
        var anyVolumeReduced = false;
        var totalOriginalWorkingSets = 0;
        var totalActualWorkingSets = 0;

        foreach (var originalTemplate in templates)
        {
            var template = originalTemplate;
            var floor = TemplateVolumeFloorRule.GetReviewedFloor(template.TemplateId, data.TemplateVolumeFloors);
            var selectionInput = new StrengthTemplateSelectionInput(template, activity, data.Goal,
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
                    GoalPriorityByExerciseId: data.GoalPriorityByExerciseId);
            var resolution = StrengthTemplateResolver.Resolve(selectionInput, MaximumWorkingExercises(activity, template));
            if (resolution.Slots.Any(slot => slot.IsRequired && slot.Outcome == StrengthSlotResolutionOutcome.NoSafeCandidate))
            {
                foreach (var alternative in StrengthTemplateCatalog.AlternativesForSchedule(scheduledSessionsPerWeek, template))
                {
                    var alternativeResolution = StrengthTemplateResolver.Resolve(selectionInput with { Template = alternative },
                        MaximumWorkingExercises(activity, alternative));
                    if (alternativeResolution.Status != "Resolved") continue;
                    template = alternative;
                    resolution = alternativeResolution;
                    floor = TemplateVolumeFloorRule.GetReviewedFloor(template.TemplateId, data.TemplateVolumeFloors);
                    break;
                }
            }

            if (resolution.Status != "Resolved")
            {
                var unresolvedSlots = resolution.Slots.Where(slot => slot.IsRequired && !slot.ExerciseId.HasValue).ToArray();
                var safeOmission = resolution.Status == "Limited" && unresolvedSlots.Length > 0 &&
                    unresolvedSlots.All(slot => slot.Outcome == StrengthSlotResolutionOutcome.NoSafeCandidate) &&
                    resolution.Slots.Any(slot => slot.ExerciseId.HasValue);
                if (!safeOmission)
                {
                    limitations.AddRange(resolution.LimitationCodes);
                    sessions.Add(new StrengthPlanPreviewSession(template.TemplateId, template.Version,
                        template.SessionIndex, null, resolution.Slots, resolution.LimitationCodes,
                        SlotConditions(template, data)));
                    continue;
                }
                movementOmissions.AddRange(unresolvedSlots.Select(slot => $"MovementOmitted:{template.TemplateId}:{slot.SlotId}"));
            }

            var originalWorkingSetsInSession = resolution.Slots
                .Where(slot => slot.ExerciseId.HasValue)
                .Sum(slot => slot.Sets.GetValueOrDefault());
            totalOriginalWorkingSets += originalWorkingSetsInSession;

            var selected = resolution.Slots.Where(slot => slot.ExerciseId.HasValue).ToList();
            var estimatedMinutes = EstimateAndTrimOptional(selected, data, sessionMinutes,
                choices.WarmupMinutes!.Value, choices.CooldownMinutes!.Value, floor);

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
                    choices.WarmupMinutes.Value, choices.CooldownMinutes.Value, floor);
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

            var actualWorkingSetsInSession = selected.Sum(slot => slot.Sets.GetValueOrDefault());
            totalActualWorkingSets += actualWorkingSetsInSession;

            if (actualWorkingSetsInSession < originalWorkingSetsInSession)
            {
                anyVolumeReduced = true;
                if (floor is null)
                    limitations.Add($"{TemplateVolumeFloorRule.TemplateVolumeFloorReviewMissing}:{template.TemplateId}");
            }

            if (floor is not null)
            {
                var floorEvaluation = TemplateVolumeFloorRule.EvaluateSelections(template.TemplateId,
                    selected, data.TemplateVolumeFloors);
                if (!floorEvaluation.IsAllowed)
                    limitations.Add(floorEvaluation.LimitationCode ?? TemplateVolumeFloorRule.VolumeBelowTemplateFloor);
            }

            IReadOnlyList<string> sessionLimitations = estimatedMinutes.HasValue
                ? NoCodes
                : ["RequiredMovementWorkExceedsSessionDurationOrTimingMissing"];
            if (!estimatedMinutes.HasValue)
            {
                limitations.AddRange(sessionLimitations);
                if (floor is null)
                    limitations.Add($"{TemplateVolumeFloorRule.TemplateVolumeFloorReviewMissing}:{template.TemplateId}");
            }
            sessions.Add(new StrengthPlanPreviewSession(template.TemplateId, template.Version,
                template.SessionIndex, estimatedMinutes, selected, sessionLimitations,
                SlotConditions(template, data),
                OriginalPlannedWorkingSets: originalWorkingSetsInSession,
                ActualWorkingSets: actualWorkingSetsInSession));
        }

        // Review data must remain a hard prerequisite even when an initial weekday assignment fails.
        if (sessions.SelectMany(session => session.Slots).Where(slot => slot.ExerciseId.HasValue)
            .Any(slot => data.MuscleGroupsByExerciseId is null ||
                !data.MuscleGroupsByExerciseId.TryGetValue(slot.ExerciseId!.Value, out var groups) ||
                groups.Count == 0 || groups.Any(string.IsNullOrWhiteSpace)))
            limitations.Add(WorkoutScheduleValidationRule.StrengthMuscleMetadataMissing);
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

        if (assignments.Status == StrengthWeekdayAssignmentStatus.Assigned)
        {
            var validationSessions = sessions.Where(session => session.Slots.Any(slot => slot.ExerciseId.HasValue))
                .Select(session =>
            {
                var muscles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var missingMuscle = false;
                foreach (var slot in session.Slots.Where(slot => slot.ExerciseId.HasValue))
                {
                    if (data.MuscleGroupsByExerciseId is null ||
                        !data.MuscleGroupsByExerciseId.TryGetValue(slot.ExerciseId!.Value, out var exerciseMuscles) ||
                        exerciseMuscles.Count == 0 || exerciseMuscles.Any(string.IsNullOrWhiteSpace))
                        missingMuscle = true;
                    else
                        muscles.UnionWith(exerciseMuscles);
                }
                return new WorkoutScheduleValidationSession(session.PlannedWeekday!.Value, activity, true,
                    missingMuscle ? null : muscles);
            }).ToArray();
            limitations.AddRange(WorkoutScheduleValidationRule.Validate(validationSessions)
                .Where(code => code != WorkoutScheduleValidationRule.ConsecutiveStrengthMuscleGroups));
        }

        var limitationCodes = limitations.Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var reviewConditions = ExerciseReviewConditions(templates, data);
        var advisories = new List<string>(profileExperienceDecision.ReasonCodes);
        advisories.AddRange(movementOmissions);
        if (scheduledSessionsPerWeek == OneSession)
            advisories.Add("OneDayFullBodyHasLimitedWeeklyCoverage");
        if (activity == WorkoutActivityType.GeneralStrength &&
            (data.ProfileTrainingExperience == TrainingExperienceLevel.Beginner ||
             profileExperienceDecision.EffectiveExperienceLevel == TrainingExperienceLevel.Beginner ||
             prescriptionExperience == ActivityExperience.Beginner))
        {
            advisories.Add("DirectBeginnerGeneralStrengthSettingsApplied");
        }

        var requiresReducedVolumeAcknowledgment = anyVolumeReduced;
        if (requiresReducedVolumeAcknowledgment)
        {
            advisories.Add(ReducedVolumeNotice);
        }

        return new StrengthPlanPreviewResult(
            limitationCodes.Length == NoSessions
                ? StrengthPlanPreviewStatus.Feasible : StrengthPlanPreviewStatus.Limited,
            experience, sessions, limitationCodes, reviewConditions,
            advisories.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            activity, StrengthSessionLimitMinutes: sessionMinutes,
            RequiresReducedVolumeAcknowledgment: requiresReducedVolumeAcknowledgment,
            OriginalPlannedWorkingSets: totalOriginalWorkingSets,
            ActualWorkingSets: totalActualWorkingSets);
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

    // Compatibility entry point for existing callers; selected styles must never be
    // silently replaced with an unselected style.
    public static StrengthPlanPreviewResult EvaluateWithFallback(
        StrengthPlanPreviewChoices? choices,
        StrengthPlanPreviewData? data) => EvaluateDirect(choices, data);

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
        StrengthPlanPreviewData data, int availableMinutes, int warmupMinutes, int cooldownMinutes,
        TemplateVolumeFloor? floor = null)
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
        while (floor is not null && (estimate is null || estimate > availableMinutes) &&
               selected.LastOrDefault(slot => !slot.IsRequired) is { } optional)
        {
            selected.Remove(optional);
            estimate = Estimate();
        }

        if ((estimate is null || estimate > availableMinutes) && floor is not null)
        {
            const int SingleSetDecrement = 1;
            bool canReduceFurther;
            do
            {
                canReduceFurther = false;
                for (var i = selected.Count - 1; i >= 0; i--)
                {
                    var slot = selected[i];
                    var floorMin = floor.MinimumSetsBySlotId.GetValueOrDefault(
                        slot.SlotId, slot.Sets.Value);
                    if (slot.Sets.HasValue && slot.Sets.Value > floorMin)
                    {
                        var newSets = slot.Sets.Value - SingleSetDecrement;
                        selected[i] = slot with { Sets = newSets };
                        canReduceFurther = true;
                        estimate = Estimate();
                        if (estimate is > NoMinutes && estimate <= availableMinutes)
                            break;
                    }
                }
            } while (canReduceFurther && (estimate is null || estimate > availableMinutes));
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
