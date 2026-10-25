using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public sealed record StrengthExerciseCatalogEntry(
    string Name,
    ReviewedExerciseCandidate Candidate,
    IReadOnlyList<string> MetadataGaps);

public sealed record StrengthTemplateSelectionInput(
    StrengthTemplateDefinition Template,
    WorkoutActivityType Activity,
    UserGoal Goal,
    ActivityPrescriptionContext PrescriptionContext,
    IReadOnlyList<StrengthExerciseCatalogEntry> Catalog,
    ExerciseEligibilityContext EligibilityContext,
    IReadOnlyDictionary<int, int> PreferredExerciseByOriginalId,
    IReadOnlySet<int> FamiliarExerciseIds,
    IReadOnlySet<int> UsablePerformanceHistoryExerciseIds,
    IReadOnlySet<int>? ExistingAcceptedExerciseIds = null,
    IReadOnlyDictionary<int, int>? GoalPriorityByExerciseId = null,
    IReadOnlyDictionary<int, int>? CompletedExerciseExposureCounts = null,
    IReadOnlySet<int>? KnownManageableLoadExerciseIds = null,
    IReadOnlySet<int>? UserConfirmedFamiliarityExerciseIds = null,
    IReadOnlyDictionary<string, IReadOnlySet<int>>? ReviewedAllowedExerciseIdsByTemplateSlot = null,
    IReadOnlySet<int>? UserConfirmedManageableLoadExerciseIds = null);

public sealed record StrengthSlotSelection(
    string SlotId,
    StrengthTemplateSlotId MovementSlot,
    bool IsRequired,
    StrengthPrescriptionRole PrescriptionRole,
    bool IsHeavyMain,
    int? ExerciseId,
    string? ExerciseName,
    string? SelectionReason,
    string? AppliedPrescriptionRule,
    int? Sets,
    int? MinimumRepetitions,
    int? MaximumRepetitions,
    int? RepetitionsInReserve,
    int? RestSeconds,
    IReadOnlyList<string> LimitationCodes)
{
    public int? InitialRepetitionTarget => MinimumRepetitions;
}

public sealed record StrengthSlotPrescription(
    int? Sets,
    int? MinimumRepetitions,
    int? MaximumRepetitions,
    int? RepetitionsInReserve,
    int? RestSeconds);

public sealed record StrengthTemplateResolution(
    string TemplateId,
    string TemplateVersion,
    string Status,
    IReadOnlyList<StrengthSlotSelection> Slots,
    IReadOnlyList<string> LimitationCodes);

/// <summary>Resolves slots deterministically after policy restrictions and user exclusions are applied.</summary>
public static class StrengthTemplateResolver
{
    private const string MissingCatalogMetadata = "CatalogEligibilityMetadataMissing";
    private const string NoEligibleCandidate = "NoEligibleCatalogExercise";
    private const string RequiredSlotUnresolved = "RequiredMovementSlotUnresolved";
    private const string OptionalSlotOmittedByLimit = "OptionalSlotOmittedByExerciseLimit";
    private const string TemplateSlotReviewMissingCode = "TemplateSlotReviewMissing";
    private static readonly string[] TemplateSlotReviewMissingCodes = [TemplateSlotReviewMissingCode];

    public static StrengthTemplateResolution Resolve(
        StrengthTemplateSelectionInput? input,
        int maximumWorkingExercises)
    {
        if (input?.Template is null || input.PrescriptionContext is null || input.Catalog is null || input.EligibilityContext is null ||
            input.PreferredExerciseByOriginalId is null || input.FamiliarExerciseIds is null ||
            input.UsablePerformanceHistoryExerciseIds is null || maximumWorkingExercises <= 0 ||
            !Enum.IsDefined(input.Activity) || !Enum.IsDefined(input.Goal))
            return Failure("InvalidTemplateSelectionInput");

        if (input.PrescriptionContext.Activity != input.Activity ||
            input.PrescriptionContext.Experience != input.EligibilityContext.Experience)
            return Failure("ActivityExperienceInputMismatch");
        ActivityPrescriptionResult? activityPrescription = null;
        if (input.Activity != WorkoutActivityType.StrengthHighIntensity)
        {
            activityPrescription = ActivityPrescriptionRule.Evaluate(input.PrescriptionContext);
            if (activityPrescription.Status != ActivityPrescriptionStatus.Eligible ||
                activityPrescription.Sessions.Count == 0)
                return new StrengthTemplateResolution(input.Template.TemplateId, input.Template.Version,
                    activityPrescription.Status.ToString(), [], activityPrescription.Reasons);
        }
        else if (!input.PrescriptionContext.IsExplicitlySelected ||
                 input.PrescriptionContext.HasCurrentRestrictionOrSymptom)
        {
            return new StrengthTemplateResolution(input.Template.TemplateId, input.Template.Version,
                input.PrescriptionContext.HasCurrentRestrictionOrSymptom
                    ? ActivityPrescriptionStatus.BlockedByRestriction.ToString()
                    : ActivityPrescriptionStatus.NotSelected.ToString(), [],
                [input.PrescriptionContext.HasCurrentRestrictionOrSymptom
                    ? "RestrictionOrSymptomRequiresReview" : "ActivityNotSelected"]);
        }
        else if (input.PrescriptionContext.Experience != ActivityExperience.Established ||
                 !input.PrescriptionContext.IsEstablishedResistanceHistory)
        {
            return new StrengthTemplateResolution(input.Template.TemplateId, input.Template.Version,
                ActivityPrescriptionStatus.OfferGeneralStrength.ToString(), [],
                ["HeavyStrengthRequiresEstablishedResistanceHistory"]);
        }

        var policyExerciseMaximum = input.Activity switch
        {
            WorkoutActivityType.HighVolumeStrength => ActivityPrescriptionRule.HighVolumeMaximumWorkingExercisesPerSession,
            WorkoutActivityType.StrengthHighIntensity => ActivityPrescriptionRule.StrengthHighIntensityMaximumWorkingExercisesPerSession,
            _ => int.MaxValue
        };
        var effectiveExerciseMaximum = Math.Min(maximumWorkingExercises, policyExerciseMaximum);
        var requiredSlotCount = input.Template.Slots.Count(slot => slot is { IsRequired: true });
        var heavyMainCount = input.Template.Slots.Count(slot => slot is { IsRequired: true, IsHeavyMainInHighIntensity: true });
        if (requiredSlotCount > effectiveExerciseMaximum ||
            input.Activity == WorkoutActivityType.StrengthHighIntensity &&
            heavyMainCount > ActivityPrescriptionRule.StrengthHighIntensityMaximumHeavyMainMovementsPerSession)
            return new StrengthTemplateResolution(input.Template.TemplateId, input.Template.Version,
                "Infeasible", [], [requiredSlotCount > effectiveExerciseMaximum
                    ? "RequiredCoverageExceedsStyleExerciseMaximum"
                    : "TemplateExceedsHighIntensityHeavyMainMaximum"]);

        var chosen = new List<StrengthSlotSelection>();
        var unresolved = new List<string>();
        foreach (var slot in input.Template.Slots)
        {
            if (slot is null || string.IsNullOrWhiteSpace(slot.SlotId) || slot.AcceptedPatterns is null ||
                slot.AcceptedPatterns.Count == 0 || slot.AcceptedPatterns.Any(pattern => !Enum.IsDefined(pattern)) ||
                slot.PreferredPatterns is null || slot.PreferredPatterns.Any(pattern =>
                    !Enum.IsDefined(pattern) || !slot.AcceptedPatterns.Contains(pattern)))
            {
                unresolved.Add("InvalidTemplateSlotDefinition");
                continue;
            }

            if (!slot.IsRequired && chosen.Count(selection => selection.ExerciseId.HasValue) >= effectiveExerciseMaximum)
            {
                chosen.Add(Unselected(slot, input.Activity, OptionalSlotOmittedByLimit));
                continue;
            }

            var reviewKey = $"{input.Template.TemplateId}|{slot.SlotId}";
            IReadOnlySet<int>? reviewedSlotExerciseIds = null;
            var hasReviewedSlot = input.ReviewedAllowedExerciseIdsByTemplateSlot is null ||
                                  input.ReviewedAllowedExerciseIdsByTemplateSlot.TryGetValue(reviewKey,
                                      out reviewedSlotExerciseIds);
            var matched = input.Catalog
                .Where(entry => entry is not null && entry.Candidate is not null &&
                                slot.AcceptedPatterns.Contains(entry.Candidate.Pattern) &&
                                (input.ReviewedAllowedExerciseIdsByTemplateSlot is null ||
                                 hasReviewedSlot && reviewedSlotExerciseIds!.Contains(entry.Candidate.ExerciseId)))
                .ToArray();
            var metadataGaps = matched.SelectMany(entry => entry.MetadataGaps ?? [])
                .Concat(hasReviewedSlot ? Array.Empty<string>() : TemplateSlotReviewMissingCodes)
                .Distinct(StringComparer.Ordinal).OrderBy(code => code, StringComparer.Ordinal).ToArray();

            var eligibility = ExerciseEligibilityRule.Filter(
                matched.Select(entry => entry.Candidate).ToArray(), input.EligibilityContext);
            var completeMetadataIds = matched.Where(entry => entry.MetadataGaps is { Count: 0 })
                .Select(entry => entry.Candidate.ExerciseId).ToHashSet();
            var candidatePool = eligibility.EligibleCandidates
                .Where(candidate => completeMetadataIds.Contains(candidate.ExerciseId))
                .Where(candidate => candidate.SupportedTimingMethods?.Contains(ExerciseTimingMethod.StraightSets) == true)
                .ToArray();
            var candidateIds = candidatePool.Select(candidate => candidate.ExerciseId).ToHashSet();

            var slotPreferences = input.PreferredExerciseByOriginalId
                .Where(pair => candidateIds.Contains(pair.Value) &&
                               input.Catalog.FirstOrDefault(entry =>
                                   entry.Candidate.ExerciseId == pair.Key) is { } original &&
                               input.Catalog.FirstOrDefault(entry =>
                                   entry.Candidate.ExerciseId == pair.Value) is { } substitute &&
                               ExerciseEligibilityRule.EvaluateSubstitution(
                                   original.Candidate, substitute.Candidate,
                                   input.EligibilityContext).Status == ExerciseEligibilityStatus.Eligible)
                .Select(pair => pair.Value).ToHashSet();
            var existingSelections = input.ExistingAcceptedExerciseIds ?? new HashSet<int>();
            var goalPriorities = input.GoalPriorityByExerciseId ?? new Dictionary<int, int>();
            var exposures = input.CompletedExerciseExposureCounts ?? new Dictionary<int, int>();
            var manageableLoads = input.KnownManageableLoadExerciseIds ?? new HashSet<int>();
            var confirmedFamiliarity = input.UserConfirmedFamiliarityExerciseIds ?? new HashSet<int>();
            var confirmedManageableLoads = input.UserConfirmedManageableLoadExerciseIds ?? new HashSet<int>();
            var selection = matched
                .Where(entry => candidateIds.Contains(entry.Candidate.ExerciseId))
                .Where(entry => chosen.All(previous => previous.ExerciseId != entry.Candidate.ExerciseId))
                .Where(entry => !IsHeavyMain(slot, input.Activity) ||
                    StrengthHighIntensityExerciseEvidenceRule.IsConfirmableCandidate(
                        entry.Candidate.ExerciseId, exposures, manageableLoads))
                .OrderByDescending(entry => IsHeavyMain(slot, input.Activity) &&
                    StrengthHighIntensityExerciseEvidenceRule.Evaluate(
                        entry.Candidate.ExerciseId,
                        exposures.GetValueOrDefault(entry.Candidate.ExerciseId),
                        manageableLoads.Contains(entry.Candidate.ExerciseId),
                        confirmedFamiliarity.Contains(entry.Candidate.ExerciseId),
                        confirmedManageableLoads.Contains(entry.Candidate.ExerciseId)) ==
                    StrengthHighIntensityExerciseEvidenceStatus.Confirmed)
                .ThenByDescending(entry => existingSelections.Contains(entry.Candidate.ExerciseId))
                .ThenByDescending(entry => input.FamiliarExerciseIds.Contains(entry.Candidate.ExerciseId) &&
                                           input.UsablePerformanceHistoryExerciseIds.Contains(entry.Candidate.ExerciseId))
                .ThenByDescending(entry => input.UsablePerformanceHistoryExerciseIds.Contains(entry.Candidate.ExerciseId))
                .ThenByDescending(entry => input.FamiliarExerciseIds.Contains(entry.Candidate.ExerciseId))
                .ThenByDescending(entry => slotPreferences.Contains(entry.Candidate.ExerciseId))
                .ThenByDescending(entry => slot.MovementSlot == StrengthTemplateSlotId.GoalSpecificAccessory
                    ? goalPriorities.GetValueOrDefault(entry.Candidate.ExerciseId)
                    : 0)
                .ThenByDescending(entry => slot.PreferredPatterns.Contains(entry.Candidate.Pattern))
                .ThenBy(entry => entry.Candidate.ExerciseId)
                .FirstOrDefault();

            if (selection is null)
            {
                var eligibilityCodes = eligibility.Decisions.SelectMany(decision => decision.Reasons)
                    .Select(reason => reason.ToString()).Distinct(StringComparer.Ordinal)
                    .OrderBy(reason => reason, StringComparer.Ordinal).ToArray();
                var codes = metadataGaps.Length > 0
                    ? metadataGaps.Append(MissingCatalogMetadata).ToArray()
                    : eligibilityCodes.Length > 0 ? eligibilityCodes : [NoEligibleCandidate];
                if (slot.IsRequired)
                    unresolved.Add(RequiredSlotUnresolved);
                if (IsHeavyMain(slot, input.Activity))
                    codes = codes.Append(StrengthHighIntensityExerciseEvidenceRule.TrustedEvidenceMissing).ToArray();
                var prescription = activityPrescription?.Sessions.Count > 0
                    ? SlotPrescription(activityPrescription.Sessions[0], slot, input.Activity)
                    : new StrengthSlotPrescription(null, null, null, null, null);
                chosen.Add(new StrengthSlotSelection(slot.SlotId, slot.MovementSlot, slot.IsRequired,
                    RoleFor(slot, input.Activity), IsHeavyMain(slot, input.Activity), null, null, null,
                    PrescriptionRule(input.Activity, slot), prescription.Sets,
                    prescription.MinimumRepetitions, prescription.MaximumRepetitions,
                    prescription.RepetitionsInReserve, prescription.RestSeconds, codes));
                continue;
            }

            var selectionReason = SelectionReason(selection, slotPreferences, input, slot,
                goalPriorities);
            var selectedPrescription = activityPrescription?.Sessions.Count > 0
                ? SlotPrescription(activityPrescription.Sessions[0], slot, input.Activity)
                : new StrengthSlotPrescription(null, null, null, null, null);
            chosen.Add(new StrengthSlotSelection(slot.SlotId, slot.MovementSlot, slot.IsRequired,
                RoleFor(slot, input.Activity), IsHeavyMain(slot, input.Activity),
                selection.Candidate.ExerciseId, selection.Name, selectionReason,
                PrescriptionRule(input.Activity, slot), selectedPrescription.Sets,
                selectedPrescription.MinimumRepetitions, selectedPrescription.MaximumRepetitions,
                selectedPrescription.RepetitionsInReserve, selectedPrescription.RestSeconds, []));
        }

        if (input.Activity == WorkoutActivityType.StrengthHighIntensity)
        {
            var selectedHeavyExerciseIds = chosen.Where(slot => slot.IsHeavyMain && slot.ExerciseId.HasValue)
                .Select(slot => slot.ExerciseId!.Value).ToArray();
            var requiredHeavyMainCount = input.Template.Slots.Count(slot => slot.IsRequired &&
                IsHeavyMain(slot, input.Activity));
            if (selectedHeavyExerciseIds.Length != requiredHeavyMainCount)
                return new StrengthTemplateResolution(input.Template.TemplateId, input.Template.Version,
                    ActivityPrescriptionStatus.OfferGeneralStrength.ToString(),
                    Array.AsReadOnly(chosen.ToArray()),
                    [StrengthHighIntensityExerciseEvidenceRule.TrustedEvidenceMissing]);
            var exposures = input.CompletedExerciseExposureCounts ?? new Dictionary<int, int>();
            var manageableLoads = input.KnownManageableLoadExerciseIds ?? new HashSet<int>();
            var confirmedFamiliarity = input.UserConfirmedFamiliarityExerciseIds ?? new HashSet<int>();
            var confirmedManageableLoads = input.UserConfirmedManageableLoadExerciseIds ?? new HashSet<int>();
            var readiness = selectedHeavyExerciseIds.Select(id =>
                StrengthHighIntensityExerciseEvidenceRule.Evaluate(id,
                    exposures.GetValueOrDefault(id), manageableLoads.Contains(id),
                    confirmedFamiliarity.Contains(id), confirmedManageableLoads.Contains(id))).ToArray();
            if (readiness.Any(status => status != StrengthHighIntensityExerciseEvidenceStatus.Confirmed))
                return new StrengthTemplateResolution(input.Template.TemplateId, input.Template.Version,
                    "NeedsUserConfirmation", Array.AsReadOnly(chosen.ToArray()),
                    [StrengthHighIntensityExerciseEvidenceRule.UserConfirmationRequired]);
            var minExposures = selectedHeavyExerciseIds.Length == 0 ? 0 :
                selectedHeavyExerciseIds.Min(id => exposures.GetValueOrDefault(id));
            var context = input.PrescriptionContext with
            {
                CompletedMainExerciseExposures = minExposures,
                HasKnownManageableLoad = selectedHeavyExerciseIds.Length > 0 &&
                    selectedHeavyExerciseIds.All(id => manageableLoads.Contains(id) &&
                                                       confirmedManageableLoads.Contains(id)),
                UserConfirmedCurrentFamiliarity = selectedHeavyExerciseIds.Length > 0 &&
                    selectedHeavyExerciseIds.All(confirmedFamiliarity.Contains)
            };
            activityPrescription = ActivityPrescriptionRule.Evaluate(context);
            if (activityPrescription.Status != ActivityPrescriptionStatus.Eligible ||
                activityPrescription.Sessions.Count == 0)
                return new StrengthTemplateResolution(input.Template.TemplateId, input.Template.Version,
                    activityPrescription.Status.ToString(), Array.AsReadOnly(chosen.ToArray()),
                    activityPrescription.Reasons.Append("HighIntensitySelectedMainPrerequisiteMissing").ToArray());

            chosen = chosen.Select(slot =>
            {
                var definition = input.Template.Slots.Single(definition => definition.SlotId == slot.SlotId);
                var prescription = SlotPrescription(activityPrescription.Sessions[0], definition, input.Activity);
                return slot with
                {
                    Sets = prescription.Sets,
                    MinimumRepetitions = prescription.MinimumRepetitions,
                    MaximumRepetitions = prescription.MaximumRepetitions,
                    RepetitionsInReserve = prescription.RepetitionsInReserve,
                    RestSeconds = prescription.RestSeconds
                };
            }).ToList();
        }

        var status = unresolved.Count > 0 ? "Limited" : "Resolved";
        return new StrengthTemplateResolution(input.Template.TemplateId, input.Template.Version,
            status, Array.AsReadOnly(chosen.ToArray()),
            Array.AsReadOnly(unresolved.Distinct(StringComparer.Ordinal).ToArray()));
    }

    private static StrengthTemplateResolution Failure(string reason) =>
        new(string.Empty, StrengthTemplateCatalog.Version, "Invalid", [], [reason]);

    private static StrengthSlotSelection Unselected(StrengthTemplateSlot slot,
        WorkoutActivityType activity, string reason) =>
        new(slot.SlotId, slot.MovementSlot, false, RoleFor(slot, activity), IsHeavyMain(slot, activity),
            null, null, null, PrescriptionRule(activity, slot), null, null, null, null, null, [reason]);

    private static StrengthPrescriptionRole RoleFor(StrengthTemplateSlot slot,
        WorkoutActivityType activity) => activity switch
    {
        WorkoutActivityType.HighVolumeStrength => slot.HighVolumeRole,
        WorkoutActivityType.StrengthHighIntensity => slot.HighIntensityRole,
        _ => slot.GeneralRole
    };

    private static bool IsHeavyMain(StrengthTemplateSlot slot, WorkoutActivityType activity) =>
        activity == WorkoutActivityType.StrengthHighIntensity && slot.IsHeavyMainInHighIntensity;

    private static string PrescriptionRule(WorkoutActivityType activity, StrengthTemplateSlot slot) =>
        $"{StrengthPlanPreviewRule.PolicyId}:{StrengthPlanPreviewRule.Version}:{activity}:{slot.SlotId}";

    private static StrengthSlotPrescription SlotPrescription(
        ActivitySessionPrescription activityPrescription, StrengthTemplateSlot slot,
        WorkoutActivityType activity)
    {
        var role = RoleFor(slot, activity);
        if (role == StrengthPrescriptionRole.Main)
            return new StrengthSlotPrescription(activityPrescription.Sets, activityPrescription.MinimumRepetitions,
                activityPrescription.MaximumRepetitions, activityPrescription.RepetitionsInReserve,
                activityPrescription.RestSeconds);

        return activity switch
        {
            WorkoutActivityType.HighVolumeStrength => new StrengthSlotPrescription(
                ActivityPrescriptionRule.HighVolumeAccessorySets,
                ActivityPrescriptionRule.AccessoryMinimumRepetitions,
                ActivityPrescriptionRule.AccessoryMaximumRepetitions,
                ActivityPrescriptionRule.InitialRepetitionsInReserve,
                ActivityPrescriptionRule.AccessoryRestSeconds),
            WorkoutActivityType.StrengthHighIntensity => new StrengthSlotPrescription(
                ActivityPrescriptionRule.StrengthHighIntensityAccessorySets,
                ActivityPrescriptionRule.AccessoryMinimumRepetitions,
                ActivityPrescriptionRule.AccessoryMaximumRepetitions,
                ActivityPrescriptionRule.InitialRepetitionsInReserve,
                ActivityPrescriptionRule.AccessoryRestSeconds),
            _ => new StrengthSlotPrescription(
                ActivityPrescriptionRule.GeneralStrengthWorkingSets,
                ActivityPrescriptionRule.GeneralStrengthAccessoryMinimumRepetitions,
                ActivityPrescriptionRule.GeneralStrengthAccessoryMaximumRepetitions,
                ActivityPrescriptionRule.InitialRepetitionsInReserve,
                ActivityPrescriptionRule.AccessoryRestSeconds)
        };
    }

    private static string SelectionReason(StrengthExerciseCatalogEntry entry,
        HashSet<int> preferredIds, StrengthTemplateSelectionInput input,
        StrengthTemplateSlot slot, IReadOnlyDictionary<int, int> goalPriorities)
    {
        var id = entry.Candidate.ExerciseId;
        if (input.ExistingAcceptedExerciseIds?.Contains(id) == true)
            return "CompatibleAcceptedPlanSelection";
        if (input.FamiliarExerciseIds.Contains(id) && input.UsablePerformanceHistoryExerciseIds.Contains(id))
            return "FamiliarWithUsablePerformanceHistory";
        if (input.UsablePerformanceHistoryExerciseIds.Contains(id))
            return "UsablePerformanceHistory";
        if (input.FamiliarExerciseIds.Contains(id))
            return "FamiliarExercise";
        if (preferredIds.Contains(id))
            return "EligibleUserPreference";
        if (slot.MovementSlot == StrengthTemplateSlotId.GoalSpecificAccessory &&
            goalPriorities.ContainsKey(id))
            return "GoalPriorityCatalogMatch";
        return "StableCatalogExerciseIdTieBreak";
    }
}
