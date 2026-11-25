namespace MuscleCuties.UseCases.Features.Caching;

/// <summary>One set of the session in progress, cached so the app opens on it offline.</summary>
public sealed record ActiveWorkoutSetSnapshot(
    int SetOrder,
    int Reps,
    decimal WeightKg,
    decimal? Rpe,
    bool IsCompleted);

/// <summary>One exercise of the session in progress, with its sets so far.</summary>
public sealed record ActiveWorkoutExerciseSnapshot(
    string ExerciseCode,
    string DisplayName,
    IReadOnlyList<ActiveWorkoutSetSnapshot> Sets);

/// <summary>The session in progress as cached: what was prescribed and what is logged.</summary>
public sealed record ActiveWorkoutSessionSnapshot(
    Guid SessionId,
    DateOnly LocalDate,
    string ArchetypeCode,
    int ElapsedSeconds,
    IReadOnlyList<ActiveWorkoutExerciseSnapshot> Exercises,
    int ConcurrencyVersion);

/// <summary>The cycle state as cached, so the phase shows before the network answers.</summary>
public sealed record CycleStateSnapshot(
    DateOnly AsOfLocalDate,
    string CurrentPhase,
    int DayOfCycle,
    int CycleLengthDays,
    string PredictionSource,
    DateOnly? PredictedNextPeriodStart,
    int ConcurrencyVersion);

/// <summary>The day's targets as cached, with unset values left absent rather than zero.</summary>
public sealed record DailyNutritionTargetsSnapshot(
    DateOnly LocalDate,
    int TargetCalories,
    decimal TargetProteinGrams,
    decimal TargetCarbsGrams,
    decimal TargetFatGrams,
    string CalculationProvenance,
    int ConcurrencyVersion);

/// <summary>A cached value with the version it was written at, so a stale copy can be detected.</summary>
public sealed record CachedSnapshotEnvelope<T>(
    T Payload,
    DateTimeOffset CapturedAtUtc,
    DateTimeOffset StaleAfterUtc,
    int SourceVersion)
{
    /// <summary>True when the cached entry has passed its freshness deadline.</summary>
    public bool IsStale(DateTimeOffset nowUtc) => nowUtc >= StaleAfterUtc;
}

/// <summary>Where the per-account cache keys live.</summary>
public interface ISnapshotCacheKeyStore
{
    Task<byte[]?> ReadAsync(CancellationToken cancellationToken = default);
    Task WriteAsync(byte[] key, CancellationToken cancellationToken = default);
    Task RemoveAsync(CancellationToken cancellationToken = default);
}

/// <summary>The on-device cache: activate an account, read and write its snapshots, clear on sign-out.</summary>
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

/// <summary>Which authority the app is currently writing under: the cloud, the device, or neither.</summary>
public enum CloudAuthorityMode
{
    PreservedLocalPreCutover = 0,
    ServerAuthoritativeCutover = 1
}

/// <summary>Whether a write may proceed now, or has to wait for the other authority.</summary>
public sealed record OfflineWriteCheckResult(
    bool Allowed,
    string? RejectionCode,
    string? UserFacingMessage);

/// <summary>Decides that write question from connectivity and the last known authority.</summary>
public interface ICloudAuthorityGate
{
    int AuthorityVersion { get; }
    CloudAuthorityMode CurrentMode { get; }
    bool IsNetworkOnline { get; }
    void SetAuthorityMode(CloudAuthorityMode mode);
    void SetNetworkOnline(bool isOnline);
    OfflineWriteCheckResult EvaluateWriteAllowed(string operationName);
}
