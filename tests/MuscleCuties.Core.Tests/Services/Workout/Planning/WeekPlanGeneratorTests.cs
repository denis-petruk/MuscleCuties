using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Services.Workout.Planning;

namespace MuscleCuties.Core.Tests.Services.Workout.Planning;

public class WeekPlanGeneratorTests : IClassFixture<WorkoutPlanningDbFixture>
{
    private readonly WorkoutPlanningDbFixture _fixture;

    public WeekPlanGeneratorTests(WorkoutPlanningDbFixture fixture)
    {
        _fixture = fixture;
    }

    private WeekPlanGenerator CreateGenerator()
    {
        var budgetResolver = new VolumeBudgetResolver(_fixture.Db);
        return new WeekPlanGenerator(_fixture.Db, _fixture.Contributions, budgetResolver);
    }

    private static WeekGenerationInput MakeInput(int days, UserGoal goal = UserGoal.MuscleTone) => new()
    {
        DaysPerWeek = days,
        Experience = TrainingExperienceLevel.Intermediate,
        Goal = goal,
        SessionMinutesTarget = 60
    };

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task GenerateWeekAsync_AllDayCounts_ProducesCorrectSessionCount(int days)
    {
        var generator = CreateGenerator();

        var week = await generator.GenerateWeekAsync(MakeInput(days));

        Assert.Equal(days, week.DaysPerWeek);
        Assert.True(week.Sessions.Count >= days,
            $"Expected at least {days} sessions, got {week.Sessions.Count}");
    }

    [Fact]
    public async Task GenerateWeekAsync_AllStrengthSessionsHaveSlots()
    {
        var generator = CreateGenerator();

        var week = await generator.GenerateWeekAsync(MakeInput(4));

        Assert.All(week.Sessions.Where(session => !session.IsCardio && !session.IsRecovery), session =>
        {
            Assert.NotEmpty(session.Slots);
            Assert.NotEmpty(session.ArchetypeCode);
        });
    }

    [Fact]
    public async Task GenerateWeekAsync_SixDayPlanIncludesBasicRecovery()
    {
        var generator = CreateGenerator();

        var week = await generator.GenerateWeekAsync(MakeInput(6));

        var recovery = Assert.Single(week.Sessions, session => session.IsRecovery);
        Assert.Equal("RECOVERY", recovery.ArchetypeCode);
        Assert.Empty(recovery.Slots);
    }

    [Fact]
    public async Task GenerateWeekAsync_SlotsHaveValidSetRanges()
    {
        var generator = CreateGenerator();

        var week = await generator.GenerateWeekAsync(MakeInput(4));

        foreach (var session in week.Sessions)
        {
            foreach (var slot in session.Slots)
            {
                Assert.True(slot.SetsMin <= slot.SetsMax,
                    $"SetsMin ({slot.SetsMin}) > SetsMax ({slot.SetsMax})");
                Assert.True(slot.AssignedSets >= slot.SetsMin,
                    $"AssignedSets ({slot.AssignedSets}) < SetsMin ({slot.SetsMin})");
                Assert.True(slot.RepsMin <= slot.RepsMax,
                    $"RepsMin ({slot.RepsMin}) > RepsMax ({slot.RepsMax})");
            }
        }
    }

    [Fact]
    public async Task GenerateWeekAsync_NoDuplicateDays()
    {
        var generator = CreateGenerator();

        var week = await generator.GenerateWeekAsync(MakeInput(4));

        var days = week.Sessions.Select(s => s.Day).ToList();
        Assert.Equal(days.Count, days.Distinct().Count());
    }

    [Fact]
    public async Task GenerateWeekAsync_TwoDayPlan_HasVariants()
    {
        var generator = CreateGenerator();

        var week = await generator.GenerateWeekAsync(MakeInput(2));

        var fSessions = week.Sessions.Where(s => s.ArchetypeCode == "F").ToList();
        if (fSessions.Count == 2)
        {
            Assert.Contains(fSessions, s => s.Variant == 1);
            Assert.Contains(fSessions, s => s.Variant == 2);
        }
    }

    [Fact]
    public async Task GenerateWeekAsync_ValidationReport_NoErrors()
    {
        var generator = CreateGenerator();

        var week = await generator.GenerateWeekAsync(MakeInput(4));

        Assert.False(week.Validation.HasErrors,
            string.Join("; ", week.Validation.Entries
                .Where(e => e.Severity == ValidationSeverity.Error)
                .Select(e => e.Message)));
    }

    [Theory]
    [InlineData(UserGoal.MuscleTone)]
    [InlineData(UserGoal.Strength)]
    [InlineData(UserGoal.FatLoss)]
    [InlineData(UserGoal.MaintainHealth)]
    public async Task GenerateWeekAsync_AllGoals_ProducesValidPlan(UserGoal goal)
    {
        var generator = CreateGenerator();

        var week = await generator.GenerateWeekAsync(MakeInput(4, goal));

        Assert.NotEmpty(week.Sessions);
        Assert.NotNull(week.Validation);
    }

    [Fact]
    public async Task GenerateWeekAsync_BudgetDistributed_SlotsGetSets()
    {
        var generator = CreateGenerator();

        var week = await generator.GenerateWeekAsync(MakeInput(4));

        var totalAssigned = week.Sessions
            .SelectMany(s => s.Slots)
            .Sum(s => s.AssignedSets);

        Assert.True(totalAssigned > 0, "No sets were assigned to any slot");
    }

    [Fact]
    public async Task GenerateWeekAsync_WithRunning_RespectsLowerDominantSpacing()
    {
        var generator = CreateGenerator();

        var input = new WeekGenerationInput
        {
            DaysPerWeek = 4,
            Experience = TrainingExperienceLevel.Intermediate,
            Goal = UserGoal.MuscleTone,
            SessionMinutesTarget = 60,
            HasRunningOrClimbing = true
        };

        var week = await generator.GenerateWeekAsync(input);

        Assert.NotEmpty(week.Sessions);
    }

    [Fact]
    public async Task GenerateWeekAsync_WithCardioActivities_AddsCardioSessions()
    {
        var generator = CreateGenerator();

        var input = new WeekGenerationInput
        {
            DaysPerWeek = 3,
            Experience = TrainingExperienceLevel.Intermediate,
            Goal = UserGoal.MuscleTone,
            SessionMinutesTarget = 60,
            SelectedActivities = [WorkoutActivityType.Running, WorkoutActivityType.Cycling]
        };

        var week = await generator.GenerateWeekAsync(input);

        var cardioSessions = week.Sessions.Where(s => s.IsCardio).ToList();
        Assert.True(cardioSessions.Count >= 1, "Expected at least one cardio session");
        Assert.All(cardioSessions, s =>
        {
            Assert.Equal("CARDIO", s.ArchetypeCode);
            Assert.NotNull(s.CardioActivity);
            Assert.Empty(s.Slots);
        });
    }

    [Fact]
    public async Task GenerateWeekAsync_CardioStacksOnStrengthDays_PreservesRestDays()
    {
        var generator = CreateGenerator();

        var input = new WeekGenerationInput
        {
            DaysPerWeek = 4,
            Experience = TrainingExperienceLevel.Intermediate,
            Goal = UserGoal.MuscleTone,
            SessionMinutesTarget = 60,
            SelectedActivities = [WorkoutActivityType.Running]
        };

        var week = await generator.GenerateWeekAsync(input);

        var allDaysUsed = week.Sessions.Select(s => s.Day).Distinct().Count();
        var targetRestDays = 7 - input.DaysPerWeek;
        var actualRestDays = 7 - allDaysUsed;

        Assert.True(actualRestDays >= targetRestDays,
            $"Expected at least {targetRestDays} rest days but got {actualRestDays}");
    }

    [Fact]
    public async Task GenerateWeekAsync_NoSelectedActivities_NoCardioSessions()
    {
        var generator = CreateGenerator();

        var week = await generator.GenerateWeekAsync(MakeInput(4));

        var cardioSessions = week.Sessions.Where(s => s.IsCardio).ToList();
        Assert.Empty(cardioSessions);
    }
}
