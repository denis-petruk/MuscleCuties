using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;
using MuscleCuties.UseCases.Features.Workout.Contracts;

namespace MuscleCuties.Core.Tests;

public sealed class ActivityPreferencePolicyTests
{
    private const int RequestedSessionsPerWeek = 3;
    private const int InvalidSessionsPerWeek = 8;
    private const int YogaDefaultDurationMinutes = 15;
    private const int UnknownActivityDtoValue = int.MaxValue;
    private static readonly int[] AvailableWeekdays =
    [
        (int)DayOfWeek.Monday,
        (int)DayOfWeek.Wednesday
    ];

    [Fact]
    public void SessionDurationOptionsFollowActivityPolicy()
    {
        Assert.Equal([25, 30], ActivitySessionDurationRule.ChoicesFor(WorkoutActivityType.Swimming));
        Assert.Equal([10, 15, 20], ActivitySessionDurationRule.ChoicesFor(WorkoutActivityType.Yoga));
        Assert.Equal([5, 10, 15], ActivitySessionDurationRule.ChoicesFor(WorkoutActivityType.Mobility));
        Assert.Equal([5, 10, 15, 20],
            ActivitySessionDurationRule.ChoicesFor(WorkoutActivityType.GentleRecovery));
        Assert.Empty(ActivitySessionDurationRule.ChoicesFor(WorkoutActivityType.Other));
        Assert.False(ActivitySessionDurationRule.RequiresExplicitChoice(WorkoutActivityType.Other));
        Assert.Null(ActivitySessionDurationRule.DefaultMinutesFor(WorkoutActivityType.Other));
        Assert.False(ActivitySessionDurationRule.IsValidSelection(WorkoutActivityType.Other, RequestedSessionsPerWeek));
        Assert.False(ActivityFrequencyPreferenceRule.RequiresExplicitWeeklyFrequency(WorkoutActivityType.Other));
    }

    [Fact]
    public void OtherActivityIsRejectedFromCurrentPreferenceSaves()
    {
        var activities = new[] { (int)WorkoutActivityType.Other };
        var durations = new Dictionary<int, int>
        {
            [(int)WorkoutActivityType.Other] = RequestedSessionsPerWeek
        };

        Assert.False(WorkoutPreferencesRule.IsValid(activities, AvailableWeekdays,
            new Dictionary<int, int>()));
        Assert.False(WorkoutPreferencesRule.IsValid(activities, AvailableWeekdays, durations,
            new Dictionary<int, int>
            {
                [(int)WorkoutActivityType.Other] = RequestedSessionsPerWeek
            }));
        Assert.False(WorkoutPreferencesRule.IsValid(activities, AvailableWeekdays, durations,
            new Dictionary<int, int>
            {
                [(int)WorkoutActivityType.Other] = InvalidSessionsPerWeek
            }));
        Assert.False(WorkoutPreferencesRule.IsValid(activities, AvailableWeekdays, durations,
            new Dictionary<int, int>
            {
                [(int)WorkoutActivityType.Yoga] = RequestedSessionsPerWeek
            }));
    }

    [Fact]
    public void FixedFrequencyActivitiesDoNotRequirePreferenceMap()
    {
        var activities = new[] { (int)WorkoutActivityType.HighVolumeStrength };
        var durations = new Dictionary<int, int>
        {
            [(int)WorkoutActivityType.HighVolumeStrength] =
                WorkoutPreferencesRule.DefaultActivitySessionDurationMinutes
        };

        Assert.True(WorkoutPreferencesRule.IsValid(activities, AvailableWeekdays, durations));
    }

    [Fact]
    public void WorkoutPreferenceContractMapsActivityDtosThroughCoreRules()
    {
        var defaultActivities = WorkoutPreferencesConstraints.BuildDefaultActivitySelection();
        var requiredActivities = WorkoutPreferencesConstraints.EnsureRequiredActivities(
            [WorkoutActivityDto.Yoga, (WorkoutActivityDto)UnknownActivityDtoValue]);

        Assert.Contains(WorkoutActivityDto.HighVolumeStrength, defaultActivities);
        Assert.Contains(WorkoutActivityDto.Yoga, requiredActivities);
        Assert.DoesNotContain((WorkoutActivityDto)UnknownActivityDtoValue, requiredActivities);
        Assert.True(WorkoutPreferencesConstraints.ActivityRequiresSessionDuration(WorkoutActivityDto.Yoga));
        Assert.Equal(YogaDefaultDurationMinutes,
            WorkoutPreferencesConstraints.DefaultSessionDurationMinutes(WorkoutActivityDto.Yoga));
        Assert.Contains(YogaDefaultDurationMinutes,
            WorkoutPreferencesConstraints.SessionDurationChoices(WorkoutActivityDto.Yoga));
        Assert.False(WorkoutPreferencesConstraints.ActivityRequiresSessionDuration(WorkoutActivityDto.Other));
        Assert.False(WorkoutPreferencesConstraints.ActivityRequiresSessionDuration(
            (WorkoutActivityDto)UnknownActivityDtoValue));
    }
}
