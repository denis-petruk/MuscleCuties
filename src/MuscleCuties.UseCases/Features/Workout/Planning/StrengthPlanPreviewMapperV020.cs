using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.UseCases.Features.Workout.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Planning;

/// <summary>Translates preview contracts to and from the Core planning rule.</summary>
internal static class StrengthPlanPreviewMapperV020
{
    public const string MixedActivityScheduleIncompleteCode = "MixedActivityScheduleIncomplete";
    public const string NoSafeStrengthContentRemainingCode = "NoSafeStrengthContentRemaining";

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
        GoalPriorityByExerciseId = source.GoalPriorityByExerciseId,
        TemplateVolumeFloors = source.TemplateVolumeFloors?.ToDictionary(pair => pair.Key, pair =>
            new TemplateVolumeFloor(pair.Value.TemplateId, pair.Value.MinimumWorkingSets, pair.Value.MinimumExercises,
                pair.Value.MinimumSetsBySlotId, TemplateVolumeFloorRule.ReviewedStatus)),
        MuscleGroupsByExerciseId = source.MuscleGroupsByExerciseId
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
        FallbackReasonCode: result.FallbackReasonCode,
        RequiresReducedVolumeAcknowledgment: result.RequiresReducedVolumeAcknowledgment);

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

        var hasStrengthDays = strengthDays.Length > 0;
        var totalScheduledSessions = schedule.Sessions.Count;
        var hasSafeNonStrengthSessions = schedule.Sessions.Any(session =>
            !StrengthStyleSelectionRule.IsStrengthStyle(session.Activity));

        // When strength sessions cannot fit or have no safe exercises, but safe non-strength activities exist,
        // allow a safe non-strength-only plan with explanation. An empty plan remains blocked.
        var isStrengthExhausted = !hasStrengthDays && response.LimitationCodes.Length == 0 &&
            (response.Sessions.Count == 0 || schedule.StrengthAssignments is { Count: 0 });
        var allowNonStrengthOnlyPlan = isStrengthExhausted && hasSafeNonStrengthSessions && totalScheduledSessions > 0;

        // When strength sessions are scheduled, fit volume to the placed days with their planned weekdays.
        // If no strength sessions could be placed, retain original sessions with unassigned weekdays.
        var alignedStrengthSessions = hasStrengthDays
            ? schedule.StrengthAssignments is { } assignments
                ? assignments.Select(item => response.Sessions[item.SessionIndex] with { PlannedWeekday = item.Day }).ToArray()
                : response.Sessions.Take(effectiveCount)
                    .Select((session, index) => session with { PlannedWeekday = strengthDays[index] }).ToArray()
            : allowNonStrengthOnlyPlan
                ? Array.Empty<StrengthPlanPreviewSessionV020>()
                : response.Sessions
                    .Select(session => session with { PlannedWeekday = null })
                    .ToArray();

        var isVolumeReduced = hasStrengthDays && effectiveCount < response.Sessions.Count;
        var isIncomplete = schedule.InputReasons.Count > 0 ||
                           (!allowNonStrengthOnlyPlan && response.Sessions.Count > 0 && !hasStrengthDays) ||
                           totalScheduledSessions == 0;

        // Placed strength sessions satisfy availability, clearing template-overflow limitation codes.
        var limitationCodes = isIncomplete
            ? response.LimitationCodes
                .Append(MixedActivityScheduleIncompleteCode)
                .Distinct(StringComparer.Ordinal)
                .ToArray()
            : allowNonStrengthOnlyPlan
                ? new[] { NoSafeStrengthContentRemainingCode }
                : response.LimitationCodes
                    .Where(code => code is not "StrengthSessionsExceedAvailableWeekdays" and not MixedActivityScheduleIncompleteCode)
                    .ToArray();

        var isFeasible = !isIncomplete &&
                         (limitationCodes.Length == 0 || allowNonStrengthOnlyPlan) &&
                         (alignedStrengthSessions.Length > 0 || allowNonStrengthOnlyPlan) &&
                         totalScheduledSessions > 0 &&
                         response.Status != nameof(StrengthPlanPreviewStatus.InvalidInput);

        var status = isFeasible
            ? nameof(StrengthPlanPreviewStatus.Feasible)
            : isIncomplete || totalScheduledSessions == 0
                ? nameof(StrengthPlanPreviewStatus.Limited)
                : response.Status;

        return response with
        {
            Status = status,
            Sessions = alignedStrengthSessions,
            LimitationCodes = limitationCodes,
            MixedActivitySchedule = ToMixedSchedule(schedule),
            RequiresReducedVolumeAcknowledgment = isVolumeReduced || response.RequiresReducedVolumeAcknowledgment
        };
    }
}
