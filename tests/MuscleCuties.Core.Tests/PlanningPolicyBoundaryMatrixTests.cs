using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class PlanningPolicyBoundaryMatrixTests
{
    private sealed class FixedClockPort(DateOnly date, DateTimeOffset now) : IPlanningClockPort
    {
        public DateOnly GetCurrentLocalDate() => date;
        public DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public void ClockPort_ProducesDeterministicEvaluationAcrossRepeatedCalls()
    {
        var fixedDate = new DateOnly(2026, 11, 4);
        var fixedTime = new DateTimeOffset(2026, 11, 4, 14, 30, 0, TimeSpan.Zero);
        var clock = new FixedClockPort(fixedDate, fixedTime);

        Assert.Equal(fixedDate, clock.GetCurrentLocalDate());
        Assert.Equal(fixedTime, clock.GetUtcNow());
    }

    [Fact]
    public void SystemClockPort_DefaultsToSystemTimeProvider()
    {
        var clock = new SystemPlanningClockPort();
        var date = clock.GetCurrentLocalDate();
        var utc = clock.GetUtcNow();

        Assert.True(date.Year >= 2026);
        Assert.True(utc.Year >= 2026);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void CatalogPort_ReturnsReviewedTemplatesForSchedule(int daysPerWeek)
    {
        var catalog = new DefaultPlanningCatalogPort();
        var templates = catalog.GetTemplatesForSchedule(daysPerWeek);

        Assert.NotNull(templates);
        Assert.NotEmpty(templates);
        Assert.All(templates, template =>
        {
            Assert.False(string.IsNullOrWhiteSpace(template.TemplateId));
            Assert.NotEmpty(template.Slots);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void CatalogPort_ReturnsEmptyForUnsupportedDays(int daysPerWeek)
    {
        var catalog = new DefaultPlanningCatalogPort();
        var templates = catalog.GetTemplatesForSchedule(daysPerWeek);

        Assert.NotNull(templates);
        Assert.Empty(templates);
    }

    [Fact]
    public void SafetyPort_EnforcesActivityRestrictions_BasedOnInjuries()
    {
        var safetyPort = new DefaultPlanningSafetyPort();
        var injuries = new List<Injury>
        {
            new(InjuryFlag.Knee, InjuryStatus.Acute, new DateOnly(2026, 1, 1))
        };

        var restricted = safetyPort.EvaluateRestrictedActivities(injuries);

        Assert.Contains(WorkoutActivityType.Hiit, restricted);
        Assert.Contains(WorkoutActivityType.Running, restricted);
        Assert.Contains(WorkoutActivityType.RockClimbing, restricted);
        Assert.True(safetyPort.IsActivityRestricted(WorkoutActivityType.Hiit, restricted, false));
        Assert.False(safetyPort.IsActivityRestricted(WorkoutActivityType.Yoga, restricted, false));
    }

    [Fact]
    public void SafetyPort_RestrictsMovementPatterns_WhenInBlockedSet()
    {
        var safetyPort = new DefaultPlanningSafetyPort();
        var blocked = new HashSet<MovementPattern> { MovementPattern.SquatPattern, MovementPattern.HipHinge };

        Assert.False(safetyPort.IsMovementPatternAllowed(MovementPattern.SquatPattern, blocked));
        Assert.False(safetyPort.IsMovementPatternAllowed(MovementPattern.HipHinge, blocked));
        Assert.True(safetyPort.IsMovementPatternAllowed(MovementPattern.HorizontalPush, blocked));
        Assert.True(safetyPort.IsMovementPatternAllowed(MovementPattern.VerticalPull, null));
    }

    [Fact]
    public void SafetyPort_FallsBackToCurrentSymptoms_WhenRestrictedSetNull()
    {
        var safetyPort = new DefaultPlanningSafetyPort();

        Assert.True(safetyPort.IsActivityRestricted(WorkoutActivityType.Running, null, true));
        Assert.False(safetyPort.IsActivityRestricted(WorkoutActivityType.Running, null, false));
    }

    [Fact]
    public void EmptyAndMissingInputs_EvaluatePredictablyWithoutThrowing()
    {
        var result = StrengthPlanPreviewRule.Evaluate(null, null);

        Assert.Equal(StrengthPlanPreviewStatus.InvalidInput, result.Status);
        Assert.Contains(StrengthPlanPreviewRule.InvalidInputCode, result.LimitationCodes);
    }
}
