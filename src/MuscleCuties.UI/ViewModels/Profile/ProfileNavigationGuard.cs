namespace MuscleCuties.UI.ViewModels.Profile;

/// <summary>
///     Single implementation of the "leave an edited profile page" decision: ask before discarding
///     unsaved edits, and only navigate back when the user agrees. The confirmation is injected so
///     the decision can be exercised without a Shell.
/// </summary>
public static class ProfileNavigationGuard
{
    public static async Task<bool> TryNavigateBackAsync(
        bool hasUnsavedChanges,
        Func<Task<bool>> confirmDiscardAsync,
        Func<Task> navigateBackAsync)
    {
        ArgumentNullException.ThrowIfNull(confirmDiscardAsync);
        ArgumentNullException.ThrowIfNull(navigateBackAsync);

        if (hasUnsavedChanges && !await confirmDiscardAsync().ConfigureAwait(false))
            return false;

        await navigateBackAsync().ConfigureAwait(false);
        return true;
    }
}
