using MuscleCuties.Core.Domain.Workout;
using MuscleCuties.UI.ViewModels.Workout;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// The Workouts list is selected by stable workout tags. A filter chip's label is localized
/// display copy, so deciding visibility from it empties the list in every non-English language:
/// "Все" is neither "All" nor a tag such as "STRENGTH", and the fallback compares "ВСЕ" to
/// "STRENGTH". Every chip therefore needs a stable tag, and the filter must read that tag.
/// </summary>
public class WorkoutListFilterTests
{
    private static readonly string RepoRoot = FindRepositoryRoot();

    [Fact]
    public void Workout_list_filters_select_by_stable_tag_not_by_localized_label()
    {
        var viewModel = File.ReadAllText(Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "ViewModels",
            "Workout", "WorkoutViewModel.cs"));

        Assert.DoesNotContain("selected.Label == \"All\"", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("selected.Label == \"Recovery\"", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("f.Label == \"All\"", viewModel, StringComparison.Ordinal);

        var chip = File.ReadAllText(Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Models", "UI",
            "Workout", "FilterChipItem.cs"));
        Assert.Contains("Tag", chip, StringComparison.Ordinal);
    }

    [Fact]
    public void The_all_filter_shows_every_scheduled_workout_of_the_week()
    {
        // The week the QA clone returned for week 1: three strength sessions, cardio, one rest day.
        (string Tag, bool IsRestDay)[] week =
        [
            (WorkoutActivityTagRule.Strength, false),
            (WorkoutActivityTagRule.Strength, false),
            (WorkoutActivityTagRule.Strength, false),
            (WorkoutActivityTagRule.Cardio, false),
            (WorkoutActivityTagRule.Rest, true)
        ];

        var visible = week
            .Where(workout => WorkoutListFilterRule.IsVisible(
                workout.IsRestDay, workout.Tag, WorkoutListFilterRule.AllTag))
            .ToArray();

        Assert.Equal(4, visible.Length);
    }

    [Fact]
    public void Localized_filter_labels_never_select_workouts()
    {
        // The localized chips the Russian catalog ships: none of them may select workouts.
        string[] russianLabels = ["Все", "Сила", "Кардио", "Восстановление"];

        foreach (var label in russianLabels)
        {
            Assert.False(
                WorkoutListFilterRule.IsVisible(false, WorkoutActivityTagRule.Strength, label),
                $"the label '{label}' must not select workouts; a chip is identified by its stable tag");
        }
    }

    [Fact]
    public void Activity_filters_keep_only_their_own_tag()
    {
        Assert.True(WorkoutListFilterRule.IsVisible(
            false, WorkoutActivityTagRule.Strength, WorkoutListFilterRule.StrengthTag));
        Assert.True(WorkoutListFilterRule.IsVisible(
            false, WorkoutActivityTagRule.Cardio, WorkoutListFilterRule.CardioTag));
        Assert.False(WorkoutListFilterRule.IsVisible(
            false, WorkoutActivityTagRule.Cardio, WorkoutListFilterRule.StrengthTag));
        Assert.False(WorkoutListFilterRule.IsVisible(
            false, WorkoutActivityTagRule.Strength, WorkoutListFilterRule.CardioTag));
    }

    [Fact]
    public void The_recovery_filter_keeps_only_recovery_workouts()
    {
        Assert.True(WorkoutListFilterRule.IsVisible(
            false, WorkoutActivityTagRule.Recovery, WorkoutListFilterRule.RecoveryTag));
        Assert.False(WorkoutListFilterRule.IsVisible(
            false, WorkoutActivityTagRule.Strength, WorkoutListFilterRule.RecoveryTag));
        Assert.False(WorkoutListFilterRule.IsVisible(
            false, WorkoutActivityTagRule.Cardio, WorkoutListFilterRule.RecoveryTag));
    }

    [Fact]
    public void Rest_days_never_appear_as_workout_cards()
    {
        Assert.False(WorkoutListFilterRule.IsVisible(
            true, WorkoutActivityTagRule.Rest, WorkoutListFilterRule.AllTag));
        Assert.False(WorkoutListFilterRule.IsVisible(true, WorkoutActivityTagRule.Rest, string.Empty));
        Assert.False(WorkoutListFilterRule.IsVisible(true, WorkoutActivityTagRule.Rest, null));
    }

    [Fact]
    public void Filter_tags_match_the_canonical_activity_tags()
    {
        // The rule repeats the codes because a UI view model may not import the Core domain.
        Assert.Equal(WorkoutActivityTagRule.Strength, WorkoutListFilterRule.StrengthTag);
        Assert.Equal(WorkoutActivityTagRule.Cardio, WorkoutListFilterRule.CardioTag);
        Assert.Equal(WorkoutActivityTagRule.Recovery, WorkoutListFilterRule.RecoveryTag);
        Assert.NotEqual(WorkoutListFilterRule.AllTag, WorkoutListFilterRule.StrengthTag);
    }

    private static string FindRepositoryRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "MuscleCuties.sln")) ||
                Directory.Exists(Path.Combine(current, ".git")))
            {
                return current;
            }
            current = Directory.GetParent(current)?.FullName;
        }

        throw new InvalidOperationException($"Could not locate repository root starting from '{AppContext.BaseDirectory}'.");
    }
}
