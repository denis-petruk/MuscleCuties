using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace MuscleCuties.Core.Tests;

public class WorkoutSessionLifecycleAndModalTests
{
    private static string RepositoryRoot()
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

        throw new InvalidOperationException($"Could not locate repository root from '{AppContext.BaseDirectory}'.");
    }

    [Fact]
    public void WorkoutExerciseItem_tracks_saved_optional_state_and_guards_invocations()
    {
        var path = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI", "Models", "UI", "Workout", "WorkoutExerciseItem.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("_lastSavedIsOptional", source);
        Assert.Contains("_lastSavedOptionalSets", source);
        Assert.Contains("SyncSavedOptionalState()", source);
        Assert.Contains("RevertOptionalState()", source);
        Assert.Contains("ShouldInvokeOptionalApi()", source);

        // Verify guard conditions
        Assert.Contains("if (IsLogged || !CanMarkOptional)", source);
        Assert.Contains("if (IsOptional == _lastSavedIsOptional && OptionalSets == _lastSavedOptionalSets)", source);
    }

    [Fact]
    public void WorkoutSessionViewModel_SaveOptionalExercise_guards_and_does_not_call_ApplyFailure()
    {
        var path = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI", "ViewModels", "Workout", "WorkoutSessionViewModel.cs");
        var source = File.ReadAllText(path);

        var match = Regex.Match(source, @"SaveOptionalExerciseAsync\s*\([^)]*\)\s*\{(.*?)\n    \}", RegexOptions.Singleline);
        Assert.True(match.Success, "SaveOptionalExerciseAsync should be defined in WorkoutSessionViewModel");

        var body = match.Groups[1].Value;
        Assert.Contains("item.IsLogged || !item.CanMarkOptional", body);
        Assert.Contains("item.IsOptional == item.LastSavedIsOptional && item.OptionalSets == item.LastSavedOptionalSets", body);
        Assert.Contains("item.RevertOptionalState()", body);
        Assert.DoesNotContain("ApplyFailure", body);
    }

    [Fact]
    public void WorkoutSessionViewModel_resumes_to_first_unlogged_and_unskipped_exercise()
    {
        var path = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI", "ViewModels", "Workout", "WorkoutSessionViewModel.cs");
        var source = File.ReadAllText(path);

        // Resume in LoadSession
        Assert.Contains("isResuming", source);
        Assert.Contains("!p.item.IsLogged && !_skippedIds.Contains(p.item.WorkoutDayExerciseId)", source);

        // Advance to first unlogged exercise in StartActivityAsync
        var match = Regex.Match(source, @"StartActivityAsync\s*\([^)]*\)\s*\{(.*?)\n    \}", RegexOptions.Singleline);
        Assert.True(match.Success, "StartActivityAsync should be defined in WorkoutSessionViewModel");
        var body = match.Groups[1].Value;
        Assert.Contains("firstUnlogged", body);
        Assert.Contains("SetCurrentExercise(firstUnlogged.item is not null ? firstUnlogged.index : 0)", body);
    }

    [Fact]
    public void Unilateral_validation_requires_observations_without_requiring_unedited_placeholder_rows()
    {
        var path = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI", "ViewModels", "Workout", "WorkoutSessionViewModel.cs");
        var source = File.ReadAllText(path);
        Assert.Matches(
            @"exercise\.IsUnilateral && \(capturedSets\.Length == 0 \|\| capturedSets\.Any\(set => !set\.HasEnteredReps\)\)",
            source);
    }

    [Fact]
    public void Fully_logged_active_workout_reopens_at_the_summary()
    {
        var path = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI", "ViewModels", "Workout", "WorkoutSessionViewModel.cs");
        var source = File.ReadAllText(path);
        var loadSession = Regex.Match(source,
            @"public async Task LoadSession\s*\([^)]*\)\s*\{(.*?)
    \}", RegexOptions.Singleline);

        Assert.True(loadSession.Success);
        Assert.Matches(
            @"else if \(_sessionCreated && SelectedActivity\.Exercises\.Count > 0\)\s*\{\s*ShowSummary\(\);",
            loadSession.Groups[1].Value);
    }

    [Fact]
    public void WorkoutSessionViewModel_abandon_session_confirmation_lifecycle_is_reliable()
    {
        var path = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI", "ViewModels", "Workout", "WorkoutSessionViewModel.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("public void AbandonPlannedSession()", source);
        Assert.Contains("IsAbandonConfirmationVisible = true", source);
        Assert.Contains("public async Task ConfirmAbandonPlannedSessionAsync()", source);
        Assert.Contains("AbandonPlannedSessionAsync(sessionId)", source);
        Assert.Contains("SettleAbandonedSessionAsync()", source);
    }

    [Fact]
    public void WorkoutSessionPage_guards_optional_exercise_toggled_and_changed()
    {
        var path = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI", "Pages", "Workout", "WorkoutSessionPage.xaml.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("ShouldInvokeOptionalApi()", source);
    }

    [Fact]
    public void AnimatedModalView_hooks_unloaded_and_disappearing_with_idempotent_scope()
    {
        var path = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI", "Controls", "Shared", "AnimatedModalView.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("Unloaded += OnUnloaded", source);
        Assert.Contains("page.Disappearing += OnPageDisappearing", source);
        // Stack order is part of the contract: when the entry sheet hands off to the editor,
        // the editor must be isolated as the top sheet rather than staying inside the chooser's
        // excluded subtree.
        Assert.Contains("List<VisualElement> _activeModals", source);
        Assert.Contains("Reisolate()", source);
        Assert.Contains("ModalAccessibilityScope.Acquire", source);
        Assert.Contains("ModalAccessibilityScope.Release", source);
        Assert.Contains("_activeModals.Add(modal)", source);
        Assert.Contains("_activeModals.Remove(modal)", source);
    }

    [Fact]
    public void WorkoutSessionViewModel_hides_retry_load_button_for_form_validation_errors()
    {
        var path = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI", "ViewModels", "Workout", "WorkoutSessionViewModel.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("public bool CanRetryLoad =>", source, StringComparison.Ordinal);
        Assert.Contains("nameof(CanRetryLoad)", source, StringComparison.Ordinal);
        Assert.Contains("\"Ui_Workout_Error_Weight\"", source, StringComparison.Ordinal);

        var xamlPath = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI", "Pages", "Workout", "WorkoutSessionPage.xaml");
        var xaml = File.ReadAllText(xamlPath);
        Assert.Contains("Command=\"{Binding RetryLoadCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding CanRetryLoad}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkoutExerciseItem_hides_empty_previous_log_and_tracks_set_row_chrome()
    {
        var itemPath = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI", "Models", "UI", "Workout", "WorkoutExerciseItem.cs");
        var itemSource = File.ReadAllText(itemPath);

        Assert.Contains("public bool HasPreviousLog =>", itemSource, StringComparison.Ordinal);
        Assert.Contains("public bool HasRecommendationText =>", itemSource, StringComparison.Ordinal);
        Assert.Contains("string.IsNullOrWhiteSpace(CalibrationPromptText)", itemSource, StringComparison.Ordinal);
        Assert.Contains("Sets[index].ShowDivider = index > 0;", itemSource, StringComparison.Ordinal);
        Assert.Contains("Sets[index].CanRemove = Sets.Count > 1;", itemSource, StringComparison.Ordinal);

        var setPath = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI", "Models", "UI", "Workout", "ExerciseSetInputItem.cs");
        var setSource = File.ReadAllText(setPath);
        Assert.Contains("private bool _showDivider;", setSource, StringComparison.Ordinal);
        Assert.Contains("private bool _canRemove;", setSource, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkoutSessionPage_groups_sets_and_tools_without_card_per_row_clutter()
    {
        var xamlPath = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI", "Pages", "Workout", "WorkoutSessionPage.xaml");
        var xaml = File.ReadAllText(xamlPath);

        // Tab bar is hidden on WorkoutSessionPage; do not waste 96 pt of bottom padding meant for tab pages.
        Assert.DoesNotContain("PresentationPageContentPadding", xaml, StringComparison.Ordinal);
        Assert.Contains("PresentationFormPagePadding", xaml, StringComparison.Ordinal);

        // Only show "Previous: ..." when an actual previous log exists.
        Assert.Contains("IsVisible=\"{Binding CurrentExercise.HasPreviousLog}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding CurrentExercise.HasRecommendationText}\"", xaml, StringComparison.Ordinal);

        // Per-set rows live inside one grouped CardSecondary separated by dividers, not a separate card per set.
        var setTemplateMatch = Regex.Match(
            xaml,
            @"BindableLayout\.ItemsSource=""\{Binding Sets\}""[\s\S]*?<DataTemplate x:DataType=""models:ExerciseSetInputItem"">(?<body>[\s\S]*?)</DataTemplate>",
            RegexOptions.Singleline);
        Assert.True(setTemplateMatch.Success, "Expected Sets DataTemplate in WorkoutSessionPage.xaml");
        var templateBody = setTemplateMatch.Groups["body"].Value;
        Assert.DoesNotContain("CardSecondary", templateBody, StringComparison.Ordinal);
        Assert.Contains("ShowDivider", templateBody, StringComparison.Ordinal);
        Assert.Contains("CanRemove", templateBody, StringComparison.Ordinal);

        // Unilateral split inputs keep full accessible names while using compact L/R placeholders so they never elide to "...".
        Assert.Contains("Placeholder=\"{loc:LocalizedText Workout_Session_LeftSideShort}\"", templateBody, StringComparison.Ordinal);
        Assert.Contains("Placeholder=\"{loc:LocalizedText Workout_Session_RightSideShort}\"", templateBody, StringComparison.Ordinal);
        Assert.Contains("SemanticProperties.Description=\"{loc:LocalizedText Workout_Session_LeftSideReps}\"", templateBody, StringComparison.Ordinal);
        Assert.Contains("SemanticProperties.Description=\"{loc:LocalizedText Workout_Session_RightSideReps}\"", templateBody, StringComparison.Ordinal);
    }
}
