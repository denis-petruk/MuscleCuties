using System.Text.RegularExpressions;
using MuscleCuties.Infrastructure.Services.Workout;

namespace MuscleCuties.Core.Tests;

public class WorkoutSetNumberingTests
{
    [Fact]
    public void Warmup_rows_do_not_consume_working_set_numbers()
    {
        var numbers = WorkoutSetNumbering.Number([true, false, false, false]);

        Assert.Equal([(1, 1), (2, 1), (3, 2), (4, 3)], numbers);
    }

    [Fact]
    public void Working_rows_without_a_warmup_start_at_one()
    {
        var numbers = WorkoutSetNumbering.Number([false, false, false]);

        Assert.Equal([(1, 1), (2, 2), (3, 3)], numbers);
    }

    [Fact]
    public void Multiple_warmup_rows_keep_their_own_sequence()
    {
        var numbers = WorkoutSetNumbering.Number([true, true, false]);

        Assert.Equal([(1, 1), (2, 2), (3, 1)], numbers);
    }

    [Fact]
    public void Session_models_use_the_per_kind_display_number()
    {
        var root = FindRepositoryRoot();
        var set = File.ReadAllText(Path.Combine(root, "src", "MuscleCuties.UI", "Models", "UI", "Workout",
            "ExerciseSetInputItem.cs"));
        Assert.Contains("DisplaySetNumber", set);
        Assert.Contains("DisplaySetNumber > 0 ? DisplaySetNumber : SetNumber", set);
        Assert.Matches(new Regex(@"AutomationPrefix\s*=>.*LabelSetNumber"), set);
        Assert.Matches(new Regex(@"SetDisplayText\s*=>[\s\S]{0,220}LabelSetNumber"), set);

        var exercise = File.ReadAllText(Path.Combine(root, "src", "MuscleCuties.UI", "Models", "UI", "Workout",
            "WorkoutExerciseItem.cs"));
        Assert.Contains("WorkoutSetNumbering.Number", exercise);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MuscleCuties.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
