using MuscleCuties.UI.Controls.Shared;

namespace MuscleCuties.UI.Controls.Nutrition;

public partial class MealEditorModal : AnimatedModalView
{
    public MealEditorModal()
    {
        InitializeComponent();
        SearchEntry.Focused += OnSearchEntryFocused;
        SearchEntry.Unfocused += OnSearchEntryUnfocused;
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        BottomSheetSizing.Apply(SheetSurface, height);
    }

    private void OnSearchSubmitted(object? sender, EventArgs e)
    {
        SearchEntry.Unfocus();
    }

    /// <summary>
    /// While the search field has the keyboard, the sheet has roughly 250 pt to work with. The header,
    /// the source tabs and the ingredient list together spend more than that, which left the results
    /// row behind the keys (D51). Collapsing them for the duration of the search keeps the field, the
    /// `Поиск` action and the first result on screen; unfocusing restores the editor exactly as it was.
    /// </summary>
    private void OnSearchEntryFocused(object? sender, FocusEventArgs e) => SetSearchMode(true);

    private void OnSearchEntryUnfocused(object? sender, FocusEventArgs e) => SetSearchMode(false);

    private void SetSearchMode(bool active)
    {
        EditorHeaderRow.IsVisible = !active;
        SourceTabsRow.IsVisible = !active;
        IngredientListRow.IsVisible = !active;
    }
}
