namespace MuscleCuties.Core.Services.ExerciseMedia;

public interface IExerciseDbClient
{
    Task<IReadOnlyList<ExerciseDbItem>> GetExercisesAsync(int limit = 50, int offset = 0,
        CancellationToken ct = default);

    Task<ExerciseDbItem?> GetExerciseByIdAsync(string id, CancellationToken ct = default);
    Task<Stream?> DownloadMediaStreamAsync(Uri mediaUri, CancellationToken ct = default);
}
