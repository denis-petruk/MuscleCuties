using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class SevenDayWorkoutPreferenceTests
{
    private const int SevenDaysPerWeek = 7;

    [Fact]
    public void SevenDaysIsAValidPreferenceWithSixStrengthDaysAndRecovery()
    {
        Assert.Equal(SevenDaysPerWeek, WeekPlanRule.ClampDaysPerWeek(SevenDaysPerWeek));
        Assert.True(WorkoutPreferencesRule.IsValid(
            [(int)WorkoutActivityType.HighVolumeStrength],
            (int)StrengthTrainingStyle.ComfortableModerate,
            SevenDaysPerWeek));

        var template = new PlanningWeekTemplate(6, SevenDaysPerWeek, "1,3,6,2,4,5,0");
        Assert.True(WeekPlanRule.TryValidateTemplate(template, SevenDaysPerWeek,
            out var sequence, out var failure), failure?.Validation.Entries.FirstOrDefault()?.Message);
        Assert.Equal(SevenDaysPerWeek, sequence.Length);
        Assert.Single(sequence, id => id == 0);

        var strengthIds = sequence.Where(id => id > 0).ToArray();
        var archetypes = strengthIds.Select(id => new PlanningArchetype(
            id, $"A{id}", $"Archetype {id}", false, false, [])).ToArray();
        var exposureMap = strengthIds.ToDictionary(id => id,
            _ => (IReadOnlySet<int>)new HashSet<int>());
        var days = Enum.GetValues<DayOfWeek>();

        var mapped = WeekScheduleRule.MapArchetypesToDays(sequence, archetypes, days,
            exposureMap, new HashSet<int>(), false, SevenDaysPerWeek);

        Assert.Equal(strengthIds.Length, mapped.Count);
        Assert.Equal(mapped.Count, mapped.Select(item => item.Day).Distinct().Count());
    }
}
