using System.Text.Json;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Session;

internal sealed record PlannedPrescriptionSnapshot(Guid DayId, string Name, string WorkoutType,
    int? ActivityTypeId, decimal? PlannedMainDurationMinutes, IReadOnlyList<PlannedExerciseSnapshot> Exercises)
{
    public string? ActivityPrescriptionJson { get; init; }
    public string? DoseAdjustmentReasonCode { get; init; }
    public string? DoseAdjustmentPolicyVersion { get; init; }
    public long? DoseAdjustmentCheckInRevision { get; init; }
    public string? DoseAdjustmentCyclePhase { get; init; }
    public string? DoseAdjustmentPhaseProvenance { get; init; }
    public string? DoseAdjustmentConfidence { get; init; }
    public GenerationSessionV2? GenerationV2 { get; init; }
    public IReadOnlyList<Guid> SplitSupersetWorkoutDayExerciseIds { get; init; } = [];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public string Serialize() => JsonSerializer.Serialize(this, JsonOptions);
    public static PlannedPrescriptionSnapshot? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<PlannedPrescriptionSnapshot>(json, JsonOptions); }
        catch (JsonException) { return null; }
    }
}

internal sealed record PlannedExerciseSnapshot(Guid Id, int ExerciseId, int Sets, int OptionalSets, int Reps,
    int? DurationSeconds, bool IsOptional)
{
    public int? TargetRepetitionsInReserve { get; init; }
    public string? PlanningSlotId { get; init; }
    public int? SupersetGroupId { get; init; }
    public bool IsUnilateral { get; init; }
    public int WarmUpSets { get; init; }
    public int MinRepetitions { get; init; }
    public int MaxRepetitions { get; init; }
    public int RestSeconds { get; init; }
    public string? LoadStatus { get; init; }
    public decimal? PrescribedLoadKg { get; init; }
    public static PlannedExerciseSnapshot FromRecord(WorkoutDayExerciseRecord exercise) =>
        new(exercise.Id, exercise.ExerciseId, exercise.Sets, exercise.OptionalSets, exercise.Reps,
            exercise.DurationSeconds, exercise.IsOptional) { TargetRepetitionsInReserve = exercise.TargetRepetitionsInReserve,
                PlanningSlotId = exercise.PlanningSlotId };

    public WorkoutDayExerciseRecord ToRecord(Guid userId, Guid dayId) => new()
    {
        Id = Id, UserId = userId, WorkoutDayId = dayId, ExerciseId = ExerciseId, Sets = Sets,
        OptionalSets = OptionalSets, Reps = Reps, DurationSeconds = DurationSeconds, IsOptional = IsOptional,
        TargetRepetitionsInReserve = TargetRepetitionsInReserve, PlanningSlotId = PlanningSlotId
    };
}
