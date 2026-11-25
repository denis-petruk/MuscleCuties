namespace MuscleCuties.UseCases.Features.Workout.Archive;

/// <summary>One set of a finished session, with what was lifted.</summary>
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

/// <summary>A finished session as the archive list shows it.</summary>
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
    /// <summary>The label the list shows for an aggregate session, which has no single activity.</summary>
    public string DetailLabel => DetailKind == "aggregateOnly"
        ? "Historical aggregate · individual sets unavailable"
        : DetailKind == "noSetDetails"
            ? "Set details unavailable"
            : string.Empty;

    /// <summary>The duration as the list shows it, with a dash under a minute.</summary>
    public string DurationLabel => TotalDurationSeconds <= 0
        ? "Not recorded"
        : $"{TotalDurationSeconds}s";

    /// <summary>The lifted volume, shown only when the session's detail is aggregate.</summary>
    public string VolumeLabel => DetailKind == "aggregateOnly" && TotalVolumeKg <= 0
        ? "Not recorded"
        : $"{TotalVolumeKg:N0} kg";

    /// <summary>The set count, shown only for aggregate sessions.</summary>
    public string SetCountLabel => DetailKind == "aggregateOnly"
        ? "Not recorded"
        : $"{TotalSetsCount} sets";

    /// <summary>Distance for cardio sessions, absent for strength ones.</summary>
    public decimal? DistanceKm { get; init; }
    /// <summary>The start in the account's local time, which is the day the archive groups by.</summary>
    public DateTime? AccountLocalStartedAt { get; init; }
}

/// <summary>A finished session in full.</summary>
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
    string TimePrecision = "unknown")
{
    /// <summary>Distance for cardio sessions, absent for strength ones.</summary>
    public decimal? DistanceKm { get; init; }
    /// <summary>The start in the account's local time, so the detail agrees with the list's day.</summary>
    public DateTime? AccountLocalStartedAt { get; init; }
}

/// <summary>One month the history filter offers, with its session count.</summary>
public sealed record HistoryMonthOptionDto(int Year, int Month);

/// <summary>Her best set for one exercise.</summary>
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

/// <summary>A change to a finished session's notes.</summary>
public sealed record UpdateWorkoutSessionNotesRequest(string? Notes, int? RpeRating);

/// <summary>A correction to one set of a finished session.</summary>
public sealed record UpdateExerciseSetRequest(double WeightKg, int RepsCompleted);

/// <summary>How many sessions fall in a period, which the filter shows before loading them.</summary>
public sealed record WorkoutHistoryCountDto(int Count);
