using MuscleCuties.UI.Services.Localization;

namespace MuscleCuties.UI.Services.Navigation;

/// <summary>
/// The one place that asks before losing unsaved edits. Four profile screens carried byte-identical copies of
/// this alert, so any change to the wording or the options had to be made four times.
/// </summary>
public static class UnsavedChangesPrompt
{
    /// <summary>True when navigation may continue; false when the user chose to keep editing.</summary>
    public static async Task<bool> ConfirmDiscardAsync(bool hasUnsavedChanges)
    {
        if (!hasUnsavedChanges || Shell.Current is not { } shell)
            return true;

        return await shell.DisplayAlertAsync(
            UiTextCatalog.Get("Profile_UnsavedChanges_Title"),
            UiTextCatalog.Get("Profile_UnsavedChanges_Message"),
            UiTextCatalog.Get("Profile_UnsavedChanges_Discard"),
            UiTextCatalog.Get("Profile_UnsavedChanges_KeepEditing"));
    }
}
