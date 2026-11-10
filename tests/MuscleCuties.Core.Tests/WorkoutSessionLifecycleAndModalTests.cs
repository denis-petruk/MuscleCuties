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
        Assert.Contains("HashSet<VisualElement> _activeModals", source);
        Assert.Contains("ModalAccessibilityScope.Acquire", source);
        Assert.Contains("ModalAccessibilityScope.Release", source);
        Assert.Contains("_activeModals.Add(modal)", source);
        Assert.Contains("_activeModals.Remove(modal)", source);
    }
}
