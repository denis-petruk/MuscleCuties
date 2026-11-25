
namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

/// <summary>The reviewed exclusion catalog: which exercise a restriction must exclude, read by the planner.</summary>
public sealed class PostgresReviewedExerciseExclusionCatalogV020Store(
    PostgresDbContext dbContext) : IReviewedExerciseExclusionCatalogV020Store
{
    /// <summary>The reviewed exclusions the database holds.</summary>
    public async Task<IReadOnlyList<ReviewedExerciseExclusionOptionV020>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        var definitions = await dbContext.WorkoutExerciseDefinitions.AsNoTracking()
            .OrderBy(definition => definition.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var reviewedSlots = ReviewedExerciseCatalogV020ReviewMap.MapSlots(definitions);
        var reviewedExerciseIds = reviewedSlots.AllowedIds.Values
            .SelectMany(exerciseIds => exerciseIds)
            .ToHashSet();
        var entries = definitions.Select(definition => (definition.Id, definition.Name));
        return ReviewedExerciseExclusionCatalogV020Mapper.FromCatalog(entries, reviewedExerciseIds);
    }
}
