using MuscleCuties.UI.Models.UI.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class WorkoutSetEntryTests
{
    [Fact]
    public void First_working_row_has_working_set_one_identifier_even_after_a_warmup()
    {
        var set = new ExerciseSetInputItem(
            Guid.NewGuid(), Guid.Empty, "1", "Synthetic exercise", 2, 8, 10, null, false, 90)
        {
            DisplaySetNumber = 1
        };

        Assert.Equal("WorkingSet1Weight", set.WeightAutomationId);
        Assert.Equal("WorkingSet1LeftReps", set.LeftRepsAutomationId);
        Assert.Equal(2, set.SetNumber);
    }

    [Theory]
    [InlineData("en-US", "12,5", 12.5)]
    [InlineData("ru-RU", "12.5", 12.5)]
    [InlineData("en-US", "-1", 8)]
    [InlineData("ru-RU", "NaN", 8)]
    public void Weight_entry_uses_either_decimal_separator_and_keeps_last_valid_value_while_editing(
        string culture, string text, double expected)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var set = new ExerciseSetInputItem(
                Guid.NewGuid(), Guid.Empty, "1", "Synthetic exercise", 1, 8, 10, null, false, 90);

            set.WeightInput = text;

            Assert.Equal(expected, set.WeightKg);
            Assert.Equal(text, set.WeightInput);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
