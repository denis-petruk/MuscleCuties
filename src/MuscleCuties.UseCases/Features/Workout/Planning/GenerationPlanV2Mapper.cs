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
        var strengthReducedForCapacity = !ReferenceEquals(plan, beforeCapacity);
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
            preview.MixedActivitySchedule?.Activities.FirstOrDefault(item => item.ActivityTypeId == group.Key) is { } previous
                ? previous with { Disposition = "Scheduled", ScheduledSessionsPerWeek = group.Count() }
                : new MixedActivityScheduleDecisionV020(group.Key, "Scheduled", null, group.Count(), group.Count(),
                    group.Count(), [])).ToArray();
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
                plan.Status == "Limited" || strengthReducedForCapacity
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
        var weekdays = plan.Sessions.Select(session => session.Weekday).Distinct().Count();
        var capacity = weekdays * MixedActivityWeekScheduleRule.MaximumSessionsPerDay;
        var strength = plan.Sessions.Where(session => session.Exercises.Count > 0)
            .OrderBy(session => session.SessionIndex).ToArray();
        var companions = plan.Sessions.Where(session => session.Exercises.Count == 0).ToArray();
        var allowedStrength = capacity - companions.Length;
        if (allowedStrength >= strength.Length) return plan;

        // A plan with no strength cannot be applied, so when companions alone fill the week the
        // newest companions make room for the coverage minimum instead of every strength session.
        var keptStrength = strength.Take(Math.Max(1, allowedStrength)).ToArray();
        var keptCompanions = allowedStrength >= 1
            ? companions
            : companions.Take(Math.Max(0, capacity - keptStrength.Length)).ToArray();
        var kept = keptStrength.Concat(keptCompanions)
            .OrderBy(session => session.SessionIndex).ToArray();
        return plan with
        {
            Sessions = kept,
            SessionCount = kept.Length,
            AdvisoryCodes = plan.AdvisoryCodes.Append(StrengthSessionsExceedAvailableWeekdaysCode)
                .Distinct(StringComparer.Ordinal).OrderBy(code => code, StringComparer.Ordinal).ToArray()
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
