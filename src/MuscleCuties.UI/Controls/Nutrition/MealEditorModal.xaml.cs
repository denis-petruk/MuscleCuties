using System.ComponentModel;
using MuscleCuties.UI.ViewModels.Nutrition;

namespace MuscleCuties.UI.Controls.Nutrition;

/// <summary>The meal editor sheet: its search field switches the sheet between editing the meal and finding food to add to</summary>
/// it.</summary>
public partial class MealEditorModal : AnimatedModalView
{
    /// <summary>Initializes the modal declared in its XAML.</summary>
    public MealEditorModal()
    {
        InitializeComponent();
        SearchEntry.Focused += OnSearchEntryFocused;
        SearchEntry.Unfocused += OnSearchEntryUnfocused;
        IngredientSearchGrid.PropertyChanged += OnIngredientSearchGridPropertyChanged;
    }

    /// <summary>Sizes the sheet to fill its host.</summary>
    protected override SheetSizing SheetSizingPolicy => SheetSizing.Fill;

    /// <summary>Submitting the search field switches the sheet into search mode, so the keyboard's action does what the user</summary>
    /// expects instead of closing.</summary>
    private void OnSearchSubmitted(object? sender, EventArgs e)
    {
        // The keyboard stays up on purpose: unfocusing here was how the picker hid its own results
        // the moment a search ran, so a hit was only reachable after dismissing the keys (D51).
        // The button steals focus on iOS, so the field takes it back once the results render.
        SetSearchMode(true);
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(250), () =>
        {
            if (IsOpen && IngredientSearchGrid.IsVisible)
                SearchEntry.Focus();
        });
    }

    /// <summary>
    /// While the search field has the keyboard, the sheet has roughly 250 pt to work with. The header,
    /// the source tabs and the ingredient list together spend more than that, which left the results
    /// row behind the keys (D51). Collapsing them for the duration of the search keeps the field, the
    /// `Поиск` action and the first result on screen; unfocusing restores the editor exactly as it was.
    /// </summary>
    private void OnSearchEntryFocused(object? sender, FocusEventArgs e) => SetSearchMode(true);

    /// <summary>Leaves search mode when the search entry loses focus.</summary>
    private void OnSearchEntryUnfocused(object? sender, FocusEventArgs e) => SetSearchMode(false);

    /// <summary>Refreshes the ingredient search grid when the view model reports a change.</summary>
    private void OnIngredientSearchGridPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IsVisible) && !IngredientSearchGrid.IsVisible)
        {
            SearchEntry.IsEnabled = false;
            SearchEntry.IsEnabled = true;
            SetSearchMode(false);
        }
    }

    /// <summary>Swaps the sheet between its meal and its search presentation, and keeps focus in the field the user is now in.</summary>
    private void SetSearchMode(bool active)
    {
        EditorHeaderRow.IsVisible = !active;
        SourceTabsRow.IsVisible = !active;
        IngredientListRow.IsVisible = !active;
        if (!active)
        {
            SourceTabsRow.SetBinding(IsVisibleProperty, nameof(NutritionViewModel.IsFoodSourceTabsVisible));
            IngredientListRow.SetBinding(IsVisibleProperty, nameof(NutritionViewModel.IsFoodFinderCollapsed));
        }
    }
}
