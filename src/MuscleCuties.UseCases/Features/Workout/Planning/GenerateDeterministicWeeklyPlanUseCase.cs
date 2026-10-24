using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.UseCases.Features.Workout.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Planning;

public sealed class GenerateDeterministicWeeklyPlanUseCase
{
    public Task<DeterministicWeeklyPlanResponseDto> ExecuteAsync(
        DeterministicWeeklyPlanRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var goal = Enum.TryParse<UserGoal>(request.Goal, ignoreCase: true, out var parsedGoal)
            ? parsedGoal
            : UserGoal.Strength;

        var exp = Enum.TryParse<TrainingExperienceLevel>(request.TrainingExperience, ignoreCase: true, out var parsedExp)
            ? parsedExp
            : TrainingExperienceLevel.Beginner;

        var equipment = Enum.TryParse<Equipment>(request.AvailableEquipment, ignoreCase: true, out var parsedEquip)
            ? parsedEquip
            : Equipment.FullGym;

        var style = Enum.TryParse<SessionTrainingStyle>(request.StrengthStyle, ignoreCase: true, out var parsedStyle)
            ? parsedStyle
            : SessionTrainingStyle.General;

        var domainActivities = request.PreferredActivities is { Count: > 0 }
            ? request.PreferredActivities.Select(WorkoutActivityContractMapper.ToCore).ToList()
            : [WorkoutActivityType.GeneralStrength];

        var coreInput = new WeeklyPlanGeneratorInput(
            Goal: goal,
            GoalPace: request.GoalPace,
            ProfileExperience: exp,
            PreferredActivities: domainActivities,
            GeneralDaysPerWeek: request.GeneralDaysPerWeek,
            PreferredSessionLengthMinutes: request.PreferredSessionLengthMinutes,
            AvailableEquipment: equipment,
            StrengthStyle: style,
            AvailableWeekdays: request.AvailableWeekdays,
            WarmupMinutes: request.WarmupMinutes,
            CooldownMinutes: request.CooldownMinutes,
            ExcludedExerciseIds: request.ExcludedExerciseIds?.ToHashSet(),
            ApplyChanges: request.ApplyChanges,
            PlanWeekStartDate: request.PlanWeekStartDate);

        var result = WeeklyWorkoutPlanGeneratorRule.Generate(coreInput);

        var response = MapToResponse(result);
        return Task.FromResult(response);
    }

    private static DeterministicWeeklyPlanResponseDto MapToResponse(DeterministicWeeklyPlanResult result)
    {
        var sessionDtos = result.Sessions.Select(s => new PlannedSessionDto(
            SessionId: s.SessionId,
            Activity: WorkoutActivityContractMapper.ToDto(s.Activity),
            DayOfWeek: s.DayOfWeek,
            EstimatedMinutes: s.EstimatedMinutes,
            IsHardSession: s.IsHardSession,
            Exercises: s.Exercises.Select(e => new PlannedExerciseDto(
                ExerciseId: e.ExerciseId,
                ExerciseName: e.ExerciseName,
                PrimaryMuscle: e.PrimaryMuscle,
                IsCompound: e.IsCompound,
                IsAccessory: e.IsAccessory,
                IsOptional: e.IsOptional,
                TargetSets: e.TargetSets,
                MinReps: e.MinReps,
                MaxReps: e.MaxReps,
                TargetRir: e.TargetRir,
                PrescribedLoadKg: e.PrescribedLoadKg,
                ProgressionAction: e.ProgressionAction.ToString(),
                ProgressionRationale: e.ProgressionRationale)).ToList(),
            Notes: s.Notes)).ToList();

        var setsByMuscle = result.HypertrophyVolumeSummary.VolumeByMuscle
            .ToDictionary(k => k.Key.ToString(), v => v.Value);

        var traceDtos = result.RuleTrace.Select(t => new RuleTraceEntryDto(
            StepNumber: t.StepNumber,
            StepName: t.StepName,
            DecisionSummary: t.DecisionSummary,
            Details: t.Details)).ToList();

        return new DeterministicWeeklyPlanResponseDto(
            PlanId: result.PlanId,
            PlanWeekStartDate: result.PlanWeekStartDate,
            GeneratedAtUtc: result.GeneratedAtUtc,
            Sessions: sessionDtos,
            HypertrophySetsByMuscle: setsByMuscle,
            HypertrophyAllMusclesSatisfied: result.HypertrophyVolumeSummary.AllTargetedMusclesWithinBounds,
            WeeklyModerateEquivalentMinutes: result.AerobicHealthSummary.TotalModerateEquivalentMinutes,
            MeetsWhoHealthTargets: result.MeetsWhoHealthTargets,
            IsDeloadWeek: result.IsDeloadWeek,
            DeloadReason: result.DeloadSummary.TriggerReason,
            EffectivePrecedenceTier: result.PrecedenceSummary.EffectiveTier.ToString(),
            IsPrecedenceApplied: result.PrecedenceSummary.IsApplied,
            HasSafetyBlock: result.HasSafetyBlock,
            RuleTrace: traceDtos);
    }
}
