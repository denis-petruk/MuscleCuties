using MuscleCuties.Core.Domain.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class WorkoutSessionActionRuleTests
{
    private const int FixtureYear = 2026;
    private const int FixtureMonth = 10;
    private const int FixtureDay = 24;
    private static readonly DateOnly SupportedSessionDate = new(FixtureYear, FixtureMonth, FixtureDay);

    [Fact]
    public void Session_Log_Requires_Valid_Ids_Date_And_NonNegative_Exercise_Values()
    {
        var validExercise = new WorkoutSessionExerciseLog(
            WorkoutSessionActionRule.MinimumPositiveIdentifier,
            WorkoutSessionActionRule.MinimumPositiveIdentifier,
            WorkoutSessionActionRule.MinimumNonNegativeCount,
            WorkoutSessionActionRule.MinimumNonNegativeCount,
            WorkoutSessionActionRule.MinimumWeightKg);

        Assert.True(WorkoutSessionActionRule.IsValidSessionLog(
            WorkoutSessionActionRule.MinimumPositiveIdentifier, SupportedSessionDate, [validExercise]));
        Assert.False(WorkoutSessionActionRule.IsValidSessionLog(
            WorkoutSessionActionRule.MinimumNonNegativeCount, SupportedSessionDate, [validExercise]));
        Assert.False(WorkoutSessionActionRule.IsValidSessionLog(
            WorkoutSessionActionRule.MinimumPositiveIdentifier, DateOnly.MinValue, [validExercise]));
        Assert.False(WorkoutSessionActionRule.IsValidSessionLog(
            WorkoutSessionActionRule.MinimumPositiveIdentifier,
            SupportedSessionDate,
            [validExercise with { WeightKg = float.NaN }]));
    }

    [Fact]
    public void Session_Actions_Apply_Identifier_Note_And_Optional_Set_Constraints()
    {
        Assert.True(WorkoutSessionActionRule.IsValidSwap(
            WorkoutSessionActionRule.MinimumPositiveIdentifier,
            WorkoutSessionActionRule.MinimumPositiveIdentifier));
        Assert.False(WorkoutSessionActionRule.IsValidMovementNote(
            WorkoutSessionActionRule.MinimumNonNegativeCount, null));
        Assert.False(WorkoutSessionActionRule.IsValidMovementNote(
            WorkoutSessionActionRule.MinimumPositiveIdentifier,
            new string('x', WorkoutSessionActionRule.MaximumMovementNoteCharacters +
                            WorkoutSessionActionRule.MinimumPositiveIdentifier)));
        Assert.True(WorkoutSessionActionRule.IsValidOptionalExercise(
            WorkoutSessionActionRule.MinimumPositiveIdentifier,
            WorkoutSessionActionRule.MinimumNonNegativeCount));
        Assert.False(WorkoutSessionActionRule.IsValidOptionalExercise(
            WorkoutSessionActionRule.MinimumPositiveIdentifier,
            -WorkoutSessionActionRule.MinimumPositiveIdentifier));
    }
}
