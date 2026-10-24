using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class WeeklyWorkoutPlanGeneratorRuleTests
{
    [Fact]
    public void Generate_NullInput_ProducesValidConservativePlanWithCompleteTrace()
    {
        var result = WeeklyWorkoutPlanGeneratorRule.Generate(null);

        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.PlanId);
        Assert.NotEmpty(result.Sessions);
        Assert.False(result.IsDeloadWeek);
        Assert.Equal(WeeklyWorkoutPlanGeneratorRule.Step7PlanFinalizationAndTrace, result.RuleTrace.Count);
        Assert.Equal("InputAggregation", result.RuleTrace[0].StepName);
        Assert.Equal("PlanFinalization", result.RuleTrace[6].StepName);
    }

    [Fact]
    public void Generate_ThreeDaysStrength_BuildsFittedSessionsAndVolume()
    {
        var input = new WeeklyPlanGeneratorInput(
            Goal: UserGoal.Strength,
            GoalPace: "Moderate",
            ProfileExperience: TrainingExperienceLevel.Intermediate,
            PreferredActivities: [WorkoutActivityType.GeneralStrength],
            GeneralDaysPerWeek: 3,
            PreferredSessionLengthMinutes: 60,
            AvailableEquipment: Equipment.FullGym,
            StrengthStyle: SessionTrainingStyle.General,
            AvailableWeekdays: [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday],
            WarmupMinutes: 5,
            CooldownMinutes: 5);

        var result = WeeklyWorkoutPlanGeneratorRule.Generate(input);

        Assert.Equal(3, result.Sessions.Count);
        Assert.All(result.Sessions, s => Assert.Equal(WorkoutActivityType.GeneralStrength, s.Activity));
        Assert.All(result.Sessions, s => Assert.NotEmpty(s.Exercises));
        Assert.Contains(result.RuleTrace, t => t.StepName == "StrengthSessionConstruction");
        Assert.Contains(result.RuleTrace, t => t.StepName == "MixedActivityPackingAndVolume");
    }

    [Fact]
    public void Generate_TightTimeBudget_TrimsOptionalAccessories()
    {
        // 20 min session with 5m warmup and 5m cooldown leaves only 10m work budget
        var input = new WeeklyPlanGeneratorInput(
            Goal: UserGoal.FatLoss,
            GoalPace: null,
            ProfileExperience: TrainingExperienceLevel.Beginner,
            PreferredActivities: [WorkoutActivityType.GeneralStrength],
            GeneralDaysPerWeek: 2,
            PreferredSessionLengthMinutes: 20,
            AvailableEquipment: Equipment.FullGym,
            StrengthStyle: SessionTrainingStyle.General,
            AvailableWeekdays: [DayOfWeek.Tuesday, DayOfWeek.Thursday],
            WarmupMinutes: 5,
            CooldownMinutes: 5);

        var result = WeeklyWorkoutPlanGeneratorRule.Generate(input);

        Assert.NotEmpty(result.Sessions);
        // Optional exercises should be trimmed in a very tight session
        foreach (var session in result.Sessions)
        {
            Assert.DoesNotContain(session.Exercises, e => e.IsOptional);
        }
    }

    [Fact]
    public void Generate_MixedActivities_PacksCardioWithHardDayCap()
    {
        var input = new WeeklyPlanGeneratorInput(
            Goal: UserGoal.MaintainHealth,
            GoalPace: null,
            ProfileExperience: TrainingExperienceLevel.Intermediate,
            PreferredActivities:
            [
                WorkoutActivityType.GeneralStrength,
                WorkoutActivityType.Running,
                WorkoutActivityType.Hiit,
                WorkoutActivityType.Yoga
            ],
            GeneralDaysPerWeek: 4,
            PreferredSessionLengthMinutes: 45,
            AvailableEquipment: Equipment.FullGym,
            StrengthStyle: SessionTrainingStyle.General,
            AvailableWeekdays: [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]);

        var result = WeeklyWorkoutPlanGeneratorRule.Generate(input);

        Assert.True(result.Sessions.Count >= 3);
        var hardSessions = result.Sessions.Count(s => s.IsHardSession);
        Assert.True(hardSessions <= WeeklyWorkoutPlanGeneratorRule.MaxHardDaysPerWeek);
    }

    [Fact]
    public void Generate_TwoWeeksUnderperformanceAndPoorRecovery_TriggersDeloadWeek()
    {
        var recoveryHistory = new List<WeeklyPerformanceAndRecoveryRecord>
        {
            new(WeekIndex: 1, CompoundUnderperformed: true, ExplicitPoorRecovery: true),
            new(WeekIndex: 2, CompoundUnderperformed: true, ExplicitPoorRecovery: true)
        };

        var input = new WeeklyPlanGeneratorInput(
            Goal: UserGoal.Strength,
            GoalPace: null,
            ProfileExperience: TrainingExperienceLevel.Intermediate,
            PreferredActivities: [WorkoutActivityType.GeneralStrength],
            GeneralDaysPerWeek: 3,
            PreferredSessionLengthMinutes: 60,
            AvailableEquipment: Equipment.FullGym,
            StrengthStyle: SessionTrainingStyle.General,
            AvailableWeekdays: [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday],
            RecoveryHistory: recoveryHistory);

        var result = WeeklyWorkoutPlanGeneratorRule.Generate(input);

        Assert.True(result.IsDeloadWeek);
        Assert.True(result.DeloadSummary.IsDeloadTriggered);
        Assert.All(result.Sessions.SelectMany(s => s.Exercises), e =>
        {
            Assert.Equal(ProgressionAction.Regress, e.ProgressionAction);
            Assert.Contains("Deload active", e.ProgressionRationale);
        });
    }

    [Fact]
    public void Generate_PriorSuccessPerformance_TriggersDoubleProgressionLoadIncrease()
    {
        var priorPerformances = new Dictionary<int, PriorExercisePerformanceRecord>
        {
            [1] = new(
                ExerciseId: 1,
                PreviousLoadKg: 50.0m,
                PreviousReps: 12,
                PreviousRir: 3,
                TechnicalFailureObserved: false,
                ConsecutiveSuccessWeeks: 2)
        };

        var input = new WeeklyPlanGeneratorInput(
            Goal: UserGoal.Strength,
            GoalPace: null,
            ProfileExperience: TrainingExperienceLevel.Intermediate,
            PreferredActivities: [WorkoutActivityType.GeneralStrength],
            GeneralDaysPerWeek: 3,
            PreferredSessionLengthMinutes: 60,
            AvailableEquipment: Equipment.FullGym,
            StrengthStyle: SessionTrainingStyle.General,
            AvailableWeekdays: [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday],
            PriorExercisePerformances: priorPerformances);

        var result = WeeklyWorkoutPlanGeneratorRule.Generate(input);

        var squat = result.Sessions.SelectMany(s => s.Exercises).FirstOrDefault(e => e.ExerciseId == 1);
        Assert.NotNull(squat);
        Assert.Equal(ProgressionAction.Advance, squat.ProgressionAction);
        Assert.True(squat.PrescribedLoadKg > 50.0m);
    }

    [Fact]
    public void Generate_AcuteInjury_BlocksProgressionAndMarksSafetyIntervention()
    {
        var activeInjuries = new List<PlannerInjuryRecord>
        {
            new(
                Site: "Knee",
                PainLevel: 8,
                Status: InjurySafetyStatus.Acute,
                HasNextDayWorsening: true)
        };

        var input = new WeeklyPlanGeneratorInput(
            Goal: UserGoal.Strength,
            GoalPace: null,
            ProfileExperience: TrainingExperienceLevel.Intermediate,
            PreferredActivities: [WorkoutActivityType.GeneralStrength],
            GeneralDaysPerWeek: 3,
            PreferredSessionLengthMinutes: 60,
            AvailableEquipment: Equipment.FullGym,
            StrengthStyle: SessionTrainingStyle.General,
            AvailableWeekdays: [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday],
            ActiveInjuries: activeInjuries);

        var result = WeeklyWorkoutPlanGeneratorRule.Generate(input);

        Assert.True(result.HasSafetyBlock);
        Assert.Equal(PrecedenceProposalStatus.BlockedBySafety, result.PrecedenceSummary.Status);
    }

    [Fact]
    public async Task UseCase_ExecuteAsync_ProducesCleanResponseWithoutCoreLeakage()
    {
        var useCase = new GenerateDeterministicWeeklyPlanUseCase();
        var request = new DeterministicWeeklyPlanRequestDto(
            Goal: "Strength",
            GoalPace: "Moderate",
            TrainingExperience: "Intermediate",
            PreferredActivities: [WorkoutActivityDto.GeneralStrength, WorkoutActivityDto.Running],
            GeneralDaysPerWeek: 3,
            PreferredSessionLengthMinutes: 50,
            AvailableEquipment: "FullGym",
            StrengthStyle: "General",
            AvailableWeekdays: [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday],
            WarmupMinutes: 5,
            CooldownMinutes: 5,
            ApplyChanges: true);

        var response = await useCase.ExecuteAsync(request);

        Assert.NotNull(response);
        Assert.NotEqual(Guid.Empty, response.PlanId);
        Assert.NotEmpty(response.Sessions);
        Assert.NotEmpty(response.RuleTrace);
        Assert.True(response.IsPrecedenceApplied);
    }
}
