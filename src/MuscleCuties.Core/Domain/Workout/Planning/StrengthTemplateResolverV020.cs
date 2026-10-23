using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public sealed record StrengthExerciseCatalogEntryV020(
    string Name,
    ReviewedExerciseCandidate Candidate,
    IReadOnlyList<string> MetadataGaps);

public sealed record StrengthTemplateSelectionInputV020(
    StrengthTemplateDefinitionV020 Template,
    WorkoutActivityType Activity,
    UserGoal Goal,
    ActivityPrescriptionContextV020 PrescriptionContext,
    IReadOnlyList<StrengthExerciseCatalogEntryV020> Catalog,
    ExerciseEligibilityContext EligibilityContext,
    IReadOnlyDictionary<int, int> PreferredExerciseByOriginalId,
    IReadOnlySet<int> FamiliarExerciseIds,
    IReadOnlySet<int> UsablePerformanceHistoryExerciseIds,
    IReadOnlySet<int>? ExistingAcceptedExerciseIds = null,
    IReadOnlyDictionary<int, int>? GoalPriorityByExerciseId = null,
    IReadOnlyDictionary<int, int>? CompletedExerciseExposureCounts = null,
    IReadOnlySet<int>? KnownManageableLoadExerciseIds = null,
    IReadOnlySet<int>? UserConfirmedFamiliarityExerciseIds = null);

public sealed record StrengthSlotSelectionV020(
    string SlotId,
    StrengthTemplateSlotIdV020 MovementSlot,
    bool IsRequired,
    StrengthPrescriptionRoleV020 PrescriptionRole,
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

public sealed record StrengthSlotPrescriptionV020(
    int? Sets,
    int? MinimumRepetitions,
    int? MaximumRepetitions,
    int? RepetitionsInReserve,
    int? RestSeconds);

public sealed record StrengthTemplateResolutionV020(
    string TemplateId,
    string TemplateVersion,
    string TrainingPolicyVersion,
    string RulesetVersion,
    string Status,
    IReadOnlyList<StrengthSlotSelectionV020> Slots,
    IReadOnlyList<string> LimitationCodes);

/// <summary>Resolves slots deterministically after policy restrictions and user exclusions are applied.</summary>
public static class StrengthTemplateResolverV020
{
    private const string PolicyId = "MC-WORKOUT-POLICY";
    private const string RuleSetPrefix = "0.2.0";
    private const string MissingCatalogMetadata = "CatalogEligibilityMetadataMissing";
    private const string NoEligibleCandidate = "NoEligibleCatalogExercise";
    private const string RequiredSlotUnresolved = "RequiredMovementSlotUnresolved";
    private const string OptionalSlotOmittedByLimit = "OptionalSlotOmittedByExerciseLimit";

    public static StrengthTemplateResolutionV020 Resolve(
        StrengthTemplateSelectionInputV020? input,
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
        ActivityPrescriptionResultV020? activityPrescription = null;
        if (input.Activity != WorkoutActivityType.StrengthHighIntensity)
        {
            activityPrescription = ActivityPrescriptionRuleV020.Evaluate(input.PrescriptionContext);
            if (activityPrescription.Status != ActivityPrescriptionStatusV020.Eligible ||
                activityPrescription.Sessions.Count == 0)
                return new StrengthTemplateResolutionV020(input.Template.TemplateId, input.Template.Version,
                    WorkoutTrainingPolicyV020.Version, WorkoutTrainingPolicyV020.RulesetVersion,
                    activityPrescription.Status.ToString(), [], activityPrescription.Reasons);
        }
        else if (!input.PrescriptionContext.IsExplicitlySelected ||
                 input.PrescriptionContext.HasCurrentRestrictionOrSymptom)
        {
            return new StrengthTemplateResolutionV020(input.Template.TemplateId, input.Template.Version,
                WorkoutTrainingPolicyV020.Version, WorkoutTrainingPolicyV020.RulesetVersion,
                input.PrescriptionContext.HasCurrentRestrictionOrSymptom
                    ? ActivityPrescriptionStatusV020.BlockedByRestriction.ToString()
                    : ActivityPrescriptionStatusV020.NotSelected.ToString(), [],
                [input.PrescriptionContext.HasCurrentRestrictionOrSymptom
                    ? "RestrictionOrSymptomRequiresReview" : "ActivityNotSelected"]);
        }
        else if (input.PrescriptionContext.Experience != ActivityExperienceV020.Established ||
                 !input.PrescriptionContext.IsEstablishedResistanceHistory)
        {
            return new StrengthTemplateResolutionV020(input.Template.TemplateId, input.Template.Version,
                WorkoutTrainingPolicyV020.Version, WorkoutTrainingPolicyV020.RulesetVersion,
                ActivityPrescriptionStatusV020.OfferGeneralStrength.ToString(), [],
                ["HeavyStrengthRequiresEstablishedResistanceHistory"]);
        }

        var policyExerciseMaximum = input.Activity switch
        {
            WorkoutActivityType.HighVolumeStrength => WorkoutTrainingPolicyV020.HighVolumeMaximumWorkingExercisesPerSession,
            WorkoutActivityType.StrengthHighIntensity => WorkoutTrainingPolicyV020.StrengthHighIntensityMaximumWorkingExercisesPerSession,
            _ => int.MaxValue
        };
        var effectiveExerciseMaximum = Math.Min(maximumWorkingExercises, policyExerciseMaximum);
        var requiredSlotCount = input.Template.Slots.Count(slot => slot is { IsRequired: true });
        var heavyMainCount = input.Template.Slots.Count(slot => slot is { IsRequired: true, IsHeavyMainInHighIntensity: true });
        if (requiredSlotCount > effectiveExerciseMaximum ||
            input.Activity == WorkoutActivityType.StrengthHighIntensity &&
            heavyMainCount > WorkoutTrainingPolicyV020.StrengthHighIntensityMaximumHeavyMainMovementsPerSession)
            return new StrengthTemplateResolutionV020(input.Template.TemplateId, input.Template.Version,
                WorkoutTrainingPolicyV020.Version, WorkoutTrainingPolicyV020.RulesetVersion,
                "Infeasible", [], [requiredSlotCount > effectiveExerciseMaximum
                    ? "RequiredCoverageExceedsStyleExerciseMaximum"
                    : "TemplateExceedsHighIntensityHeavyMainMaximum"]);

        var chosen = new List<StrengthSlotSelectionV020>();
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

            var matched = input.Catalog
                .Where(entry => entry is not null && entry.Candidate is not null &&
                                slot.AcceptedPatterns.Contains(entry.Candidate.Pattern))
                .ToArray();
            var metadataGaps = matched.SelectMany(entry => entry.MetadataGaps ?? [])
                .Distinct(StringComparer.Ordinal).OrderBy(code => code, StringComparer.Ordinal).ToArray();

            var eligibility = ExerciseEligibilityV02Rule.Filter(
                matched.Select(entry => entry.Candidate).ToArray(), input.EligibilityContext);
            var completeMetadataIds = matched.Where(entry => entry.MetadataGaps is { Count: 0 })
                .Select(entry => entry.Candidate.ExerciseId).ToHashSet();
            var candidatePool = eligibility.EligibleCandidates
                .Where(candidate => completeMetadataIds.Contains(candidate.ExerciseId))
                .Where(candidate => candidate.SupportedTimingMethods?.Contains(ExerciseTimingMethod.StraightSets) == true)
                .ToArray();
            var candidateIds = candidatePool.Select(candidate => candidate.ExerciseId).ToHashSet();

            var slotPreferences = input.PreferredExerciseByOriginalId
                .Where(pair => matched.Any(entry => entry.Candidate.ExerciseId == pair.Key) &&
                               candidateIds.Contains(pair.Value))
                .Select(pair => pair.Value).ToHashSet();
            var existingSelections = input.ExistingAcceptedExerciseIds ?? new HashSet<int>();
            var goalPriorities = input.GoalPriorityByExerciseId ?? new Dictionary<int, int>();
            var exposures = input.CompletedExerciseExposureCounts ?? new Dictionary<int, int>();
            var manageableLoads = input.KnownManageableLoadExerciseIds ?? new HashSet<int>();
            var confirmedFamiliarity = input.UserConfirmedFamiliarityExerciseIds ?? new HashSet<int>();
            var selection = matched
                .Where(entry => candidateIds.Contains(entry.Candidate.ExerciseId))
                .Where(entry => !IsHeavyMain(slot, input.Activity) ||
                    exposures.GetValueOrDefault(entry.Candidate.ExerciseId) >=
                        WorkoutTrainingPolicyV020.StrengthHighIntensityRequiredExerciseExposures &&
                    manageableLoads.Contains(entry.Candidate.ExerciseId) &&
                    confirmedFamiliarity.Contains(entry.Candidate.ExerciseId))
                .OrderByDescending(entry => existingSelections.Contains(entry.Candidate.ExerciseId))
                .ThenByDescending(entry => input.FamiliarExerciseIds.Contains(entry.Candidate.ExerciseId) &&
                                           input.UsablePerformanceHistoryExerciseIds.Contains(entry.Candidate.ExerciseId))
                .ThenByDescending(entry => input.UsablePerformanceHistoryExerciseIds.Contains(entry.Candidate.ExerciseId))
                .ThenByDescending(entry => input.FamiliarExerciseIds.Contains(entry.Candidate.ExerciseId))
                .ThenByDescending(entry => slotPreferences.Contains(entry.Candidate.ExerciseId))
                .ThenByDescending(entry => slot.MovementSlot == StrengthTemplateSlotIdV020.GoalSpecificAccessory
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
                    codes = codes.Append("HighIntensityMainNeedsFourExposuresKnownLoadAndFamiliarity").ToArray();
                var prescription = activityPrescription?.Sessions.Count > 0
                    ? SlotPrescription(activityPrescription.Sessions[0], slot, input.Activity)
                    : new StrengthSlotPrescriptionV020(null, null, null, null, null);
                chosen.Add(new StrengthSlotSelectionV020(slot.SlotId, slot.MovementSlot, slot.IsRequired,
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
                : new StrengthSlotPrescriptionV020(null, null, null, null, null);
            chosen.Add(new StrengthSlotSelectionV020(slot.SlotId, slot.MovementSlot, slot.IsRequired,
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
                return new StrengthTemplateResolutionV020(input.Template.TemplateId, input.Template.Version,
                    WorkoutTrainingPolicyV020.Version, WorkoutTrainingPolicyV020.RulesetVersion,
                    ActivityPrescriptionStatusV020.OfferGeneralStrength.ToString(),
                    Array.AsReadOnly(chosen.ToArray()),
                    ["EveryRequiredHighIntensityMainNeedsFourExposuresKnownLoadAndFamiliarity"]);
            var exposures = input.CompletedExerciseExposureCounts ?? new Dictionary<int, int>();
            var manageableLoads = input.KnownManageableLoadExerciseIds ?? new HashSet<int>();
            var confirmedFamiliarity = input.UserConfirmedFamiliarityExerciseIds ?? new HashSet<int>();
            var minExposures = selectedHeavyExerciseIds.Length == 0 ? 0 :
                selectedHeavyExerciseIds.Min(id => exposures.GetValueOrDefault(id));
            var context = input.PrescriptionContext with
            {
                CompletedMainExerciseExposures = minExposures,
                HasKnownManageableLoad = selectedHeavyExerciseIds.Length > 0 &&
                    selectedHeavyExerciseIds.All(manageableLoads.Contains),
                UserConfirmedCurrentFamiliarity = selectedHeavyExerciseIds.Length > 0 &&
                    selectedHeavyExerciseIds.All(confirmedFamiliarity.Contains)
            };
            activityPrescription = ActivityPrescriptionRuleV020.Evaluate(context);
            if (activityPrescription.Status != ActivityPrescriptionStatusV020.Eligible ||
                activityPrescription.Sessions.Count == 0)
                return new StrengthTemplateResolutionV020(input.Template.TemplateId, input.Template.Version,
                    WorkoutTrainingPolicyV020.Version, WorkoutTrainingPolicyV020.RulesetVersion,
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
        return new StrengthTemplateResolutionV020(input.Template.TemplateId, input.Template.Version,
            WorkoutTrainingPolicyV020.Version, WorkoutTrainingPolicyV020.RulesetVersion,
            status, Array.AsReadOnly(chosen.ToArray()),
            Array.AsReadOnly(unresolved.Distinct(StringComparer.Ordinal).ToArray()));
    }

    private static StrengthTemplateResolutionV020 Failure(string reason) =>
        new(string.Empty, StrengthTemplateCatalogV020.Version, WorkoutTrainingPolicyV020.Version,
            WorkoutTrainingPolicyV020.RulesetVersion, "Invalid", [], [reason]);

    private static StrengthSlotSelectionV020 Unselected(StrengthTemplateSlotV020 slot,
        WorkoutActivityType activity, string reason) =>
        new(slot.SlotId, slot.MovementSlot, false, RoleFor(slot, activity), IsHeavyMain(slot, activity),
            null, null, null, PrescriptionRule(activity, slot), null, null, null, null, null, [reason]);

    private static StrengthPrescriptionRoleV020 RoleFor(StrengthTemplateSlotV020 slot,
        WorkoutActivityType activity) => activity switch
    {
        WorkoutActivityType.HighVolumeStrength => slot.HighVolumeRole,
        WorkoutActivityType.StrengthHighIntensity => slot.HighIntensityRole,
        _ => slot.GeneralRole
    };

    private static bool IsHeavyMain(StrengthTemplateSlotV020 slot, WorkoutActivityType activity) =>
        activity == WorkoutActivityType.StrengthHighIntensity && slot.IsHeavyMainInHighIntensity;

    private static string PrescriptionRule(WorkoutActivityType activity, StrengthTemplateSlotV020 slot) =>
        $"{PolicyId}:{RuleSetPrefix}:{activity}:{slot.SlotId}";

    private static StrengthSlotPrescriptionV020 SlotPrescription(
        ActivitySessionPrescriptionV020 activityPrescription, StrengthTemplateSlotV020 slot,
        WorkoutActivityType activity)
    {
        var role = RoleFor(slot, activity);
        if (role == StrengthPrescriptionRoleV020.Main)
            return new StrengthSlotPrescriptionV020(activityPrescription.Sets, activityPrescription.MinimumRepetitions,
                activityPrescription.MaximumRepetitions, activityPrescription.RepetitionsInReserve,
                activityPrescription.RestSeconds);

        return activity switch
        {
            WorkoutActivityType.HighVolumeStrength => new StrengthSlotPrescriptionV020(
                WorkoutTrainingPolicyV020.HighVolumeAccessorySets,
                WorkoutTrainingPolicyV020.AccessoryMinimumRepetitions,
                WorkoutTrainingPolicyV020.AccessoryMaximumRepetitions,
                WorkoutTrainingPolicyV020.InitialRepetitionsInReserve,
                WorkoutTrainingPolicyV020.AccessoryRestSeconds),
            WorkoutActivityType.StrengthHighIntensity => new StrengthSlotPrescriptionV020(
                WorkoutTrainingPolicyV020.StrengthHighIntensityAccessorySets,
                WorkoutTrainingPolicyV020.AccessoryMinimumRepetitions,
                WorkoutTrainingPolicyV020.AccessoryMaximumRepetitions,
                WorkoutTrainingPolicyV020.InitialRepetitionsInReserve,
                WorkoutTrainingPolicyV020.AccessoryRestSeconds),
            _ => new StrengthSlotPrescriptionV020(
                WorkoutTrainingPolicyV020.GeneralStrengthWorkingSets,
                WorkoutTrainingPolicyV020.AccessoryMinimumRepetitions,
                WorkoutTrainingPolicyV020.AccessoryMaximumRepetitions,
                WorkoutTrainingPolicyV020.InitialRepetitionsInReserve,
                WorkoutTrainingPolicyV020.AccessoryRestSeconds)
        };
    }

    private static string SelectionReason(StrengthExerciseCatalogEntryV020 entry,
        IReadOnlySet<int> preferredIds, StrengthTemplateSelectionInputV020 input,
        StrengthTemplateSlotV020 slot, IReadOnlyDictionary<int, int> goalPriorities)
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
        if (slot.MovementSlot == StrengthTemplateSlotIdV020.GoalSpecificAccessory &&
            goalPriorities.ContainsKey(id))
            return "GoalPriorityCatalogMatch";
        return "StableCatalogExerciseIdTieBreak";
    }
}
