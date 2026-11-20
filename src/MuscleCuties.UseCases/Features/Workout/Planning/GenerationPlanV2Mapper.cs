using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Domain.Workout.Planning.V2;
using MuscleCuties.Core.Models.Enums.Workout;
using System.Security.Cryptography;
using System.Text.Json;

namespace MuscleCuties.UseCases.Features.Workout.Planning;

/// <summary>Projects the canonical prescription into transport and the existing slot envelope.</summary>
public static class GenerationPlanV2Mapper
{
    private const string StrengthSessionsExceedAvailableWeekdaysCode =
        "StrengthSessionsExceedAvailableWeekdays";
    private const string ActivityFrequencyLimitedByAvailableWeekdaysCode =
        "ActivityFrequencyLimitedByAvailableWeekdays";

    public static StrengthPlanPreviewResponseV020 ForCanonicalUse(StrengthPlanPreviewResponseV020 preview)
    {
        if (preview.Status != "Feasible") return preview;
        if (preview.GenerationV2 is not { } plan) return preview;
        var retainedActivities = (preview.MixedActivitySchedule?.Sessions ?? [])
            .Where(session => !StrengthStyleSelectionRule.IsStrengthStyle((WorkoutActivityType)session.ActivityTypeId) &&
                !plan.Sessions.Any(generated => generated.ActivityTypeId == session.ActivityTypeId))
            .Select((session, index) => new GenerationSessionV2(
                plan.Sessions.Select(item => item.SessionIndex).DefaultIfEmpty(-1).Max() + index + 1,
                session.Weekday, plan.Sessions.FirstOrDefault()?.LocalDate ?? default,
                session.ActivityTypeId, 0, session.DurationMinutes, [])).ToArray();
        if (retainedActivities.Length > 0)
            plan = plan with { Sessions = plan.Sessions.Concat(retainedActivities).ToArray(),
                SessionCount = plan.Sessions.Count + retainedActivities.Length };
        var beforeCapacity = plan;
        plan = LimitWeekToCapacity(plan);
        var reducedActivities = beforeCapacity.Sessions.GroupBy(session => session.ActivityTypeId)
            .Where(group => group.Count() > plan.Sessions.Count(session => session.ActivityTypeId == group.Key))
            .Select(group => group.Key).ToHashSet();
        var volumeReducedForCapacity = reducedActivities.Count > 0 ||
            !ReferenceEquals(plan, beforeCapacity) &&
            beforeCapacity.Sessions.Count > plan.Sessions.Count;
        var strength = ToStrengthSessions(plan);
        var sessions = plan.Sessions.Select(session =>
        {
            var previous = preview.MixedActivitySchedule?.Sessions.FirstOrDefault(item =>
                item.ActivityTypeId == session.ActivityTypeId && item.Weekday == session.Weekday);
            var original = preview.MixedActivitySchedule?.Activities.FirstOrDefault(item =>
                item.ActivityTypeId == session.ActivityTypeId)?.OriginalPrescription;
            return new MixedActivityScheduleSessionV020(session.ActivityTypeId, session.Weekday,
                session.EstimatedTotalMinutes + (session.AttachedFinisher?.DurationMinutes ?? 0),
                session.Exercises.Count > 0 || previous?.IsHardSession == true,
                plan.Sessions.Count(other => other.Weekday == session.Weekday && other.SessionIndex <= session.SessionIndex),
                previous?.OccurrencePrescription);
        }).ToArray();
        var decisions = sessions.GroupBy(session => session.ActivityTypeId).Select(group =>
        {
            var decision = preview.MixedActivitySchedule?.Activities.FirstOrDefault(item => item.ActivityTypeId == group.Key) is { } previous
                ? previous with { Disposition = "Scheduled", ScheduledSessionsPerWeek = group.Count() }
                : new MixedActivityScheduleDecisionV020(group.Key, "Scheduled", null, group.Count(), group.Count(),
                    group.Count(), []);
            // A session lost to the two-a-day cap is reported on its activity, never dropped quietly.
            return reducedActivities.Contains(group.Key)
                ? decision with
                {
                    ReasonCodes = decision.ReasonCodes.Append(ActivityFrequencyLimitedByAvailableWeekdaysCode)
                        .Distinct(StringComparer.Ordinal).ToArray()
                }
                : decision;
        }).ToArray();
        plan = plan with { PrescriptionHash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
            { plan.PolicyVersion, plan.InputContractVersion, plan.CatalogVersion, plan.Sessions,
                plan.LimitationCodes, plan.AdvisoryCodes, plan.ReadinessAdaptationApplied }))) };
        var usable = strength.Count > 0 && strength.All(session => session.Slots.Count > 0) &&
            plan.Status is nameof(WorkoutGenerationStatusV2.Feasible) or nameof(WorkoutGenerationStatusV2.Limited);
        return preview with
        {
            GenerationV2 = plan,
            Sessions = strength,
            MixedActivitySchedule = new(sessions, decisions, preview.MixedActivitySchedule?.InputReasonCodes ?? []),
            Status = usable ? preview.Status : "Limited",
            LimitationCodes = usable ? preview.LimitationCodes : preview.LimitationCodes.Concat(plan.LimitationCodes)
                .Append("NoSafeStrengthContentRemaining").Distinct(StringComparer.Ordinal).ToArray(),
            AdvisoryCodes = preview.AdvisoryCodes.Concat(plan.AdvisoryCodes).Concat(plan.LimitationCodes)
                .Distinct(StringComparer.Ordinal).OrderBy(code => code, StringComparer.Ordinal).ToArray(),
            RequiresReducedVolumeAcknowledgment = preview.RequiresReducedVolumeAcknowledgment ||
                plan.Status == "Limited" || volumeReducedForCapacity
        };
    }

    /// <summary>
    /// The owner rule allows two activities a day. V2 promotes a strength session onto every
    /// available weekday as training history grows, so once companions occupy the week the plan can
    /// ask for more sessions than the week holds. Strength gives way to the free slots and the
    /// reduction is reported, because the accept path validates this same week and would otherwise
    /// refuse the whole proposal (ProposalMixedScheduleInvalid).
    /// </summary>
    private static GenerationPlanV2 LimitWeekToCapacity(GenerationPlanV2 plan)
    {
        var maxPerDay = MixedActivityWeekScheduleRule.MaximumSessionsPerDay;
        var weekdays = plan.Sessions.Select(session => session.Weekday).Distinct().OrderBy(day => day).ToArray();
        var sessions = plan.Sessions.OrderBy(session => session.SessionIndex).ToList();
        var strengthReduced = false;

        var companionCount = sessions.Count(session => session.Exercises.Count == 0);
        var allowedStrength = weekdays.Length * maxPerDay - companionCount;
        var strength = sessions.Where(session => session.Exercises.Count > 0)
            .OrderBy(session => session.SessionIndex).ToArray();
        if (allowedStrength < strength.Length)
        {
            // A plan with no strength cannot be applied, so one session always survives.
            var kept = strength.Take(Math.Max(1, allowedStrength)).ToHashSet();
            sessions = sessions.Where(session => session.Exercises.Count == 0 || kept.Contains(session)).ToList();
            strengthReduced = true;
        }

        // A day holds at most two activities and never the same activity twice. A companion that
        // lands on a strength day moves to a day with room rather than disappearing.
        foreach (var day in sessions.GroupBy(session => session.Weekday)
                     .Where(group => group.Count() > maxPerDay).ToArray())
        {
            var ordered = day.OrderBy(session => session.Exercises.Count > 0 ? 0 : 1)
                .ThenBy(session => session.SessionIndex).ToArray();
            foreach (var session in ordered.Skip(maxPerDay))
            {
                var target = weekdays.FirstOrDefault(candidate => candidate != session.Weekday &&
                    sessions.Count(other => other.Weekday == candidate) < maxPerDay &&
                    sessions.All(other => other.Weekday != candidate ||
                        other.ActivityTypeId != session.ActivityTypeId));
                if (target == default) sessions.Remove(session);
                else sessions[sessions.IndexOf(session)] = session with { Weekday = target };
            }
        }

        var orderedSessions = sessions.OrderBy(session => session.SessionIndex).ToArray();
        if (!strengthReduced && orderedSessions.SequenceEqual(plan.Sessions)) return plan;
        return plan with
        {
            Sessions = orderedSessions,
            SessionCount = orderedSessions.Length,
            AdvisoryCodes = strengthReduced
                ? plan.AdvisoryCodes.Append(StrengthSessionsExceedAvailableWeekdaysCode)
                    .Distinct(StringComparer.Ordinal).OrderBy(code => code, StringComparer.Ordinal).ToArray()
                : plan.AdvisoryCodes
        };
    }

    public static GenerationPlanV2 ToPlan(WorkoutGenerationResultV2 generation,
        StrengthPlanPreviewSourceV020 source)
    {
        var unilateralIds = source.Catalog.Where(entry => entry.Candidate.UnilateralDoublesTime)
            .Select(entry => entry.Candidate.ExerciseId).ToHashSet();
        return new(generation.Status.ToString(), generation.PolicyVersion, WorkoutPolicyV2.InputContractVersion,
            generation.CatalogVersion, generation.AdaptedSessions.Count, generation.NormalizedPrescriptionHash,
            generation.LimitationCodes, generation.AdvisoryCodes, generation.RuleTrace)
        {
            Sessions = generation.AdaptedSessions.Select(session => new GenerationSessionV2(
                session.SessionIndex, session.Weekday, session.LocalDate, (int)session.Activity,
                session.GeneralWarmupMinutes, session.EstimatedTotalMinutes,
                session.Exercises.Select(exercise => new GenerationExerciseV2(
                    exercise.OrderIndex, exercise.ExerciseId, exercise.ExerciseName, exercise.WarmUpSets,
                    exercise.WorkingSets, exercise.MinRepetitions, exercise.MaxRepetitions,
                    exercise.TargetRir, exercise.RestSeconds, exercise.LoadStatus.ToString(),
                    exercise.PrescribedLoadKg, exercise.SupersetGroupId,
                    unilateralIds.Contains(exercise.ExerciseId), exercise.SelectionReasonCode)
                    {
                        IsOptional = !exercise.IsPriorityOrCompound, Pattern = exercise.Pattern.ToString(),
                        SlotId = $"v2-{LogicalSlotKey(exercise.Pattern.ToString())}-{session.Exercises.Count(other =>
                            LogicalSlotKey(other.Pattern.ToString()) == LogicalSlotKey(exercise.Pattern.ToString()) &&
                            other.OrderIndex < exercise.OrderIndex)}"
                    }).ToArray(),
                session.AttachedFinisher is { } finisher ? new GenerationFinisherV2(
                    (int)finisher.Activity, finisher.DurationMinutes, finisher.IntensityBand.ToString(),
                    finisher.TargetRpeMin, finisher.TargetRpeMax) : null)).ToArray(),
            MissingGaps = generation.LimitationCodes.Concat(generation.AdvisoryCodes.Where(code =>
                    code.StartsWith("WeeklyCoverage", StringComparison.Ordinal) ||
                    code.StartsWith("WeeklyMovementCoverage", StringComparison.Ordinal)))
                .Distinct(StringComparer.Ordinal).OrderBy(code => code, StringComparer.Ordinal).ToArray(),
            ReadinessAdaptationApplied = generation.ReadinessAdaptationApplied,
            CoachExplanation = generation.CoachExplanation
        };
    }

    public static IReadOnlyList<StrengthPlanPreviewSessionV020> ToStrengthSessions(GenerationPlanV2 plan) =>
        plan.Sessions.Where(session => session.Exercises.Count > 0).Select(session =>
            new StrengthPlanPreviewSessionV020("MC-V2", plan.PolicyVersion, session.SessionIndex,
                session.EstimatedTotalMinutes, session.Exercises.Select(exercise =>
                    new StrengthPlanSlotOutputV020(string.IsNullOrWhiteSpace(exercise.SlotId)
                            ? $"v2-{LogicalSlotKey(exercise.Pattern)}-{session.Exercises.Count(other =>
                                LogicalSlotKey(other.Pattern) == LogicalSlotKey(exercise.Pattern) && other.OrderIndex < exercise.OrderIndex)}"
                            : exercise.SlotId,
                        (int)LogicalSlot(exercise.Pattern), !exercise.IsOptional,
                        (int)(exercise.IsOptional ? StrengthPrescriptionRole.Accessory : StrengthPrescriptionRole.Main), false,
                        exercise.ExerciseId, exercise.ExerciseName, exercise.SelectionReasonCode,
                        plan.PolicyVersion, exercise.WorkingSets, exercise.MinRepetitions,
                        exercise.MaxRepetitions, exercise.TargetRir, exercise.RestSeconds, [])).ToArray(),
                [], new Dictionary<string, string>(), session.Weekday)).ToArray();

    private static string LogicalSlotKey(string pattern) => LogicalSlot(pattern) is { } slot &&
        slot != StrengthMovementSlotDto.GoalSpecificAccessory ? slot.ToString() : pattern;

    private static StrengthMovementSlotDto LogicalSlot(string pattern) => pattern switch
    {
        nameof(MovementPattern.SquatPattern) or nameof(MovementPattern.Lunge) => StrengthMovementSlotDto.KneeDominant,
        nameof(MovementPattern.HipHinge) or nameof(MovementPattern.HipThrust) => StrengthMovementSlotDto.HipDominant,
        nameof(MovementPattern.HorizontalPush) or nameof(MovementPattern.VerticalPush) => StrengthMovementSlotDto.UpperPush,
        nameof(MovementPattern.HorizontalPull) or nameof(MovementPattern.VerticalPull) => StrengthMovementSlotDto.UpperPull,
        nameof(MovementPattern.AntiExtension) or nameof(MovementPattern.AntiRotation) or
            nameof(MovementPattern.AntiLateralFlexion) => StrengthMovementSlotDto.Trunk,
        _ => StrengthMovementSlotDto.GoalSpecificAccessory
    };
}
