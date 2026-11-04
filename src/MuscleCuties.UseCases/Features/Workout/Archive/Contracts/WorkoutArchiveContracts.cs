namespace MuscleCuties.UseCases.Features.Workout.Archive;

public sealed record WorkoutExerciseSetDto(
    Guid Id,
    Guid WorkoutSessionLogId,
    string? ExerciseId,
    string ExerciseName,
    int SetNumber,
    double WeightKg,
    int RepsCompleted,
    double? Rpe,
    bool IsWarmup,
    int RestTimeSeconds);

public sealed record WorkoutArchiveSummaryDto(
    Guid Id,
    string WorkoutName,
    DateTime StartedAt,
    DateTime? CompletedAt,
    int TotalDurationSeconds,
    double TotalVolumeKg,
    string? Notes,
    int? RpeRating,
    int TotalSetsCount,
    IReadOnlyList<string> PrimaryExercises,
    string DetailKind = "noSetDetails",
    string TimePrecision = "unknown")
{
    public string DetailLabel => DetailKind == "aggregateOnly"
        ? "Historical aggregate · individual sets unavailable"
        : DetailKind == "noSetDetails"
            ? "Set details unavailable"
            : string.Empty;

    public string DurationLabel => TotalDurationSeconds <= 0
        ? "Not recorded"
        : $"{TotalDurationSeconds}s";

    public string VolumeLabel => DetailKind == "aggregateOnly" && TotalVolumeKg <= 0
        ? "Not recorded"
        : $"{TotalVolumeKg:N0} kg";

    public string SetCountLabel => DetailKind == "aggregateOnly"
        ? "Not recorded"
        : $"{TotalSetsCount} sets";
}

public sealed record WorkoutArchiveDetailDto(
    Guid Id,
    string WorkoutName,
    DateTime StartedAt,
    DateTime? CompletedAt,
    int TotalDurationSeconds,
    double TotalVolumeKg,
    string? Notes,
    int? RpeRating,
    IReadOnlyList<WorkoutExerciseSetDto> Sets,
    string DetailKind = "noSetDetails",
    string TimePrecision = "unknown");

public sealed record HistoryMonthOptionDto(int Year, int Month);

public sealed record ExercisePersonalRecordDto(
    string? ExerciseId,
    string ExerciseName,
    double MaxWeightKg,
    int RepsAtMaxWeight,
    double MaxSetVolumeKg,
    double EstimatedOneRepMaxKg,
    DateTime AchievedAt,
    Guid WorkoutSessionLogId,
    Guid ExerciseSetLogId);

public sealed record UpdateWorkoutSessionNotesRequest(string? Notes, int? RpeRating);

public sealed record UpdateExerciseSetRequest(double WeightKg, int RepsCompleted);

public sealed record WorkoutHistoryCountDto(int Count);
