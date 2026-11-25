namespace MuscleCuties.UI.Navigation;

/// <summary>
/// The chooser and the manual editor are two full-screen sheets. A handoff exists only while the
/// chooser is still visible and the editor has been requested; the chooser may be dismissed only
/// after the editor has actually completed its open transition.
/// </summary>
internal static class NutritionMealEntryHandoff
{
    /// <summary>True when the meal entry editor should open straight away.</summary>
    public static bool IsHandoff(bool editorRequested, bool chooserVisible) =>
        editorRequested && chooserVisible;

    /// <summary>True when the chooser may close because the editor is up.</summary>
    public static bool CanCloseChooser(
        bool editorRequested,
        bool chooserVisible,
        bool editorPresented) =>
        IsHandoff(editorRequested, chooserVisible) && editorPresented;
}
