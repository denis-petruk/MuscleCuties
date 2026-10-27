using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
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
    public void Generate_One_Available_Day_Blocks_Plan_Instead_Of_Generating_One_Strength_Session()
    {
        var input = new WeeklyPlanGeneratorInput(
            Goal: UserGoal.Strength,
            GoalPace: null,
            ProfileExperience: TrainingExperienceLevel.Beginner,
            PreferredActivities: [WorkoutActivityType.GeneralStrength, WorkoutActivityType.Cycling],
            GeneralDaysPerWeek: GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek,
            PreferredSessionLengthMinutes: WeeklyWorkoutPlanGeneratorRule.DefaultPreferredSessionLengthMinutes,
            AvailableEquipment: Equipment.FullGym,
            StrengthStyle: SessionTrainingStyle.General,
            AvailableWeekdays: [DayOfWeek.Monday],
            ApplyChanges: true);

        var result = WeeklyWorkoutPlanGeneratorRule.Generate(input);

        Assert.True(result.HasSafetyBlock);
        Assert.Empty(result.Sessions);
        Assert.False(result.PrecedenceSummary.IsApplied);
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
            RecoveryHistory: recoveryHistory,
            EnableAutomaticDeload: true);

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
    public void Generate_DeloadRemainsInactive_WhenNotExplicitlyEnabled()
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
            RecoveryHistory: recoveryHistory,
            EnableAutomaticDeload: false);

        var result = WeeklyWorkoutPlanGeneratorRule.Generate(input);

        // Deload trigger is recognized in summary, but NOT applied to sessions when inactive
        Assert.True(result.DeloadSummary.IsDeloadTriggered);
        Assert.All(result.Sessions.SelectMany(s => s.Exercises), e =>
        {
            Assert.NotEqual(ProgressionAction.Regress, e.ProgressionAction);
            Assert.DoesNotContain("Deload active", e.ProgressionRationale);
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
    public void Generate_CardioSessions_DoNotUseUserPickedStrengthLength_GenerateOwnDuration()
    {
        var input = new WeeklyPlanGeneratorInput(
            Goal: UserGoal.FatLoss,
            GoalPace: "Moderate",
            ProfileExperience: TrainingExperienceLevel.Beginner,
            PreferredActivities: [WorkoutActivityType.GeneralStrength, WorkoutActivityType.Running, WorkoutActivityType.Cycling],
            GeneralDaysPerWeek: 2,
            PreferredSessionLengthMinutes: 75,
            AvailableEquipment: Equipment.FullGym,
            StrengthStyle: SessionTrainingStyle.General,
            AvailableWeekdays: [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday, DayOfWeek.Saturday],
            WarmupMinutes: 5,
            CooldownMinutes: 5);

        var result = WeeklyWorkoutPlanGeneratorRule.Generate(input);

        var runningSession = result.Sessions.FirstOrDefault(s => s.Activity == WorkoutActivityType.Running);
        var cyclingSession = result.Sessions.FirstOrDefault(s => s.Activity == WorkoutActivityType.Cycling);

        Assert.NotNull(runningSession);
        Assert.NotNull(cyclingSession);
        // Beginner continuous cardio duration is 20 minutes (10 warmup/cooldown + 10 main), NOT 75 minutes
        Assert.Equal(20, runningSession.EstimatedMinutes);
        Assert.Equal(20, cyclingSession.EstimatedMinutes);
    }
}
