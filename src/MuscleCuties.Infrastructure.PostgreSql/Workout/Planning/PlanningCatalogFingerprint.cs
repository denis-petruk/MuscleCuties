using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

internal static class PlanningCatalogFingerprint
{
    // The migration-owned function holds SHARE table locks until this transaction ends.
    // Catalog writers need not participate in an application-level advisory lock.
    internal static Task LockCatalogAsync(PostgresDbContext context, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlRawAsync("SELECT app.lock_workout_planning_catalog()", cancellationToken);

    internal static string FromInputs(IEnumerable<WorkoutExerciseDefinition> definitions,
        IEnumerable<GoalTierWeight> weights, IEnumerable<UserExercisePreferenceRecord> preferences) =>
        PlanningInputFingerprint.Hash(new
        {
            Definitions = definitions.OrderBy(row => row.Id).Select(row => new
            {
                row.Id, row.SeedCode, row.Name, row.Pattern, row.Required, row.LongLengthLoaded,
                row.FatigueCost, row.SkillDemand, row.SetupSeconds, row.SecondsPerRep,
                row.EligibleForHiBlock, row.UnilateralDoublesTime, row.IsBodyweight,
                row.Contraindications, row.PreferredFor, row.SupineOrProne, row.ValsalvaDemand,
                row.SubstituteGroupId, row.TrainingCatalogVersionV020, row.SupportedExperiencesV020Json,
                row.DemandTagsV020Json, row.DemandTagsReviewedV020, row.RestrictionConflictTagsV020Json,
                row.RestrictionMappingsReviewedV020, row.SupportedTimingMethodsV020Json,
                row.ReviewedSubstituteExerciseIdsV020Json, row.ReviewEvidenceV020Json,
                Contributions = row.MuscleContributions.OrderBy(item => item.Id).Select(item => new
                {
                    item.Id, item.ExerciseId, item.MuscleGroupId, item.Fraction,
                    MuscleGroupName = item.MuscleGroup == null ? null : item.MuscleGroup.Name
                }).ToArray()
            }).ToArray(),
            Weights = weights.OrderBy(row => row.Id).Select(row => new
                { row.Id, row.Goal, row.MuscleGroupId, row.Tier }).ToArray(),
            Preferences = preferences.OrderBy(row => row.CreatedAt).ThenBy(row => row.Id).Select(row => new
                { row.Id, row.OriginalExerciseId, row.PreferredExerciseId, row.CreatedAt }).ToArray()
        });

    internal static async Task<string> LoadAsync(PostgresDbContext context, Guid userId,
        CancellationToken cancellationToken)
    {
        var definitions = await context.WorkoutExerciseDefinitions.AsNoTracking()
            .Include(row => row.MuscleContributions).ThenInclude(row => row.MuscleGroup)
            .OrderBy(row => row.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        var weights = await context.GoalTierWeights.AsNoTracking().OrderBy(row => row.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var preferences = await context.UserExercisePreferences.AsNoTracking().Where(row => row.UserId == userId)
            .OrderBy(row => row.CreatedAt).ThenBy(row => row.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        return FromInputs(definitions, weights, preferences);
    }
}
