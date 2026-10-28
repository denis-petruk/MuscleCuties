using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Domain.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class WorkoutPopulationScopeRuleTests
{
    private static readonly DateOnly Today = new(2026, 10, 28);

    [Theory]
    [InlineData(2008, 10, 29, WorkoutPopulationScopeRule.AgeUnsupported)]
    [InlineData(2008, 10, 28, null)]
    [InlineData(1961, 10, 29, null)]
    [InlineData(1961, 10, 28, WorkoutPopulationScopeRule.AgeUnsupported)]
    public void Age_scope_uses_account_local_birthday(int year, int month, int day, string? expected)
    {
        Assert.Equal(expected, WorkoutPopulationScopeRule.Limitation(new DateOnly(year, month, day), Today, PregnancyStatus.No));
    }

    [Fact]
    public void Unknown_or_future_birth_date_requests_profile_update()
    {
        Assert.Equal(WorkoutPopulationScopeRule.BirthDateRequired,
            WorkoutPopulationScopeRule.Limitation(null, Today, PregnancyStatus.Unknown));
        Assert.Equal(WorkoutPopulationScopeRule.BirthDateRequired,
            WorkoutPopulationScopeRule.Limitation(Today.AddDays(1), Today, PregnancyStatus.Unknown));
    }

    [Fact]
    public void Missing_account_local_date_is_distinct_from_missing_birth_date()
    {
        Assert.Equal(WorkoutPopulationScopeRule.DateUnavailable,
            WorkoutPopulationScopeRule.Limitation(new DateOnly(1990, 1, 1), null, PregnancyStatus.Unknown));
    }

    [Fact]
    public void Known_pregnancy_is_limited_while_unknown_is_not_inferred()
    {
        var birthDate = new DateOnly(1990, 1, 1);
        Assert.Equal(WorkoutPopulationScopeRule.PregnancyUnsupported,
            WorkoutPopulationScopeRule.Limitation(birthDate, Today, PregnancyStatus.Yes));
        Assert.Null(WorkoutPopulationScopeRule.Limitation(birthDate, Today, PregnancyStatus.Unknown));
    }
}
