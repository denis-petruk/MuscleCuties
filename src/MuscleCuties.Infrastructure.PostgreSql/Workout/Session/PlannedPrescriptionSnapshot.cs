
namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Session;

/// <summary>A day's prescription frozen as it was planned. Stored as JSON next to the live rows so a session can be judged
/// against what was actually prescribed, even after the plan changed.</summary>
internal sealed record PlannedPrescriptionSnapshot(Guid DayId, string Name, string WorkoutType,
    int? ActivityTypeId, decimal? PlannedMainDurationMinutes, IReadOnlyList<PlannedExerciseSnapshot> Exercises)
{
    /// <summary>The activity day's own prescription, set only for non-strength days.</summary>
    public string? ActivityPrescriptionJson { get; init; }
    /// <summary>Why the dose was adjusted for that day, as a code the client translates.</summary>
    public string? DoseAdjustmentReasonCode { get; init; }
    /// <summary>The policy version that produced the adjustment, so an old decision can be told from a current one.</summary>
    public string? DoseAdjustmentPolicyVersion { get; init; }
    /// <summary>The check-in revision the adjustment read, so a newer check-in invalidates it.</summary>
    public long? DoseAdjustmentCheckInRevision { get; init; }
    /// <summary>The phase the adjustment was made for.</summary>
    public string? DoseAdjustmentCyclePhase { get; init; }
    /// <summary>Whether the phase was logged or projected, which is what the client shows as confidence.</summary>
    public string? DoseAdjustmentPhaseProvenance { get; init; }
    /// <summary>How sure the phase was, kept with the adjustment rather than recomputed later.</summary>
    public string? DoseAdjustmentConfidence { get; init; }
    /// <summary>The version-2 generation record, present only for days produced by that planner.</summary>
    public GenerationSessionV2? GenerationV2 { get; init; }
    /// <summary>The exercise rows split out of a superset for this day, so the pairing can be rebuilt.</summary>
    public IReadOnlyList<Guid> SplitSupersetWorkoutDayExerciseIds { get; init; } = [];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    /// <summary>Serializes the snapshot for storage.</summary>
    public string Serialize() => JsonSerializer.Serialize(this, JsonOptions);
    /// <summary>Reads a stored snapshot, answering null for empty or unreadable JSON so a corrupt row degrades to "no snapshot"
    /// instead of failing the session.</summary>
    public static PlannedPrescriptionSnapshot? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<PlannedPrescriptionSnapshot>(json, JsonOptions); }
        catch (JsonException) { return null; }
    }
}

/// <summary>One exercise as prescribed in a snapshot, carrying the prescription fields the live row may since have changed.</summary>
internal sealed record PlannedExerciseSnapshot(Guid Id, int ExerciseId, int Sets, int OptionalSets, int Reps,
    int? DurationSeconds, bool IsOptional)
{
    /// <summary>The effort target, in reps left in reserve, rather than a weight.</summary>
    public int? TargetRepetitionsInReserve { get; init; }
    /// <summary>The planning slot it filled, so a substitute can be placed in the same slot.</summary>
    public string? PlanningSlotId { get; init; }
    /// <summary>The superset it belongs to; exercises sharing it are performed back to back.</summary>
    public int? SupersetGroupId { get; init; }
    /// <summary>Whether the work is per side, which doubles its duration.</summary>
    public bool IsUnilateral { get; init; }
    /// <summary>The warm-up sets prescribed before the working sets; they are not counted as volume.</summary>
    public int WarmUpSets { get; init; }
    /// <summary>The bottom of the rep range, which is where load is reduced.</summary>
    public int MinRepetitions { get; init; }
    /// <summary>The top of the rep range, which is where load increases.</summary>
    public int MaxRepetitions { get; init; }
    /// <summary>The rest prescribed after each set, in seconds.</summary>
    public int RestSeconds { get; init; }
    /// <summary>Whether the load was prescribed, held or unknown — the reason a weight may be absent.</summary>
    public string? LoadStatus { get; init; }
    /// <summary>The prescribed load in kilograms; null when no load was prescribed.</summary>
    public decimal? PrescribedLoadKg { get; init; }
    /// <summary>Builds the snapshot from a stored workout day exercise.</summary>
    public static PlannedExerciseSnapshot FromRecord(WorkoutDayExerciseRecord exercise) =>
        new(exercise.Id, exercise.ExerciseId, exercise.Sets, exercise.OptionalSets, exercise.Reps,
            exercise.DurationSeconds, exercise.IsOptional)
        {
            TargetRepetitionsInReserve = exercise.TargetRepetitionsInReserve,
            PlanningSlotId = exercise.PlanningSlotId
        };

    /// <summary>The live row this snapshot prescribes, rebuilt under the given owner and day; only the fields the snapshot
    /// carries are restored.</summary>
    public WorkoutDayExerciseRecord ToRecord(Guid userId, Guid dayId) => new()
    {
        Id = Id,
        UserId = userId,
        WorkoutDayId = dayId,
        ExerciseId = ExerciseId,
        Sets = Sets,
        OptionalSets = OptionalSets,
        Reps = Reps,
        DurationSeconds = DurationSeconds,
        IsOptional = IsOptional,
        TargetRepetitionsInReserve = TargetRepetitionsInReserve,
        PlanningSlotId = PlanningSlotId
    };
}
