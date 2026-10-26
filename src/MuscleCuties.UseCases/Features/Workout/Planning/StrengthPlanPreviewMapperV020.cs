using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.UseCases.Features.Workout.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Planning;

/// <summary>Translates preview contracts to and from the Core planning rule.</summary>
internal static class StrengthPlanPreviewMapperV020
{
    public const string MixedActivityScheduleIncompleteCode = "MixedActivityScheduleIncomplete";

    public static StrengthPlanPreviewChoices ToChoices(StrengthPlanPreviewRequestV020 request) => new(
        WorkoutActivityContractMapper.ToCore(request.Activity),
        request.LocalDate,
        request.ActivityHistory is { } history
            ? new ActivityTrainingHistory(history.ActivityId, history.ConsistentParticipationWeeks,
                history.AverageSessionsPerWeekInRecentFourWeeks, history.LastCompletedSessionDate,
                history.IsSelfReported)
            : null,
        request.ExcludedExerciseIds,
        request.UserConfirmedFamiliarExerciseIds,
        request.AcknowledgedExerciseConditionIds,
        request.UnknownExperienceAssessmentCompleted,
        request.UserConfirmedManageableLoadExerciseIds,
        request.AcknowledgedSubstitutionPairKeys,
        request.WarmupMinutes,
        request.CooldownMinutes,
        request.AvailableWeekdays);

    public static StrengthPlanPreviewData ToData(StrengthPlanPreviewSourceV020 source) => new(
        source.Goal,
        source.Equipment,
        source.ActiveInjuryFlags,
        source.HasCurrentSymptoms,
        source.SelectedActivityTypes?.Select(activity => (WorkoutActivityType)activity).ToHashSet(),
        source.AvailableWeekdays,
        source.ActivitySessionDurationsMinutes?.ToDictionary(
            duration => (WorkoutActivityType)duration.Key,
            duration => duration.Value),
        source.ProfileTrainingExperience,
        source.Catalog,
        source.PreferredExerciseByOriginalId,
        source.AcceptedExerciseIds,
        source.CompletedExerciseExposureCounts,
        source.KnownManageableLoadExerciseIds,
        source.SecondsPerRepByExerciseId,
        source.PrimaryMuscleByExerciseId,
        source.ReviewedAllowedExerciseIdsByTemplateSlot,
        source.TemplateSlotReviewConditions)
    {
        GoalPriorityByExerciseId = source.GoalPriorityByExerciseId
    };

    public static StrengthPlanPreviewResponseV020 ToResponse(StrengthPlanPreviewResult result) => new(
        StrengthPlanPreviewRule.PolicyId,
        StrengthPlanPreviewRule.Version,
        StrengthPlanPreviewRule.RulesetVersion,
        result.Status.ToString(),
        (int)result.Experience,
        result.Sessions.Select(session => new StrengthPlanPreviewSessionV020(
            session.TemplateId,
            session.TemplateVersion,
            session.SessionIndex,
            session.EstimatedMinutes,
            session.Slots.Select(slot => new StrengthPlanSlotOutputV020(
                slot.SlotId,
                (int)slot.MovementSlot,
                slot.IsRequired,
                (int)slot.PrescriptionRole,
                slot.IsHeavyMain,
                slot.ExerciseId,
                slot.ExerciseName,
                slot.SelectionReason,
                slot.AppliedPrescriptionRule,
                slot.Sets,
                slot.MinimumRepetitions,
                slot.MaximumRepetitions,
                slot.RepetitionsInReserve,
                slot.RestSeconds,
                slot.LimitationCodes)).ToArray(),
            session.LimitationCodes,
            session.SlotReviewConditions,
            session.PlannedWeekday)).ToArray(),
        result.LimitationCodes,
        result.ExerciseReviewConditions.Select(condition => new StrengthExerciseReviewConditionV020(
            condition.ExerciseId,
            condition.ExerciseName,
            condition.ExperienceConditions,
            condition.GeneralConditions)).ToArray(),
        result.AdvisoryCodes,
        EffectiveActivityTypeId: result.EffectiveActivity.HasValue ? (int)result.EffectiveActivity.Value : null,
        FallbackSource: result.FallbackSource,
        FallbackReasonCode: result.FallbackReasonCode);

    public static MixedActivityScheduleV020 ToMixedSchedule(MixedActivityWeekScheduleResult result) => new(
        result.Sessions.Select(session => new MixedActivityScheduleSessionV020(
            (int)session.Activity,
            session.Day,
            session.DurationMinutes,
            session.IsHardSession)).ToArray(),
        result.Activities.Select(activity => new MixedActivityScheduleDecisionV020(
            (int)activity.Activity,
            activity.Disposition.ToString(),
            activity.PrescriptionStatus?.ToString(),
            activity.PolicySessionsPerWeek,
            activity.TargetSessionsPerWeek,
            activity.ScheduledSessionsPerWeek,
            activity.Reasons,
            activity.Prescription?.Modality.ToString(),
            activity.EffectiveActivity.HasValue ? (int)activity.EffectiveActivity.Value : null,
            activity.FallbackReasonCode,
            activity.MaxSessionsPerWeek)).ToArray(),
        result.InputReasons);

    public static StrengthPlanPreviewResponseV020 AttachMixedSchedule(
        StrengthPlanPreviewResponseV020 response,
        MixedActivityWeekScheduleResult schedule,
        WorkoutActivityDto chosenStrengthActivity)
    {
        var coreStrengthActivity = WorkoutActivityContractMapper.ToCore(chosenStrengthActivity);
        var strengthDays = schedule.Sessions
            .Where(session => session.Activity == coreStrengthActivity ||
                              (response.EffectiveActivityTypeId.HasValue && (int)session.Activity == response.EffectiveActivityTypeId.Value))
            .Select(session => session.Day)
            .ToArray();
        var effectiveCount = Math.Min(response.Sessions.Count, strengthDays.Length);
        var alignedStrengthSessions = response.Sessions
            .Select((session, index) => session with
            {
                PlannedWeekday = index < strengthDays.Length ? strengthDays[index] : null
            })
            .ToArray();
        if (strengthDays.Length > 0 && effectiveCount < response.Sessions.Count)
        {
            // If fewer strength days are available than the template originally targeted,
            // cut volume to the placed strength sessions so the plan fits the available days.
            alignedStrengthSessions = alignedStrengthSessions.Take(effectiveCount).ToArray();
        }

        // Structurally valid availability and at least one placed strength session are required to apply.
        var isIncomplete = schedule.InputReasons.Count > 0 ||
                           (response.Sessions.Count > 0 && strengthDays.Length == 0);
        var limitationCodes = response.LimitationCodes;
        if (alignedStrengthSessions.Length > 0 && alignedStrengthSessions.All(s => s.PlannedWeekday.HasValue))
        {
            // Volume was successfully cut and all placed strength sessions fit on available weekdays.
            limitationCodes = limitationCodes
                .Where(code => code is not "StrengthSessionsExceedAvailableWeekdays" and not MixedActivityScheduleIncompleteCode)
                .ToArray();
        }

        if (isIncomplete)
        {
            limitationCodes = limitationCodes.Append(MixedActivityScheduleIncompleteCode)
                .Distinct(StringComparer.Ordinal).ToArray();
        }

        var isFeasible = !isIncomplete &&
                         limitationCodes.Count == 0 &&
                         alignedStrengthSessions.Length > 0 &&
                         response.Status != nameof(StrengthPlanPreviewStatus.InvalidInput);

        return response with
        {
            Status = isFeasible ? nameof(StrengthPlanPreviewStatus.Feasible) :
                     (isIncomplete ? nameof(StrengthPlanPreviewStatus.Limited) : response.Status),
            Sessions = alignedStrengthSessions,
            LimitationCodes = limitationCodes,
            MixedActivitySchedule = ToMixedSchedule(schedule)
        };
    }
}
