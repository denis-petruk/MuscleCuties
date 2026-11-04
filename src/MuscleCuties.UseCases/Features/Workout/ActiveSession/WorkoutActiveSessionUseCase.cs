namespace MuscleCuties.UseCases.Features.Workout.ActiveSession;

public sealed class WorkoutActiveSessionUseCase(IWorkoutActiveSessionStore store)
{
    public Task<IReadOnlyList<PlannedWorkoutSessionHistoryEntry>> GetPlannedSessionHistoryAsync(Guid userId, int workoutDayId, CancellationToken cancellationToken = default) =>
        store.GetPlannedSessionHistoryAsync(userId, workoutDayId, cancellationToken);
    public Task<ActiveWorkoutSessionSnapshot?> GetPlannedSessionAsync(Guid userId, int workoutDayId, CancellationToken cancellationToken = default) =>
        store.GetPlannedSessionAsync(userId, workoutDayId, cancellationToken);
    private const int MaximumIdempotencyKeyLength = 128;
    private const int MaximumNameLength = 200;
    private const int MaximumNotesLength = 2000;
    private const int MinimumSetNumber = 1;
    private const int MaximumSetNumber = 100;
    private const decimal MinimumWeightKg = 0m;
    private const decimal MaximumWeightKg = 1000m;
    private const int MinimumReps = 0;
    private const int MaximumReps = 1000;
    private const decimal MinimumRpe = 1m;
    private const decimal MaximumRpe = 10m;
    private const int MinimumRestSeconds = 0;
    private const int MaximumRestSeconds = 3600;
    private const int MinimumDurationSeconds = 0;
    private const int MaximumDurationSeconds = 86_400;

    public Task<ActiveWorkoutSessionSnapshot?> ResumeTimingAsync(Guid userId, ResumeWorkoutTimingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCommon(userId, command.SessionId, command.IdempotencyKey);
        return store.ResumeTimingAsync(userId, command, cancellationToken);
    }

    public Task<ActiveWorkoutSessionSnapshot?> CheckpointTimingAsync(Guid userId, CheckpointWorkoutTimingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCommon(userId, command.SessionId, command.IdempotencyKey);
        if (command.IntervalId == Guid.Empty || command.MeasuredSeconds < MinimumDurationSeconds || command.MeasuredSeconds > MaximumDurationSeconds)
            throw new ArgumentException("Invalid active interval checkpoint.", nameof(command));
        return store.CheckpointTimingAsync(userId, command, cancellationToken);
    }

    public Task<ActiveWorkoutSessionSnapshot?> GetSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || sessionId == Guid.Empty)
            throw new ArgumentException("A valid owner and session ID are required.");
        return store.GetSessionAsync(userId, sessionId, cancellationToken);
    }

    public Task<ActiveWorkoutSessionSnapshot> StartSessionAsync(
        Guid userId,
        StartWorkoutSessionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCommon(userId, command.SessionId, command.IdempotencyKey);
        if (string.IsNullOrWhiteSpace(command.WorkoutName) ||
            command.WorkoutName.Trim().Length > MaximumNameLength ||
            command.Notes?.Length > MaximumNotesLength)
            throw new ArgumentException("Workout session requires a valid name and bounded notes.", nameof(command));

        return store.StartSessionAsync(
            userId,
            command with { WorkoutName = command.WorkoutName.Trim() },
            cancellationToken);
    }

    public Task<ActiveWorkoutSessionSnapshot?> UpsertSetAsync(
        Guid userId,
        UpsertWorkoutSessionSetCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCommon(userId, command.SessionId, command.IdempotencyKey);
        ValidateSet(command.Set);
        return store.UpsertSetAsync(
            userId,
            command with { Set = NormalizeSet(command.Set) },
            cancellationToken);
    }

    public Task<ActiveWorkoutSessionSnapshot?> FinishSessionAsync(
        Guid userId,
        FinishWorkoutSessionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCommon(userId, command.SessionId, command.IdempotencyKey);
        if (command.TotalDurationSeconds < MinimumDurationSeconds ||
            command.TotalDurationSeconds > MaximumDurationSeconds ||
            (command.RpeRating is { } rpe && (rpe < (int)MinimumRpe || rpe > (int)MaximumRpe)) ||
            command.Notes?.Length > MaximumNotesLength)
            throw new ArgumentException("Workout finish parameters are outside supported bounds.", nameof(command));

        var finalSets = command.FinalSets ?? [];
        foreach (var set in finalSets)
            ValidateSet(set);

        return store.FinishSessionAsync(
            userId,
            command with { FinalSets = finalSets.Select(NormalizeSet).ToArray() },
            cancellationToken);
    }

    public Task<ActiveWorkoutSessionSnapshot?> CancelSessionAsync(
        Guid userId,
        CancelWorkoutSessionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCommon(userId, command.SessionId, command.IdempotencyKey);
        return store.CancelSessionAsync(userId, command, cancellationToken);
    }

    private static void ValidateCommon(Guid userId, Guid sessionId, string idempotencyKey)
    {
        if (userId == Guid.Empty || sessionId == Guid.Empty)
            throw new ArgumentException("Valid owner and session IDs are required.");
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Trim().Length > MaximumIdempotencyKeyLength)
            throw new ArgumentException("A valid non-empty idempotency key is required.", nameof(idempotencyKey));
    }

    private static void ValidateSet(ActiveExerciseSetInput? set)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (set.SetId == Guid.Empty ||
            string.IsNullOrWhiteSpace(set.ExerciseName) ||
            set.ExerciseName.Trim().Length > MaximumNameLength ||
            set.SetNumber < MinimumSetNumber ||
            set.SetNumber > MaximumSetNumber ||
            set.WeightKg < MinimumWeightKg ||
            set.WeightKg > MaximumWeightKg ||
            set.RepsCompleted < MinimumReps ||
            set.RepsCompleted > MaximumReps ||
            (set.Rpe is { } rpe && (rpe < MinimumRpe || rpe > MaximumRpe)) ||
            set.RestTimeSeconds < MinimumRestSeconds ||
            set.RestTimeSeconds > MaximumRestSeconds)
            throw new ArgumentException("Exercise set values are outside supported bounds.", nameof(set));
    }

    private static ActiveExerciseSetInput NormalizeSet(ActiveExerciseSetInput set) =>
        set with
        {
            ExerciseName = set.ExerciseName.Trim(),
            ExerciseId = string.IsNullOrWhiteSpace(set.ExerciseId) ? null : set.ExerciseId.Trim()
        };
}
