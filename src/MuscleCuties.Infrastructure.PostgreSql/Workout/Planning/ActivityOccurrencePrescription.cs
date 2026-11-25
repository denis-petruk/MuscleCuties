
namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

/// <summary>Reads and writes the activity day's prescription JSON, which is what carries the occurrence rules for a non-strength day.</summary>
internal static class ActivityOccurrencePrescription
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Serializes the occurrence prescription, or nothing when it is absent.</summary>
    internal static string? Serialize(ActivitySessionPrescriptionOutputV020? prescription) =>
        prescription is null ? null : JsonSerializer.Serialize(prescription, JsonOptions);

    /// <summary>The stored activity prescription, or nothing when the day carries none.</summary>
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

    /// <summary>One occurrence's prescription as the domain type, carrying the duration and intensity actually chosen.</summary>
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

    /// <summary>True when the occurrence carries a prescription the schedule can use.</summary>
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

    // Only an explicit occurrence override is an adjustment. The original prescription
    // remains separate so unchanged sessions do not enter the same-day adjustment rule.
    /// <summary>The schedule validation entry for one occurrence.</summary>
    internal static WorkoutScheduleValidationSession ValidationSession(MixedActivityScheduleSessionV020 session,
        MixedActivityScheduleDecisionV020 decision, IReadOnlySet<string>? muscles = null) =>
        new(session.Weekday, (WorkoutActivityType)session.ActivityTypeId, session.IsHardSession, muscles,
            session.DurationMinutes, ToDomain(decision.OriginalPrescription),
            ToDomain(session.OccurrencePrescription));
}
