using System.Text.RegularExpressions;
using MuscleCuties.Infrastructure.Services.Workout;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// Gate 77 regression coverage for the DB Shoulder Press weight-entry blocker: a calibration exercise
/// whose unobserved rows are cleared still has to accept a load the user typed, and validation must
/// read the same value the payload sends.
/// </summary>
public class WorkoutWeightEntryRuleTests
{
    private static readonly string RepoRoot = FindRepositoryRoot();

    [Fact]
    public void Warmup_only_calibration_load_is_accepted()
    {
        var sets = new[]
        {
            new WorkoutEnteredSet("20", IsWarmup: true, WasEdited: true),
            new WorkoutEnteredSet(null, IsWarmup: false, WasEdited: false),
            new WorkoutEnteredSet(null, IsWarmup: false, WasEdited: false)
        };

        var entered = WorkoutWeightEntryRule.ResolveEnteredWeightKg(sets, fallbackText: null);

        Assert.Equal(20d, entered);
        Assert.True(WorkoutWeightEntryRule.IsAcceptable(entered));
    }

    [Fact]
    public void Working_set_load_wins_over_a_warmup_load()
    {
        var sets = new[]
        {
            new WorkoutEnteredSet("10", IsWarmup: true, WasEdited: true),
            new WorkoutEnteredSet("42.5", IsWarmup: false, WasEdited: true)
        };

        Assert.Equal(42.5d, WorkoutWeightEntryRule.ResolveEnteredWeightKg(sets, "0"));
    }

    [Fact]
    public void Aggregate_text_is_the_fallback_when_no_row_is_typed()
    {
        var sets = new[] { new WorkoutEnteredSet(string.Empty, IsWarmup: false, WasEdited: false) };

        Assert.Equal(0d, WorkoutWeightEntryRule.ResolveEnteredWeightKg(sets, "0.0"));
    }

    [Fact]
    public void Group_separator_round_trip_is_parsed()
    {
        Assert.Equal(1600d, WorkoutWeightEntryRule.ParseWeight("1,600.0"));
        Assert.Equal(1600d, WorkoutWeightEntryRule.ParseWeight("1 600,0"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("heavy")]
    [InlineData("-5")]
    public void Empty_or_invalid_load_is_rejected(string? text)
    {
        Assert.Null(WorkoutWeightEntryRule.ParseWeight(text));
        Assert.False(WorkoutWeightEntryRule.IsAcceptable(WorkoutWeightEntryRule.ParseWeight(text)));
    }

    [Fact]
    public void Session_screen_validation_and_payload_use_the_shared_rule()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "ViewModels", "Workout",
            "WorkoutSessionViewModel.cs"));

        Assert.Contains("WorkoutWeightEntryRule.ResolveEnteredWeightKg", source);

        var validate = Regex.Match(source,
            @"private bool ValidateInputs\(WorkoutExerciseItem exercise\)\s*\{(?<body>.*?)\n    \}",
            RegexOptions.Singleline);
        Assert.True(validate.Success, "ValidateInputs should be defined in WorkoutSessionViewModel");
        Assert.Contains("WorkoutWeightEntryRule", validate.Groups["body"].Value);

        var item = File.ReadAllText(Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Models", "UI", "Workout",
            "WorkoutExerciseItem.cs"));
        Assert.Contains("WorkoutWeightEntryRule.ResolveEnteredWeightKg", item);
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
