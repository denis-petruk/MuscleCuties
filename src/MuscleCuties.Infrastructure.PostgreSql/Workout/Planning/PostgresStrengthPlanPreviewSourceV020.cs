using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

public sealed class PostgresStrengthPlanPreviewSourceV020(PostgresDbContext dbContext)
    : IStrengthPlanPreviewSourceV020
{
    private const string ReviewResourceName =
        "MuscleCuties.Infrastructure.PostgreSql.Workout.Planning.ReviewData.ReviewedExerciseCatalogV020.json";

    public Task<StrengthPlanPreviewSourceV020?> LoadAsync(Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);

            var profile = await dbContext.UserProfiles.AsNoTracking()
                .SingleOrDefaultAsync(row => row.UserId == userId, cancellationToken)
                .ConfigureAwait(false);
            if (profile is null || !Enum.TryParse<UserGoal>(profile.Goal, true, out var goal) ||
                !Enum.IsDefined(goal) ||
                !Enum.TryParse<Equipment>(profile.EquipmentLevel, true, out var equipment) ||
                !Enum.IsDefined(equipment))
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return null;
            }

            var definitions = await dbContext.WorkoutExerciseDefinitions.AsNoTracking()
                .Include(row => row.MuscleContributions)
                .ThenInclude(row => row.MuscleGroup)
                .OrderBy(row => row.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
            var reviewedSlots = MapReviewedSlots(definitions);
            var preferences = await dbContext.UserExercisePreferences.AsNoTracking()
                .Where(row => row.UserId == userId)
                .OrderBy(row => row.CreatedAt).ThenBy(row => row.Id)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var injuries = await dbContext.WorkoutInjuryLogs.AsNoTracking()
                .Where(row => row.UserId == userId && row.Status != "Cleared")
                .Select(row => new { row.SiteFlag, row.Pain })
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var logs = await dbContext.WorkoutExerciseLogs.AsNoTracking()
                .Where(row => row.UserId == userId && row.CompletedSets > 0 && row.CompletedReps > 0)
                .Select(row => new { row.ExerciseId, row.WorkoutLogId, row.WeightKg })
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var activePlanId = await dbContext.WorkoutPlans.AsNoTracking()
                .Where(row => row.UserId == userId && row.IsActive)
                .OrderByDescending(row => row.CreatedAt)
                .Select(row => (Guid?)row.Id)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            var acceptedExerciseIds = new HashSet<int>();
            if (activePlanId is { } planId)
            {
                var dayIds = await dbContext.WorkoutDays.AsNoTracking()
                    .Where(row => row.UserId == userId && row.WorkoutPlanId == planId)
                    .Select(row => row.Id).ToArrayAsync(cancellationToken).ConfigureAwait(false);
                acceptedExerciseIds = (await dbContext.WorkoutDayExercises.AsNoTracking()
                    .Where(row => row.UserId == userId && dayIds.Contains(row.WorkoutDayId))
                    .Select(row => row.ExerciseId).ToListAsync(cancellationToken).ConfigureAwait(false))
                    .ToHashSet();
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new StrengthPlanPreviewSourceV020(
                goal, equipment, profile.SessionDurationMinutes,
                injuries.Aggregate(InjuryFlag.None, (flags, injury) =>
                    flags | (InjuryFlag)injury.SiteFlag),
                injuries.Any(injury => injury.Pain > 0),
                definitions.Select(WorkoutExerciseV020CatalogMapper.Map).ToArray(),
                preferences.GroupBy(row => row.OriginalExerciseId)
                    .ToDictionary(group => group.Key, group => group.Last().PreferredExerciseId),
                acceptedExerciseIds,
                logs.GroupBy(row => row.ExerciseId)
                    .ToDictionary(group => group.Key,
                        group => group.Select(row => row.WorkoutLogId).Distinct().Count()),
                logs.Where(row => row.WeightKg is > 0)
                    .Select(row => row.ExerciseId).ToHashSet(),
                definitions.ToDictionary(row => row.Id, row => (int)row.SecondsPerRep),
                definitions.Where(row => row.MuscleContributions.Any())
                    .ToDictionary(row => row.Id, row => row.MuscleContributions
                        .OrderByDescending(contribution => contribution.Fraction)
                        .ThenBy(contribution => contribution.MuscleGroupId)
                        .First().MuscleGroup?.Name ?? string.Empty),
                reviewedSlots.AllowedIds,
                reviewedSlots.Conditions);
        });

    private static (IReadOnlyDictionary<string, IReadOnlySet<int>> AllowedIds,
        IReadOnlyDictionary<string, string> Conditions) MapReviewedSlots(
        IReadOnlyList<MuscleCuties.Infrastructure.PostgreSql.Models.Catalog.WorkoutExerciseDefinition> definitions)
    {
        using var stream = typeof(PostgresStrengthPlanPreviewSourceV020).Assembly
            .GetManifestResourceStream(ReviewResourceName);
        if (stream is null)
            return (new Dictionary<string, IReadOnlySet<int>>(), new Dictionary<string, string>());
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        if (root.GetProperty("policyVersion").GetString() != WorkoutTrainingPolicyV020.Version ||
            root.GetProperty("templateVersion").GetString() != StrengthTemplateCatalogV020.Version)
            return (new Dictionary<string, IReadOnlySet<int>>(), new Dictionary<string, string>());

        var bySeedCode = definitions.ToDictionary(row => row.SeedCode, StringComparer.Ordinal);
        var databaseIdBySourceId = new Dictionary<int, int>();
        foreach (var entry in root.GetProperty("exercises").EnumerateArray())
        {
            var seedCode = entry.GetProperty("seedCode").GetString()!;
            if (!bySeedCode.TryGetValue(seedCode, out var row) ||
                row.Name != entry.GetProperty("expectedName").GetString() ||
                row.Pattern.ToString() != entry.GetProperty("expectedPattern").GetString() ||
                (int)row.Required != entry.GetProperty("expectedRequiredEquipment").GetInt32() ||
                row.SetupSeconds != entry.GetProperty("expectedSetupSeconds").GetInt32() ||
                row.SecondsPerRep != entry.GetProperty("expectedSecondsPerRep").GetInt32() ||
                (int)row.Contraindications != entry.GetProperty("expectedContraindications").GetInt32())
                continue;
            databaseIdBySourceId[entry.GetProperty("sourceExerciseId").GetInt32()] = row.Id;
        }
        var allowed = new Dictionary<string, IReadOnlySet<int>>(StringComparer.Ordinal);
        var conditions = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var slot in root.GetProperty("templateSlots").EnumerateArray())
        {
            var fullId = slot.GetProperty("templateIdAndVersion").GetString() ?? string.Empty;
            var parts = fullId.Split(" / ", StringSplitOptions.None);
            if (parts.Length != 2 || parts[1] != StrengthTemplateCatalogV020.Version)
                continue;
            var slotId = slot.GetProperty("movementSlot").GetString() ?? string.Empty;
            var definition = StrengthTemplateCatalogV020.Templates.FirstOrDefault(item =>
                item.TemplateId == parts[0]);
            var slotDefinition = definition?.Slots.FirstOrDefault(item => item.SlotId == slotId);
            if (slotDefinition is null ||
                slot.GetProperty("requiredOrOptional").GetString() !=
                (slotDefinition.IsRequired ? "Required" : "Optional") ||
                slot.GetProperty("roleByStyle").GetString() !=
                $"General={slotDefinition.GeneralRole}; High volume={slotDefinition.HighVolumeRole}; High intensity={(slotDefinition.IsHeavyMainInHighIntensity ? "Heavy main" : slotDefinition.HighIntensityRole.ToString())}")
                continue;
            var candidateSourceIds = slot.GetProperty("candidateSourceExerciseIds").EnumerateArray()
                .Select(value => value.GetInt32()).ToArray();
            if (candidateSourceIds.Any(id => !databaseIdBySourceId.ContainsKey(id)))
                continue;
            var key = $"{parts[0]}|{slotId}";
            allowed[key] = candidateSourceIds.Select(id => databaseIdBySourceId[id]).ToHashSet();
            conditions[key] = slot.GetProperty("selectionConstraints").GetString() ?? string.Empty;
        }
        return (allowed, conditions);
    }
}
