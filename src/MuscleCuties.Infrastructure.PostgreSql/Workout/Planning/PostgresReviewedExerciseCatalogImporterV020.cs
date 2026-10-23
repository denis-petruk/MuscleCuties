using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

public sealed record ReviewedCatalogImportDryRunV020(
    string ExerciseReviewVersion,
    string CatalogSnapshot,
    string WorkbookSha256,
    int ExercisesInReview,
    int MatchedDatabaseRecords,
    int DirectedPairsInReview,
    int TemplateSlotsInReview,
    int ExerciseRestrictionAssessmentsCompleted,
    int OwnerDirectInstructionVerifiedAssessments,
    int AutomaticSubstitutionsEnabled,
    IReadOnlyList<string> Issues)
{
    public bool CanImport => Issues.Count == 0;
}

/// <summary>Applies workbook review fields and the separately attributed owner restriction decision.</summary>
public sealed class PostgresReviewedExerciseCatalogImporterV020(PostgresDbContext context)
{
    private const int ExpectedExerciseCount = 59;
    private const int ExpectedDirectedPairCount = 160;
    private const int ExpectedTemplateSlotCount = 52;
    private const string ExpectedExerciseReviewVersion = "MC-EXERCISE-REVIEW-2026-10-23";
    private const string ExpectedSubstitutionReviewVersion = "MC-SUBSTITUTIONS-2026-10-23";
    private const string ExpectedCatalogSnapshot = "REPO-SEED-SNAPSHOT-2026-10-23";
    private const string ReviewEvidenceMigration = "20261023160000_AddWorkoutExerciseReviewEvidenceV020";

    public async Task<ReviewedCatalogImportDryRunV020> DryRunAsync(string manifestPath,
        CancellationToken cancellationToken = default)
    {
        using var document = await ReadManifestAsync(manifestPath, cancellationToken).ConfigureAwait(false);
        var pending = await context.Database.GetPendingMigrationsAsync(cancellationToken)
            .ConfigureAwait(false);
        var query = context.WorkoutExerciseDefinitions.AsNoTracking();
        var definitions = pending.Contains(ReviewEvidenceMigration, StringComparer.Ordinal)
            ? await query.Select(row => new WorkoutExerciseDefinition
                {
                    Id = row.Id,
                    SeedCode = row.SeedCode,
                    Name = row.Name,
                    Pattern = row.Pattern,
                    Required = row.Required,
                    SetupSeconds = row.SetupSeconds,
                    SecondsPerRep = row.SecondsPerRep,
                    Contraindications = row.Contraindications
                }).ToListAsync(cancellationToken).ConfigureAwait(false)
            : await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        return InspectSnapshot(document.RootElement, definitions);
    }

    public async Task<ReviewedCatalogImportDryRunV020> ApplyAsync(string manifestPath,
        CancellationToken cancellationToken = default) =>
        await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            using var document = await ReadManifestAsync(manifestPath, cancellationToken).ConfigureAwait(false);
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
            var definitions = await context.WorkoutExerciseDefinitions
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var result = InspectSnapshot(document.RootElement, definitions);
            if (!result.CanImport)
                throw new InvalidOperationException("Review import blocked by dry-run catalog or review mismatch.");

            var bySeedCode = definitions.ToDictionary(row => row.SeedCode, StringComparer.Ordinal);
            var databaseIdBySourceId = document.RootElement.GetProperty("exercises").EnumerateArray()
                .ToDictionary(entry => entry.GetProperty("sourceExerciseId").GetInt32(),
                    entry => bySeedCode[String(entry, "seedCode")].Id);
            foreach (var entry in document.RootElement.GetProperty("exercises").EnumerateArray())
            {
                var record = bySeedCode[entry.GetProperty("seedCode").GetString()!];
                record.TrainingCatalogVersionV020 = result.CatalogSnapshot;
                record.SupportedExperiencesV020Json = entry.GetProperty("supportedExperiences").GetRawText();
                record.DemandTagsV020Json = entry.GetProperty("demandTags").GetRawText();
                record.DemandTagsReviewedV020 = true;
                // The later owner clearance is tracked separately from the workbook's recorded status.
                // Existing anatomical contraindication flags remain intact and authoritative.
                record.RestrictionConflictTagsV020Json = "[]";
                record.RestrictionMappingsReviewedV020 = true;
                record.SupportedTimingMethodsV020Json = entry.GetProperty("supportedTimingMethods").GetRawText();
                // Directed pair conditions are retained as evidence; no automatic replacement is authorized.
                record.ReviewedSubstituteExerciseIdsV020Json = "[]";
                record.ReviewEvidenceV020Json = EvidenceJson(document.RootElement, entry,
                    databaseIdBySourceId);
            }
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        });

    public static ReviewedCatalogImportDryRunV020 InspectSnapshot(JsonElement root,
        IReadOnlyList<WorkoutExerciseDefinition> definitions)
    {
        var issues = new List<string>();
        var reviewVersion = String(root, "exerciseReviewVersion");
        var substitutionVersion = String(root, "directedSubstitutionReviewVersion");
        var policyVersion = String(root, "policyVersion");
        var templateVersion = String(root, "templateVersion");
        var catalogSnapshot = String(root, "catalogSnapshot");
        var workbookSha = String(root, "workbookSha256");
        if (reviewVersion != ExpectedExerciseReviewVersion ||
            substitutionVersion != ExpectedSubstitutionReviewVersion ||
            policyVersion != WorkoutTrainingPolicyV020.Version ||
            templateVersion != StrengthTemplateCatalogV020.Version ||
            catalogSnapshot != ExpectedCatalogSnapshot ||
            workbookSha.Length != 64 || !workbookSha.All(Uri.IsHexDigit))
            issues.Add("ReviewVersionOrSourceHashMismatch");
        if (string.IsNullOrWhiteSpace(String(root, "reviewerNameAsProvided")) ||
            string.IsNullOrWhiteSpace(String(root, "credentialsAndScopeAsProvided")) ||
            string.IsNullOrWhiteSpace(String(root, "approvalReferenceAsProvided")))
            issues.Add("ReviewerAttributionOrScopeMissing");
        var ownerAssertion = root.GetProperty("ownerRestrictionAssessmentAssertion");
        var directVerification = root.GetProperty("ownerDirectVerification");
        if (string.IsNullOrWhiteSpace(String(ownerAssertion, "source")) ||
            string.IsNullOrWhiteSpace(String(ownerAssertion, "scope")) ||
            !String(ownerAssertion, "reviewerCredentialVerification").Contains(
                "confirmed by owner direct instruction", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(String(directVerification, "source")) ||
            string.IsNullOrWhiteSpace(String(directVerification, "scope")) ||
            string.IsNullOrWhiteSpace(String(root, "assessmentScopeAndLimits")))
            issues.Add("OwnerRestrictionAssessmentAssertionMissing");
        var entries = root.GetProperty("exercises").EnumerateArray().ToArray();
        var pairs = entries.Sum(entry => entry.GetProperty("directedSubstitutionReviews")
            .GetArrayLength());
        var slots = root.GetProperty("templateSlots").EnumerateArray().ToArray();
        if (entries.Length != ExpectedExerciseCount || pairs != ExpectedDirectedPairCount ||
            slots.Length != ExpectedTemplateSlotCount ||
            entries.Select(entry => entry.GetProperty("sourceExerciseId").GetInt32()).Distinct().Count() != entries.Length ||
            entries.Select(entry => String(entry, "seedCode")).Distinct(StringComparer.Ordinal).Count() != entries.Length)
            issues.Add("ReviewCoverageOrDuplicateIdentityMismatch");

        var bySeedCode = definitions.GroupBy(row => row.SeedCode, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        var sourceIds = entries.Select(entry => entry.GetProperty("sourceExerciseId").GetInt32()).ToHashSet();
        var directedKeys = entries.SelectMany(entry => entry.GetProperty("directedSubstitutionReviews")
                .EnumerateArray().Select(pair => (Source: entry.GetProperty("sourceExerciseId").GetInt32(),
                    Target: pair.GetProperty("targetSourceExerciseId").GetInt32())))
            .ToArray();
        if (directedKeys.Distinct().Count() != directedKeys.Length)
            issues.Add("DuplicateDirectedSubstitutionRelationship");
        var matched = 0;
        foreach (var entry in entries)
        {
            var sourceId = entry.GetProperty("sourceExerciseId").GetInt32();
            if (!bySeedCode.TryGetValue(String(entry, "seedCode"), out var row))
            {
                issues.Add($"DatabaseExerciseMissing:{sourceId}");
                continue;
            }
            if (row.Name != String(entry, "expectedName") ||
                row.Pattern.ToString() != String(entry, "expectedPattern") ||
                (int)row.Required != entry.GetProperty("expectedRequiredEquipment").GetInt32() ||
                row.SetupSeconds != entry.GetProperty("expectedSetupSeconds").GetInt32() ||
                row.SecondsPerRep != entry.GetProperty("expectedSecondsPerRep").GetInt32() ||
                (int)row.Contraindications != entry.GetProperty("expectedContraindications").GetInt32())
            {
                issues.Add($"DatabaseExerciseChanged:{sourceId}");
                continue;
            }
            if (row.ReviewEvidenceV020Json is { Length: > 0 } prior &&
                !prior.Contains(workbookSha, StringComparison.OrdinalIgnoreCase))
                issues.Add($"DifferentReviewAlreadyStored:{sourceId}");
            matched++;
            if (String(entry, "reviewDecision") != "Approved with conditions" ||
                String(entry, "restrictionAssessmentStatus") != "Assessed" ||
                !String(entry, "restrictionAssessmentSource").Contains(
                    "Owner direct instruction", StringComparison.Ordinal) ||
                String(entry, "effectiveRestrictionAssessmentStatus") !=
                    "OwnerAssessedClearanceSubjectToExistingContraindications" ||
                !ValidEnumArray<ActivityExperienceV020>(entry, "supportedExperiences", allowEmpty: false) ||
                !ValidEnumArray<ActivityDemandTag>(entry, "demandTags", allowEmpty: true) ||
                !ValidEnumArray<ExerciseTimingMethod>(entry, "supportedTimingMethods", allowEmpty: false) ||
                !Enum.IsDefined(row.Pattern))
                issues.Add($"ReviewDecisionOrMetadataInvalid:{sourceId}");
            foreach (var pair in entry.GetProperty("directedSubstitutionReviews").EnumerateArray())
            {
                var targetId = pair.GetProperty("targetSourceExerciseId").GetInt32();
                var category = String(pair, "category");
                if (targetId == sourceId || !sourceIds.Contains(targetId) ||
                    category is not ("CloseProgrammingAlternative" or "ManualProgrammingAlternative" or
                        "RejectedAutomaticSubstitution") ||
                    (category == "RejectedAutomaticSubstitution") !=
                    (String(pair, "reviewDecision") == "Rejected") ||
                    string.IsNullOrWhiteSpace(String(pair, "conditions")))
                    issues.Add($"DirectedSubstitutionInvalid:{sourceId}:{targetId}");
            }
        }
        foreach (var slot in slots)
        {
            var fullTemplateId = String(slot, "templateIdAndVersion");
            var templateParts = fullTemplateId.Split(" / ", StringSplitOptions.None);
            var template = templateParts.Length == 2 &&
                           templateParts[1] == StrengthTemplateCatalogV020.Version
                ? StrengthTemplateCatalogV020.Templates.FirstOrDefault(item =>
                    item.TemplateId == templateParts[0])
                : null;
            var definition = template?.Slots.FirstOrDefault(item =>
                item.SlotId == String(slot, "movementSlot"));
            var candidateIds = slot.GetProperty("candidateSourceExerciseIds").EnumerateArray()
                .Select(value => value.GetInt32()).ToArray();
            if (definition is null ||
                String(slot, "requiredOrOptional") != (definition.IsRequired ? "Required" : "Optional") ||
                String(slot, "roleByStyle") !=
                $"General={definition.GeneralRole}; High volume={definition.HighVolumeRole}; High intensity={(definition.IsHeavyMainInHighIntensity ? "Heavy main" : definition.HighIntensityRole.ToString())}" ||
                !candidateIds.All(sourceIds.Contains) ||
                string.IsNullOrWhiteSpace(String(slot, "selectionConstraints")))
                issues.Add($"TemplateSlotReviewInvalid:{fullTemplateId}:{String(slot, "movementSlot")}");
        }
        if (root.GetProperty("automaticSubstitutionsEnabled").GetBoolean())
            issues.Add("AutomaticSubstitutionsMustRemainDisabled");
        return new ReviewedCatalogImportDryRunV020(reviewVersion, catalogSnapshot, workbookSha,
            entries.Length, matched, pairs, slots.Length, entries.Length, entries.Length, 0,
            issues.Distinct(StringComparer.Ordinal).OrderBy(issue => issue, StringComparer.Ordinal).ToArray());
    }

    private static bool ValidEnumArray<TEnum>(JsonElement entry, string property, bool allowEmpty)
        where TEnum : struct, Enum
    {
        var values = entry.GetProperty(property).EnumerateArray().ToArray();
        return (allowEmpty || values.Length > 0) && values.All(value =>
            Enum.TryParse<TEnum>(value.GetString(), false, out var parsed) && Enum.IsDefined(parsed));
    }

    private static string EvidenceJson(JsonElement root, JsonElement exercise,
        IReadOnlyDictionary<int, int> databaseIdBySourceId) => JsonSerializer.Serialize(new
    {
        exerciseReviewVersion = String(root, "exerciseReviewVersion"),
        directedSubstitutionReviewVersion = String(root, "directedSubstitutionReviewVersion"),
        policyVersion = String(root, "policyVersion"),
        templateVersion = String(root, "templateVersion"),
        catalogSnapshot = String(root, "catalogSnapshot"),
        workbookSha256 = String(root, "workbookSha256"),
        reviewerNameAsProvided = String(root, "reviewerNameAsProvided"),
        credentialsAndScopeAsProvided = String(root, "credentialsAndScopeAsProvided"),
        approvalReferenceAsProvided = String(root, "approvalReferenceAsProvided"),
        assessmentScopeAndLimits = String(root, "assessmentScopeAndLimits"),
        ownerRestrictionAssessmentAssertion = root.GetProperty("ownerRestrictionAssessmentAssertion"),
        ownerDirectVerification = root.GetProperty("ownerDirectVerification"),
        resolvedDirectedSubstitutionReviews = exercise.GetProperty("directedSubstitutionReviews")
            .EnumerateArray().Select(pair => new
            {
                targetExerciseId = databaseIdBySourceId[pair.GetProperty("targetSourceExerciseId").GetInt32()],
                category = String(pair, "category"),
                conditions = String(pair, "conditions"),
                reference = String(pair, "reference")
            }).ToArray(),
        exercise
    });

    private static string String(JsonElement value, string property) =>
        value.GetProperty(property).GetString() ?? string.Empty;

    private static async Task<JsonDocument> ReadManifestAsync(string path, CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonDocument.Parse(bytes);
    }
}
