namespace MuscleCuties.Core.Services.ExerciseMedia;

public sealed class ExerciseSyncResult
{
    public int TotalIngested { get; set; }
    public int TotalUpdated { get; set; }
    public int TotalMediaDownloaded { get; set; }
    public bool IsSuccess { get; set; }
    public string? Message { get; set; }
}

public sealed class ExerciseMediaDownloadResult
{
    public int VectorSvgsDownloaded { get; set; }
    public int LottieAnimationsDownloaded { get; set; }
    public int FailedCount { get; set; }
}

public interface IExerciseMediaSyncService
{
    Task<ExerciseSyncResult> SyncExerciseCatalogAsync(CancellationToken ct = default);
    Task<ExerciseMediaDownloadResult> DownloadMissingMediaAsync(CancellationToken ct = default);
    Task<string?> EnsureExerciseVisualMediaAsync(int exerciseId, CancellationToken ct = default);
}
