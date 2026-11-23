using MuscleCuties.UI.Services.Localization;

namespace MuscleCuties.UI.ViewModels.Profile;

/// <summary>
///     The shell dialog every profile page shows before it drops unsaved edits. One copy of the
///     four localized strings, used by all profile view models through <see cref="ProfileNavigationGuard" />.
/// </summary>
internal static class ProfileDiscardPrompt
{
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
