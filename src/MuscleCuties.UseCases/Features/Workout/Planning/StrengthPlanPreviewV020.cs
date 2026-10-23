using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.UseCases.Features.Workout.Planning;

public sealed record StrengthPlanPreviewRequestV020(
    WorkoutActivityType Activity,
    int StrengthDaysPerWeek,
    DateOnly LocalDate,
    ActivityTrainingHistoryV020? ActivityHistory,
    IReadOnlySet<int>? ExcludedExerciseIds = null,
    IReadOnlySet<int>? UserConfirmedFamiliarExerciseIds = null,
    IReadOnlySet<int>? AcknowledgedExerciseConditionIds = null,
    bool UnknownExperienceAssessmentCompleted = false,
    IReadOnlySet<int>? UserConfirmedManageableLoadExerciseIds = null,
    IReadOnlySet<string>? AcknowledgedSubstitutionPairKeys = null,
    int? WarmupMinutes = null,
    int? CooldownMinutes = null);

public sealed record StrengthPlanPreviewSourceV020(
    UserGoal Goal,
    Equipment Equipment,
    int SessionMinutes,
    InjuryFlag ActiveInjuryFlags,
    bool HasCurrentSymptoms,
    IReadOnlyList<StrengthExerciseCatalogEntryV020> Catalog,
    IReadOnlyDictionary<int, int> PreferredExerciseByOriginalId,
    IReadOnlySet<int> AcceptedExerciseIds,
    IReadOnlyDictionary<int, int> CompletedExerciseExposureCounts,
    IReadOnlySet<int> KnownManageableLoadExerciseIds,
    IReadOnlyDictionary<int, int> SecondsPerRepByExerciseId,
    IReadOnlyDictionary<int, string> PrimaryMuscleByExerciseId,
    IReadOnlyDictionary<string, IReadOnlySet<int>> ReviewedAllowedExerciseIdsByTemplateSlot,
    IReadOnlyDictionary<string, string> TemplateSlotReviewConditions);

public interface IStrengthPlanPreviewSourceV020
{
    Task<StrengthPlanPreviewSourceV020?> LoadAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record StrengthPlanPreviewSessionV020(
    string TemplateId,
    string TemplateVersion,
    int SessionIndex,
    decimal? EstimatedMinutes,
    IReadOnlyList<StrengthSlotSelectionV020> Slots,
    IReadOnlyList<string> LimitationCodes,
    IReadOnlyDictionary<string, string> SlotReviewConditions);

public sealed record StrengthExerciseReviewConditionV020(
    int ExerciseId,
    string ExerciseName,
    string ExperienceConditions,
    string GeneralConditions);

public sealed record StrengthPlanPreviewResponseV020(
    string PolicyId,
    string PolicyVersion,
    string RulesetVersion,
    string Status,
    ActivityExperienceV020 Experience,
    IReadOnlyList<StrengthPlanPreviewSessionV020> Sessions,
    IReadOnlyList<string> LimitationCodes,
    int? FeasibleAlternativeStrengthDaysPerWeek,
    IReadOnlyList<StrengthExerciseReviewConditionV020> ExerciseReviewConditions,
    IReadOnlyList<string> AdvisoryCodes);

/// <summary>Uses only catalog reviewed metadata and server-owned profile, injury and performance records.</summary>
public sealed class PreviewStrengthPlanV020UseCase(IStrengthPlanPreviewSourceV020 source)
{
    public async Task<StrengthPlanPreviewResponseV020> ExecuteAsync(Guid userId,
        StrengthPlanPreviewRequestV020 request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (userId == Guid.Empty || !Enum.IsDefined(request.Activity) ||
            request.Activity is not (WorkoutActivityType.GeneralStrength or
                WorkoutActivityType.HighVolumeStrength or WorkoutActivityType.StrengthHighIntensity) ||
            request.StrengthDaysPerWeek is < 1 or > 4 ||
            request.ExcludedExerciseIds?.Any(id => id <= 0) == true ||
            request.UserConfirmedFamiliarExerciseIds?.Any(id => id <= 0) == true ||
            request.AcknowledgedExerciseConditionIds?.Any(id => id <= 0) == true)
            return Result("InvalidInput", ActivityExperienceV020.Unknown, [], ["InvalidStrengthPreviewInput"]);
        if (request.UserConfirmedManageableLoadExerciseIds?.Any(id => id <= 0) == true ||
            request.AcknowledgedSubstitutionPairKeys?.Any(string.IsNullOrWhiteSpace) == true ||
            request.WarmupMinutes is < 0 || request.CooldownMinutes is < 0)
            return Result("InvalidInput", ActivityExperienceV020.Unknown, [], ["InvalidStrengthPreviewInput"]);
        if (request.WarmupMinutes is null || request.CooldownMinutes is null)
            return Result("Limited", ActivityExperienceV020.Unknown, [],
                ["StrengthWarmupOrCooldownDurationMissing"]);

        var profile = await source.LoadAsync(userId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
            return Result("InvalidInput", ActivityExperienceV020.Unknown, [], ["PlanningProfileMissing"]);
        if (profile.SessionMinutes <= 0)
            return Result("Limited", ActivityExperienceV020.Unknown, [], ["SessionDurationMissing"]);
        if (request.Activity == WorkoutActivityType.StrengthHighIntensity &&
            request.StrengthDaysPerWeek > WorkoutTrainingPolicyV020.StrengthHighIntensityMaximumSessionsPerWeekInitially)
            return Result("Limited", ActivityExperienceV020.Unknown, [],
                ["HighIntensityStrengthLimitedToTwoInitialSessionsPerWeek"],
                WorkoutTrainingPolicyV020.StrengthHighIntensityMaximumSessionsPerWeekInitially);
        if (request.StrengthDaysPerWeek > WorkoutTrainingPolicyV020.InitialMaximumGeneratedHardDaysPerWeek)
            return Result("Limited", ActivityExperienceV020.Unknown, [],
                ["FourStrengthDaysExceedThreeHardDayWeeklyCap"],
                WorkoutTrainingPolicyV020.InitialMaximumGeneratedHardDaysPerWeek);

        var experience = ActivityExperienceRuleV020.Classify(request.Activity.ToString(),
            request.ActivityHistory, request.LocalDate).Experience;
        if (experience == ActivityExperienceV020.Unknown &&
            !request.UnknownExperienceAssessmentCompleted)
            return Result("Limited", experience, [], ["ActivityExperienceHistoryMissing"]);

        var availableEquipment = AvailableEquipment(profile.Equipment);
        var context = new ExerciseEligibilityContext(availableEquipment, experience,
            request.ExcludedExerciseIds ?? new HashSet<int>(), new HashSet<MovementPattern>(),
            new HashSet<ActivityDemandTag>(), false,
            profile.ActiveInjuryFlags,
            request.AcknowledgedExerciseConditionIds,
            request.UnknownExperienceAssessmentCompleted,
            request.AcknowledgedSubstitutionPairKeys);
        var prescription = new ActivityPrescriptionContextV020(request.Activity, experience, null,
            profile.HasCurrentSymptoms, true,
            experience == ActivityExperienceV020.Established,
            0, false, false);
        var templates = StrengthTemplateCatalogV020.ForSchedule(request.StrengthDaysPerWeek);
        var sessions = new List<StrengthPlanPreviewSessionV020>();
        var weeklySets = new Dictionary<string, int>(StringComparer.Ordinal);
        var limitations = new List<string>();

        foreach (var template in templates)
        {
            var resolution = StrengthTemplateResolverV020.Resolve(
                new StrengthTemplateSelectionInputV020(template, request.Activity, profile.Goal,
                    prescription, profile.Catalog, context, profile.PreferredExerciseByOriginalId,
                    profile.CompletedExerciseExposureCounts.Keys.ToHashSet(),
                    profile.CompletedExerciseExposureCounts.Keys.ToHashSet(),
                    profile.AcceptedExerciseIds, CompletedExerciseExposureCounts:
                    profile.CompletedExerciseExposureCounts,
                    KnownManageableLoadExerciseIds: profile.KnownManageableLoadExerciseIds
                        .Intersect(request.UserConfirmedManageableLoadExerciseIds ?? new HashSet<int>())
                        .ToHashSet(),
                    UserConfirmedFamiliarityExerciseIds:
                    request.UserConfirmedFamiliarExerciseIds ?? new HashSet<int>(),
                    ReviewedAllowedExerciseIdsByTemplateSlot:
                    profile.ReviewedAllowedExerciseIdsByTemplateSlot),
                request.Activity switch
                {
                    WorkoutActivityType.HighVolumeStrength =>
                        WorkoutTrainingPolicyV020.HighVolumeMaximumWorkingExercisesPerSession,
                    WorkoutActivityType.StrengthHighIntensity =>
                        WorkoutTrainingPolicyV020.StrengthHighIntensityMaximumWorkingExercisesPerSession,
                    _ => template.Slots.Count
                });
            if (resolution.Status != "Resolved")
            {
                limitations.AddRange(resolution.LimitationCodes);
                sessions.Add(new StrengthPlanPreviewSessionV020(template.TemplateId, template.Version,
                    template.SessionIndex, null, resolution.Slots, resolution.LimitationCodes,
                    SlotConditions(template, profile)));
                continue;
            }

            var selected = resolution.Slots.Where(slot => slot.ExerciseId.HasValue).ToList();
            _ = EstimateAndTrimOptional(selected, profile, profile.SessionMinutes,
                request.WarmupMinutes.Value, request.CooldownMinutes.Value);
            if (request.Activity == WorkoutActivityType.HighVolumeStrength)
            {
                var sessionSets = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var slot in selected.ToArray())
                {
                    var exerciseId = slot.ExerciseId!.Value;
                    if (!profile.PrimaryMuscleByExerciseId.TryGetValue(exerciseId, out var muscle) ||
                        string.IsNullOrWhiteSpace(muscle))
                    {
                        limitations.Add($"PrimaryMuscleMissing:{exerciseId}");
                        continue;
                    }
                    var projected = weeklySets.GetValueOrDefault(muscle) +
                                    sessionSets.GetValueOrDefault(muscle) + slot.Sets.GetValueOrDefault();
                    if (projected > WorkoutTrainingPolicyV020.HighVolumeInitialDirectSetsPerPrimaryMusclePerWeek &&
                        !slot.IsRequired)
                    {
                        selected.Remove(slot);
                        continue;
                    }
                    sessionSets[muscle] = sessionSets.GetValueOrDefault(muscle) +
                                          slot.Sets.GetValueOrDefault();
                    if (projected > WorkoutTrainingPolicyV020.HighVolumeInitialDirectSetsPerPrimaryMusclePerWeek)
                        limitations.Add($"DirectSetCeilingExceeded:{muscle}");
                }
                var finalEstimate = EstimateAndTrimOptional(selected, profile, profile.SessionMinutes,
                    request.WarmupMinutes.Value, request.CooldownMinutes.Value);
                if (finalEstimate.HasValue)
                {
                    foreach (var slot in selected)
                    {
                        var muscle = profile.PrimaryMuscleByExerciseId.GetValueOrDefault(slot.ExerciseId!.Value);
                        if (!string.IsNullOrWhiteSpace(muscle))
                            weeklySets[muscle] = weeklySets.GetValueOrDefault(muscle) +
                                                 slot.Sets.GetValueOrDefault();
                    }
                }
            }
            var estimate = EstimateAndTrimOptional(selected, profile, profile.SessionMinutes,
                request.WarmupMinutes.Value, request.CooldownMinutes.Value);
            if (estimate is null)
                limitations.Add("RequiredMovementWorkExceedsSessionDurationOrTimingMissing");
            sessions.Add(new StrengthPlanPreviewSessionV020(template.TemplateId, template.Version,
                template.SessionIndex, estimate, selected, estimate is null
                    ? ["RequiredMovementWorkExceedsSessionDurationOrTimingMissing"] : [],
                SlotConditions(template, profile)));
        }

        var codes = limitations.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var relevantIds = profile.ReviewedAllowedExerciseIdsByTemplateSlot
            .Where(pair => templates.Any(template => pair.Key.StartsWith(
                template.TemplateId + "|", StringComparison.Ordinal)))
            .SelectMany(pair => pair.Value).ToHashSet();
        var exerciseConditions = profile.Catalog
            .Where(entry => relevantIds.Contains(entry.Candidate.ExerciseId) &&
                            entry.Candidate.ReviewEvidencePresent)
            .OrderBy(entry => entry.Candidate.ExerciseId)
            .Select(entry => new StrengthExerciseReviewConditionV020(entry.Candidate.ExerciseId,
                entry.Name, entry.Candidate.ExperienceConditionsV020 ?? string.Empty,
                entry.Candidate.ReviewConditionsV020 ?? string.Empty))
            .ToArray();
        return Result(codes.Length == 0 ? "Feasible" : "Limited", experience, sessions, codes,
            exerciseConditions: exerciseConditions,
            advisoryCodes: request.StrengthDaysPerWeek == 1
                ? ["OneDayFullBodyHasLimitedWeeklyCoverage"] : []);
    }

    private static decimal? EstimateAndTrimOptional(List<StrengthSlotSelectionV020> selected,
        StrengthPlanPreviewSourceV020 source, int availableMinutes,
        int warmupMinutes, int cooldownMinutes)
    {
        decimal? Estimate()
        {
            decimal total = warmupMinutes + cooldownMinutes;
            foreach (var slot in selected)
            {
                if (slot.ExerciseId is not { } id || slot.Sets is not > 0 ||
                    slot.MinimumRepetitions is not > 0 || slot.RestSeconds is not >= 0 ||
                    !source.SecondsPerRepByExerciseId.TryGetValue(id, out var secondsPerRep) ||
                    secondsPerRep <= 0)
                    return null;
                var candidate = source.Catalog.First(entry => entry.Candidate.ExerciseId == id).Candidate;
                if (candidate.SetupMinutes is not { } setup)
                    return null;
                total += setup +
                         (decimal)slot.Sets.Value * slot.MinimumRepetitions.Value * secondsPerRep *
                         (candidate.UnilateralDoublesTime ? 2 : 1) / 60m +
                         (decimal)(slot.Sets.Value - 1) * slot.RestSeconds.Value / 60m;
            }
            return total;
        }

        var estimate = Estimate();
        while ((estimate is null || estimate > availableMinutes) &&
               selected.LastOrDefault(slot => !slot.IsRequired) is { } optional)
        {
            selected.Remove(optional);
            estimate = Estimate();
        }
        return !estimate.HasValue || estimate.Value <= 0 || estimate.Value > availableMinutes
            ? null : estimate;
    }

    private static IReadOnlyDictionary<string, string> SlotConditions(
        StrengthTemplateDefinitionV020 template, StrengthPlanPreviewSourceV020 source) =>
        template.Slots.ToDictionary(slot => slot.SlotId,
            slot => source.TemplateSlotReviewConditions.GetValueOrDefault(
                $"{template.TemplateId}|{slot.SlotId}") ?? "Template slot review missing",
            StringComparer.Ordinal);

    private static IReadOnlySet<ExerciseEquipmentKind> AvailableEquipment(Equipment equipment) =>
        equipment switch
        {
            Equipment.FullGym => Enum.GetValues<ExerciseEquipmentKind>().ToHashSet(),
            Equipment.HomeDumbbellsBands => new HashSet<ExerciseEquipmentKind>
            {
                ExerciseEquipmentKind.Bodyweight, ExerciseEquipmentKind.Dumbbell,
                ExerciseEquipmentKind.ResistanceBand
            },
            Equipment.Bodyweight => new HashSet<ExerciseEquipmentKind>
            {
                ExerciseEquipmentKind.Bodyweight
            },
            _ => new HashSet<ExerciseEquipmentKind>()
        };

    private static StrengthPlanPreviewResponseV020 Result(string status, ActivityExperienceV020 experience,
        IReadOnlyList<StrengthPlanPreviewSessionV020> sessions, IReadOnlyList<string> reasons,
        int? alternativeDays = null,
        IReadOnlyList<StrengthExerciseReviewConditionV020>? exerciseConditions = null,
        IReadOnlyList<string>? advisoryCodes = null) =>
        new(WorkoutTrainingPolicyV020.PolicyId, WorkoutTrainingPolicyV020.Version,
            WorkoutTrainingPolicyV020.RulesetVersion, status, experience, sessions, reasons,
            alternativeDays, exerciseConditions ?? [], advisoryCodes ?? []);
}
