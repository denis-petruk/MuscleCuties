namespace MuscleCuties.Core.Models.Workout;

public sealed record ExerciseSetLogDto(
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

public sealed record WorkoutSessionSummaryDto(
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

public sealed record WorkoutSessionDetailDto(
    Guid Id,
    string WorkoutName,
    DateTime StartedAt,
    DateTime? CompletedAt,
    int TotalDurationSeconds,
    double TotalVolumeKg,
    string? Notes,
    int? RpeRating,
    IReadOnlyList<ExerciseSetLogDto> Sets,
    string DetailKind = "noSetDetails",
    string TimePrecision = "unknown");
