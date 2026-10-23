namespace MuscleCuties.UseCases.Features.Caching;

public sealed record ActiveWorkoutSetSnapshot(
    int SetOrder,
    int Reps,
    decimal WeightKg,
    decimal? Rpe,
    bool IsCompleted);

public sealed record ActiveWorkoutExerciseSnapshot(
    string ExerciseCode,
    string DisplayName,
    IReadOnlyList<ActiveWorkoutSetSnapshot> Sets);

public sealed record ActiveWorkoutSessionSnapshot(
    Guid SessionId,
    DateOnly LocalDate,
    string ArchetypeCode,
    int ElapsedSeconds,
    IReadOnlyList<ActiveWorkoutExerciseSnapshot> Exercises,
    int ConcurrencyVersion);

public sealed record CycleStateSnapshot(
    DateOnly AsOfLocalDate,
    string CurrentPhase,
    int DayOfCycle,
    int CycleLengthDays,
    string PredictionSource,
    DateOnly? PredictedNextPeriodStart,
    int ConcurrencyVersion);

public sealed record DailyNutritionTargetsSnapshot(
    DateOnly LocalDate,
    int TargetCalories,
    decimal TargetProteinGrams,
    decimal TargetCarbsGrams,
    decimal TargetFatGrams,
    string CalculationProvenance,
    int ConcurrencyVersion);

public sealed record CachedSnapshotEnvelope<T>(
    T Payload,
    DateTimeOffset CapturedAtUtc,
    DateTimeOffset StaleAfterUtc,
    int SourceVersion)
{
    public bool IsStale(DateTimeOffset nowUtc) => nowUtc >= StaleAfterUtc;
}

public interface ISnapshotCacheKeyStore
{
    Task<byte[]?> ReadAsync(CancellationToken cancellationToken = default);
    Task WriteAsync(byte[] key, CancellationToken cancellationToken = default);
    Task RemoveAsync(CancellationToken cancellationToken = default);
}

public interface ILocalCacheService
{
    Task ActivateAccountAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task SaveActiveWorkoutSnapshotAsync(
        Guid accountId,
        ActiveWorkoutSessionSnapshot snapshot,
        DateTimeOffset capturedAtUtc,
        CancellationToken cancellationToken = default);

    Task<CachedSnapshotEnvelope<ActiveWorkoutSessionSnapshot>?> LoadActiveWorkoutSnapshotAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task SaveCycleStateSnapshotAsync(
        Guid accountId,
        CycleStateSnapshot snapshot,
        DateTimeOffset capturedAtUtc,
        CancellationToken cancellationToken = default);

    Task<CachedSnapshotEnvelope<CycleStateSnapshot>?> LoadCycleStateSnapshotAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task SaveNutritionTargetsSnapshotAsync(
        Guid accountId,
        DailyNutritionTargetsSnapshot snapshot,
        DateTimeOffset capturedAtUtc,
        CancellationToken cancellationToken = default);

    Task<CachedSnapshotEnvelope<DailyNutritionTargetsSnapshot>?> LoadNutritionTargetsSnapshotAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}

public enum CloudAuthorityMode
{
    PreservedLocalPreCutover = 0,
    ServerAuthoritativeCutover = 1
}

public sealed record OfflineWriteCheckResult(
    bool Allowed,
    string? RejectionCode,
    string? UserFacingMessage);

public interface ICloudAuthorityGate
{
    int AuthorityVersion { get; }
    CloudAuthorityMode CurrentMode { get; }
    bool IsNetworkOnline { get; }
    void SetAuthorityMode(CloudAuthorityMode mode);
    void SetNetworkOnline(bool isOnline);
    OfflineWriteCheckResult EvaluateWriteAllowed(string operationName);
}
