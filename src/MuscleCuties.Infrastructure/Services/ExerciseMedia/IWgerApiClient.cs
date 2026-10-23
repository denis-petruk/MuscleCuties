namespace MuscleCuties.Core.Services.ExerciseMedia;

public interface IWgerApiClient
{
    Task<IReadOnlyList<WgerExerciseInfo>> GetExercisesAsync(int limit = 50, int offset = 0,
        CancellationToken ct = default);

    Task<IReadOnlyList<WgerMuscleSvgResult>> GetMuscleSvgsAsync(CancellationToken ct = default);
    Task<Stream?> DownloadMediaStreamAsync(Uri mediaUri, CancellationToken ct = default);
}
