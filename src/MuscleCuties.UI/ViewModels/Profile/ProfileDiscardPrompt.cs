using MuscleCuties.UI.Services.Localization;

namespace MuscleCuties.UI.ViewModels.Profile;

/// <summary>
///     The shell dialog every profile page shows before it drops unsaved edits. One copy of the
///     four localized strings, used by all profile view models through <see cref="ProfileNavigationGuard" />.
/// </summary>
internal static class ProfileDiscardPrompt
{
    /// <summary>Asks before unsaved edits are dropped. Without a shell there is nobody to ask, so it allows the</summary>
    /// navigation rather than trapping the user on the page.</summary>
    public static async Task<bool> ConfirmAsync()
    {
        if (Shell.Current is not { } shell)
            return true;

        return await shell.DisplayAlertAsync(
            UiTextCatalog.Get("Profile_UnsavedChanges_Title"),
            UiTextCatalog.Get("Profile_UnsavedChanges_Message"),
            UiTextCatalog.Get("Profile_UnsavedChanges_Discard"),
            UiTextCatalog.Get("Profile_UnsavedChanges_KeepEditing"));
    }
}
