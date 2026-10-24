namespace MuscleCuties.UseCases.Features.Workout.Planning;

public sealed record ReviewedExerciseExclusionOptionV020(int ExerciseId, string Name);

public static class ReviewedExerciseExclusionCatalogV020Mapper
{
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

public interface IReviewedExerciseExclusionCatalogV020Store
{
    Task<IReadOnlyList<ReviewedExerciseExclusionOptionV020>> LoadAsync(
        CancellationToken cancellationToken = default);
}

public sealed class GetReviewedExerciseExclusionCatalogV020UseCase(
    IReviewedExerciseExclusionCatalogV020Store store)
{
    public Task<IReadOnlyList<ReviewedExerciseExclusionOptionV020>> ExecuteAsync(
        CancellationToken cancellationToken = default) =>
        store.LoadAsync(cancellationToken);
}
