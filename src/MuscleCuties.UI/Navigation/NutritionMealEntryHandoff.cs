namespace MuscleCuties.UI.Navigation;

/// <summary>
/// The chooser and the manual editor are two full-screen sheets. A handoff exists only while the
/// chooser is still visible and the editor has been requested; the chooser may be dismissed only
/// after the editor has actually completed its open transition.
/// </summary>
internal static class NutritionMealEntryHandoff
{
    public static bool IsHandoff(bool editorRequested, bool chooserVisible) =>
        editorRequested && chooserVisible;

    public static bool CanCloseChooser(
        bool editorRequested,
        bool chooserVisible,
        bool editorPresented) =>
        IsHandoff(editorRequested, chooserVisible) && editorPresented;
}
