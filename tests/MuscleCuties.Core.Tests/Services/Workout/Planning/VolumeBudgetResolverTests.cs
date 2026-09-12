using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Services.Workout.Planning;

namespace MuscleCuties.Core.Tests.Services.Workout.Planning;

public class VolumeBudgetResolverTests : IClassFixture<WorkoutPlanningDbFixture>
{
    private readonly WorkoutPlanningDbFixture _fixture;

    public VolumeBudgetResolverTests(WorkoutPlanningDbFixture fixture)
    {
        _fixture = fixture;
    }

    private VolumeBudgetResolver CreateResolver() => new(_fixture.Db);

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task ResolveBudgetAsync_AllDayCounts_ReturnsNonEmptyBudget(int days)
    {
        var resolver = CreateResolver();

        var budget = await resolver.ResolveBudgetAsync(
            days, TrainingExperienceLevel.Intermediate, UserGoal.MuscleTone);

        Assert.NotEmpty(budget);
        Assert.All(budget.Values, v => Assert.True(v >= 0));
    }

    [Fact]
    public async Task ResolveBudgetAsync_BeginnerModifier_LowerThanIntermediate()
    {
        var resolver = CreateResolver();

        var beginner = await resolver.ResolveBudgetAsync(
            4, TrainingExperienceLevel.Beginner, UserGoal.MuscleTone);
        var intermediate = await resolver.ResolveBudgetAsync(
            4, TrainingExperienceLevel.Intermediate, UserGoal.MuscleTone);

        var commonMuscle = beginner.Keys.Intersect(intermediate.Keys).First();
        Assert.True(beginner[commonMuscle] <= intermediate[commonMuscle]);
    }

    [Fact]
    public async Task ResolveBudgetAsync_AdvancedModifier_HigherThanIntermediate()
    {
        var resolver = CreateResolver();

        var advanced = await resolver.ResolveBudgetAsync(
            4, TrainingExperienceLevel.Advanced, UserGoal.MuscleTone);
        var intermediate = await resolver.ResolveBudgetAsync(
            4, TrainingExperienceLevel.Intermediate, UserGoal.MuscleTone);

        var commonMuscle = advanced.Keys.Intersect(intermediate.Keys).First();
        Assert.True(advanced[commonMuscle] >= intermediate[commonMuscle]);
    }

    [Fact]
    public async Task ResolveBudgetAsync_GlutesCeiling_NeverExceeds26()
    {
        var resolver = CreateResolver();

        var budget = await resolver.ResolveBudgetAsync(
            6, TrainingExperienceLevel.Advanced, UserGoal.Strength);

        if (budget.TryGetValue(1, out var gluteSets))
            Assert.True(gluteSets <= 26.0, $"Glutes budget {gluteSets} exceeds ceiling of 26");
    }

    [Fact]
    public async Task ResolveBudgetAsync_DefaultCeiling_NeverExceeds22()
    {
        var resolver = CreateResolver();

        var budget = await resolver.ResolveBudgetAsync(
            6, TrainingExperienceLevel.Advanced, UserGoal.MuscleTone);

        foreach (var (muscleId, sets) in budget)
        {
            var ceiling = muscleId == 1 ? 26.0 : 22.0;
            Assert.True(sets <= ceiling, $"Muscle {muscleId} budget {sets} exceeds ceiling of {ceiling}");
        }
    }

    [Fact]
    public async Task ResolveBudgetAsync_ShortSession_ScalesTierC()
    {
        var resolver = CreateResolver();

        var normal = await resolver.ResolveBudgetAsync(
            4, TrainingExperienceLevel.Intermediate, UserGoal.MuscleTone, sessionMinutesTarget: 60);
        var shortSession = await resolver.ResolveBudgetAsync(
            4, TrainingExperienceLevel.Intermediate, UserGoal.MuscleTone, sessionMinutesTarget: 30);

        var totalNormal = normal.Values.Sum();
        var totalShort = shortSession.Values.Sum();
        Assert.True(totalShort <= totalNormal, "Short session budget should not exceed normal session budget");
    }
}
