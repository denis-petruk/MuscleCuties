using System.Text.Json;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

internal static class ActivityOccurrencePrescription
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal static string? Serialize(ActivitySessionPrescriptionOutputV020? prescription) =>
        prescription is null ? null : JsonSerializer.Serialize(prescription, JsonOptions);

    internal static ActivitySessionPrescriptionOutputV020? Deserialize(string? json)
    {
        if (json is null) return null;
        try
        {
            return JsonSerializer.Deserialize<ActivitySessionPrescriptionOutputV020>(json, JsonOptions)
                ?? throw new InvalidOperationException("The saved activity prescription is unavailable.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("The saved activity prescription is unavailable.", exception);
        }
    }

    internal static ActivitySessionPrescription? ToDomain(ActivitySessionPrescriptionOutputV020? value)
    {
        if (value is null || !Enum.IsDefined((WorkoutActivityType)value.ActivityTypeId) ||
            !Enum.TryParse<SessionPurpose>(value.Purpose, out var purpose) || !Enum.IsDefined(purpose) ||
            !Enum.TryParse<ActivityModality>(value.Modality, out var modality) || !Enum.IsDefined(modality) ||
            !Enum.TryParse<IntendedIntensity>(value.Intensity, out var intensity) || !Enum.IsDefined(intensity) ||
            !Enum.TryParse<SessionTrainingStyle>(value.TrainingStyle, out var style) || !Enum.IsDefined(style) ||
            value.Reasons is null) return null;
        return new((WorkoutActivityType)value.ActivityTypeId, purpose, modality, intensity, style,
            value.FrequencyPerWeek, value.WarmupMinutes, value.MainMinutes, value.CooldownMinutes,
            value.Sets, value.MinimumRepetitions, value.MaximumRepetitions, value.RepetitionsInReserve,
            value.RestSeconds, value.IntervalWorkCount, value.IntervalWorkSeconds,
            value.IntervalRecoveryCount, value.IntervalRecoverySeconds, value.RequiresUserConfirmation,
            value.Reasons);
    }

    internal static bool IsValid(MixedActivityScheduleSessionV020 session,
        MixedActivityScheduleDecisionV020 decision)
    {
        // Historical proposals have neither field and retain the original validation path.
        if (session.OccurrencePrescription is null && decision.OriginalPrescription is null) return true;
        var original = ToDomain(decision.OriginalPrescription);
        var occurrence = ToDomain(session.OccurrencePrescription ?? decision.OriginalPrescription);
        if (original is null || occurrence is null || original.Activity != (WorkoutActivityType)session.ActivityTypeId ||
            occurrence.Activity != original.Activity) return false;
        return original with { Reasons = occurrence.Reasons } == occurrence ||
            SameDayDemandAdjustmentRule.IsValid(original, occurrence, session.DurationMinutes);
    }

    internal static WorkoutScheduleValidationSession ValidationSession(MixedActivityScheduleSessionV020 session,
        MixedActivityScheduleDecisionV020 decision, IReadOnlySet<string>? muscles = null) =>
        new(session.Weekday, (WorkoutActivityType)session.ActivityTypeId, session.IsHardSession, muscles,
            session.DurationMinutes, ToDomain(decision.OriginalPrescription),
            ToDomain(session.OccurrencePrescription ?? decision.OriginalPrescription));
}
