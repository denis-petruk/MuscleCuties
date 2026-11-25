namespace MuscleCuties.UseCases.Features.Workout.Planning;

/// <summary>An exercise the reviewer excluded, with the reason shown.</summary>
public sealed record ReviewedExerciseExclusionOptionV020(int ExerciseId, string Name);

/// <summary>Maps the reviewed exclusions onto the options the client shows.</summary>
public static class ReviewedExerciseExclusionCatalogV020Mapper
{
    /// <summary>The reviewed exclusions the catalog offers.</summary>
    public static IReadOnlyList<ReviewedExerciseExclusionOptionV020> FromCatalog(
        IEnumerable<(int ExerciseId, string Name)> catalog,
        IReadOnlySet<int> reviewedExerciseIds) =>
        catalog
            .Where(exercise => reviewedExerciseIds.Contains(exercise.ExerciseId))
            .GroupBy(exercise => exercise.ExerciseId)
            .Select(group => group.First())
            .OrderBy(exercise => exercise.Name, StringComparer.CurrentCulture)
            .ThenBy(exercise => exercise.ExerciseId)
            .Select(exercise => new ReviewedExerciseExclusionOptionV020(exercise.ExerciseId, exercise.Name))
            .ToArray();
}

/// <summary>The reviewed exclusion read.</summary>
public interface IReviewedExerciseExclusionCatalogV020Store
{
    Task<IReadOnlyList<ReviewedExerciseExclusionOptionV020>> LoadAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>The reviewed exclusions, which the planner must honour.</summary>
public sealed class GetReviewedExerciseExclusionCatalogV020UseCase(
    IReviewedExerciseExclusionCatalogV020Store store)
{
    /// <summary>The exclusion options for the planning screen.</summary>
    public Task<IReadOnlyList<ReviewedExerciseExclusionOptionV020>> ExecuteAsync(
        CancellationToken cancellationToken = default) =>
        store.LoadAsync(cancellationToken);
}
