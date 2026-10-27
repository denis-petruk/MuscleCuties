using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class SevenDayWorkoutPreferenceTests
{
    private const int SevenDaysPerWeek = 7;

    [Fact]
    public void SevenAvailableWeekdaysAreValidWithoutImplyingSevenStrengthSessions()
    {
        Assert.True(WorkoutPreferencesRule.IsValid(
            [(int)WorkoutActivityType.HighVolumeStrength],
            Enum.GetValues<DayOfWeek>().Select(day => (int)day).ToArray(),
            new Dictionary<int, int>
            {
                [(int)WorkoutActivityType.HighVolumeStrength] =
                    WorkoutPreferencesRule.DefaultActivitySessionDurationMinutes
            }));
        Assert.Equal(SevenDaysPerWeek, Enum.GetValues<DayOfWeek>().Length);

        var result = ActivityPrescriptionRule.Evaluate(new ActivityPrescriptionContext(
            WorkoutActivityType.HighVolumeStrength,
            ActivityExperience.Established,
            ExistingBaselineMinutes: null,
            HasCurrentRestrictionOrSymptom: false,
            IsExplicitlySelected: true,
            IsEstablishedResistanceHistory: true,
            CompletedMainExerciseExposures: 0,
            HasKnownManageableLoad: false,
            UserConfirmedCurrentFamiliarity: false));

        Assert.Equal(ActivityPrescriptionStatus.Eligible, result.Status);
        Assert.Single(result.Sessions);
        Assert.Equal(ActivityPrescriptionRule.HighVolumeStrengthSessionsPerWeek,
            result.Sessions[0].FrequencyPerWeek);
    }

    [Fact]
    public void RestDayAloneIsNotAValidWorkoutPreference()
    {
        var availableWeekdays = new[] { (int)DayOfWeek.Monday, (int)DayOfWeek.Wednesday };
        var noDurations = new Dictionary<int, int>();
        var restDay = new[] { (int)WorkoutActivityType.RestDay };

        Assert.False(WorkoutPreferencesRule.IsValid(restDay, availableWeekdays, noDurations));
        Assert.False(WorkoutPreferencesRule.IsValid(restDay, availableWeekdays,
            new Dictionary<int, int>
            {
                [(int)WorkoutActivityType.RestDay] =
                    WorkoutPreferencesRule.DefaultActivitySessionDurationMinutes
            }));
    }

    [Fact]
    public void OtherActivityIsNoLongerAcceptedAsCurrentWorkoutPreference()
    {
        Assert.False(WorkoutPreferencesRule.RequiresSessionDuration(WorkoutActivityType.Other));
        Assert.False(WorkoutPreferencesRule.RequiresExplicitSessionDuration(WorkoutActivityType.Other));
        Assert.False(WorkoutPreferencesRule.RequiresExplicitSessionDuration(WorkoutActivityType.HighVolumeStrength));
        Assert.False(WorkoutPreferencesRule.IsValid(
            [(int)WorkoutActivityType.Other],
            [(int)DayOfWeek.Monday, (int)DayOfWeek.Wednesday],
            new Dictionary<int, int>()));
    }
}
