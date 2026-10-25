namespace MuscleCuties.UseCases.Features.Workout.Planning;

/// <summary>Transport representation of a selected workout slot, without Core domain types.</summary>
public sealed record StrengthPlanSlotOutputV020(
    string SlotId,
    int MovementSlot,
    bool IsRequired,
    int PrescriptionRole,
    bool IsHeavyMain,
    int? ExerciseId,
    string? ExerciseName,
    string? SelectionReason,
    string? AppliedPrescriptionRule,
    int? Sets,
    int? MinimumRepetitions,
    int? MaximumRepetitions,
    int? RepetitionsInReserve,
    int? RestSeconds,
    IReadOnlyList<string> LimitationCodes)
{
    public int? InitialRepetitionTarget => MinimumRepetitions;
}

public sealed record StrengthPlanPreviewSessionV020(
    string TemplateId,
    string TemplateVersion,
    int SessionIndex,
    decimal? EstimatedMinutes,
    IReadOnlyList<StrengthPlanSlotOutputV020> Slots,
    IReadOnlyList<string> LimitationCodes,
    IReadOnlyDictionary<string, string> SlotReviewConditions,
    DayOfWeek? PlannedWeekday = null);

public sealed record StrengthExerciseReviewConditionV020(
    int ExerciseId,
    string ExerciseName,
    string ExperienceConditions,
    string GeneralConditions);

public sealed record StrengthPlanPreviewResponseV020(
    string PolicyId,
    string PolicyVersion,
    string RulesetVersion,
    string Status,
    int Experience,
    IReadOnlyList<StrengthPlanPreviewSessionV020> Sessions,
    IReadOnlyList<string> LimitationCodes,
    IReadOnlyList<StrengthExerciseReviewConditionV020> ExerciseReviewConditions,
    IReadOnlyList<string> AdvisoryCodes,
    MixedActivityScheduleV020? MixedActivitySchedule = null,
    int? EffectiveActivityTypeId = null,
    string? FallbackSource = null,
    string? FallbackReasonCode = null);

public sealed record MixedActivityScheduleSessionV020(
    int ActivityTypeId,
    DayOfWeek Weekday,
    decimal DurationMinutes,
    bool IsHardSession);

public sealed record MixedActivityScheduleDecisionV020(
    int ActivityTypeId,
    string Disposition,
    string? PrescriptionStatus,
    int? PolicySessionsPerWeek,
    int? TargetSessionsPerWeek,
    int ScheduledSessionsPerWeek,
    IReadOnlyList<string> ReasonCodes,
    string? Modality = null,
    int? EffectiveActivityTypeId = null,
    string? FallbackReasonCode = null,
    int? MaxSessionsPerWeek = null);

public sealed record MixedActivityScheduleV020(
    IReadOnlyList<MixedActivityScheduleSessionV020> Sessions,
    IReadOnlyList<MixedActivityScheduleDecisionV020> Activities,
    IReadOnlyList<string> InputReasonCodes);
