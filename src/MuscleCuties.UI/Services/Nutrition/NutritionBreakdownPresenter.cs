
using Microsoft.Maui.Graphics;

namespace MuscleCuties.UI.Services.Nutrition;

/// <summary>
/// Presentation state for the nutrition breakdown sheet, shared by the main Nutrition page and the
/// Daily Food Log. It never loads a date and never computes nutrients: the owning surface captures
/// its own date, calls the overview use case, and hands in values already mapped by
/// <see cref="NutritionPresentationMapper"/>. That is what keeps today's main-page state off a
/// historical Food Log date.
/// </summary>
public partial class NutritionBreakdownPresenter : ObservableObject
{
    private readonly Action? _onClosed;
    private readonly Func<MealItem, Task>? _editMealAsync;

    /// <summary>Takes the callback for the panel closing and the optional edit action, which decides whether the</summary>
    /// edit affordance appears at all.</summary>
    public NutritionBreakdownPresenter(Action? onClosed = null, Func<MealItem, Task>? editMealAsync = null)
    {
        _onClosed = onClosed;
        _editMealAsync = editMealAsync;
        CloseCommand = new RelayCommand(Close);
        EditMealCommand = new AsyncRelayCommand(EditMealAsync, () => CanEditMeal);
    }

    [ObservableProperty] private bool _isVisible;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorText = string.Empty;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _caloriesText = string.Empty;
    [ObservableProperty] private string _macrosText = string.Empty;
    [ObservableProperty] private string _fiberText = string.Empty;
    [ObservableProperty] private string _nutrientSummaryText = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCoverageText))]
    private string _coverageText = string.Empty;
    [ObservableProperty] private float _proteinCalories;
    [ObservableProperty] private float _carbsCalories;
    [ObservableProperty] private float _fatsCalories;
    [ObservableProperty] private ObservableCollection<MacroBreakdownItem> _macroItems = [];
    [ObservableProperty] private ObservableCollection<DailyMicronutrientItem> _micronutrients = [];
    [ObservableProperty] private MealItem? _selectedMeal;

    /// <summary>True when the breakdown has an error to show.</summary>
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);
    /// <summary>True when the breakdown has coverage text.</summary>
    public bool HasCoverageText => !string.IsNullOrWhiteSpace(CoverageText);
    /// <summary>True when the shown meal can be edited.</summary>
    public bool CanEditMeal => SelectedMeal is not null && _editMealAsync is not null;

    /// <summary>The command that closes the breakdown.</summary>
    public RelayCommand CloseCommand { get; }
    /// <summary>The command that opens the shown meal for editing.</summary>
    public AsyncRelayCommand EditMealCommand { get; }

    /// <summary>Opens the panel in its loading state, clearing any previous meal, error and coverage text.</summary>
    public void ShowLoading(string title)
    {
        Title = title;
        IsVisible = true;
        IsLoading = true;
        ErrorText = string.Empty;
        SelectedMeal = null;
        EditMealCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanEditMeal));
    }

    /// <summary>Opens the panel showing a failure instead of a meal, with no edit affordance.</summary>
    public void ShowError(string title, string message)
    {
        Title = title;
        IsVisible = true;
        IsLoading = false;
        SelectedMeal = null;
        CoverageText = string.Empty;
        MacroItems = [];
        Micronutrients = [];
        ErrorText = message;
        EditMealCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanEditMeal));
    }

    /// <summary>Presents the breakdown for the given totals.</summary>
    public void Show(
        string title,
        NutritionMacros macros,
        IEnumerable<DailyMicronutrientItem> micronutrients,
        MealItem? meal,
        string coverageText = "")
    {
        var nutrients = micronutrients.ToList();
        Title = title;
        CaloriesText = macros.CaloriesText;
        MacrosText = macros.MacroText;
        FiberText = BuildFiberText(nutrients);
        NutrientSummaryText = BuildNutrientSummary(nutrients);
        CoverageText = coverageText;
        ProteinCalories = macros.ProteinCalories;
        CarbsCalories = macros.CarbsCalories;
        FatsCalories = macros.FatsCalories;
        MacroItems = new ObservableCollection<MacroBreakdownItem>(BuildMacroItems(macros));
        Micronutrients = new ObservableCollection<DailyMicronutrientItem>(nutrients);
        SelectedMeal = meal;
        ErrorText = string.Empty;
        IsLoading = false;
        IsVisible = true;
        EditMealCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanEditMeal));
    }

    /// <summary>Empties the panel and hides it, leaving nothing of the previous meal behind.</summary>
    public void Clear()
    {
        IsVisible = false;
        IsLoading = false;
        ErrorText = string.Empty;
        SelectedMeal = null;
        Title = string.Empty;
        CoverageText = string.Empty;
        MacroItems = [];
        Micronutrients = [];
        EditMealCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanEditMeal));
    }

    /// <summary>Hides the panel and reports the close, without changing what it holds.</summary>
    private void Close()
    {
        IsVisible = false;
        _onClosed?.Invoke();
    }

    /// <summary>Hides the panel and hands the selected meal to the edit action, doing nothing when there is no meal</summary>
    /// or no edit action.</summary>
    private async Task EditMealAsync()
    {
        if (SelectedMeal is not { } meal || _editMealAsync is null)
            return;

        IsVisible = false;
        _onClosed?.Invoke();
        await _editMealAsync(meal);
    }

    /// <summary>Locates the fibre row without inventing a value when it is absent.</summary>
    public static string BuildFiberText(IEnumerable<DailyMicronutrientItem> micronutrients)
    {
        var fiber = micronutrients.FirstOrDefault(item => item.Name == "Fiber");
        return fiber is null ? UiTextCatalog.Format("Nutrition_Breakdown_FiberFormat", 0d) : UiTextCatalog.Format("Nutrition_Breakdown_FiberFormat", fiber.Amount);
    }

    /// <summary>The one-line micronutrient summary: how many targets were reached out of how many, or a message when</summary>
    /// the day has no micronutrient data.</summary>
    public static string BuildNutrientSummary(IEnumerable<DailyMicronutrientItem> micronutrients)
    {
        var items = micronutrients.ToList();
        if (items.Count == 0)
            return UiTextCatalog.Get("Ui_Nutrition_NoMicronutrients");

        var complete = items.Count(item => item.IsGoalHit);
        return UiTextCatalog.Format("Nutrition_Breakdown_TargetsReachedFormat", complete, items.Count);
    }

    /// <summary>The macro rows with their shares of the logged calories. Shares are of the logged total, not the</summary>
    /// target, so they always add up to what was eaten.</summary>
    public static IEnumerable<MacroBreakdownItem> BuildMacroItems(NutritionMacros macros)
    {
        var proteinCalories = macros.ProteinCalories;
        var carbsCalories = macros.CarbsCalories;
        var fatsCalories = macros.FatsCalories;
        var macroCalories = Math.Max(0f, proteinCalories + carbsCalories + fatsCalories);

        return
        [
            new MacroBreakdownItem
            {
                Name = "Protein",
                Grams = macros.Protein,
                Calories = proteinCalories,
                Progress = CalculateMacroShare(proteinCalories, macroCalories),
                Color = AppThemeResources.GetColor("ProteinLight", default)
            },
            new MacroBreakdownItem
            {
                Name = "Carbs",
                Grams = macros.Carbs,
                Calories = carbsCalories,
                Progress = CalculateMacroShare(carbsCalories, macroCalories),
                Color = AppThemeResources.GetColor("CarbsLight", default)
            },
            new MacroBreakdownItem
            {
                Name = "Fats",
                Grams = macros.Fats,
                Calories = fatsCalories,
                Progress = CalculateMacroShare(fatsCalories, macroCalories),
                Color = AppThemeResources.GetColor("FatsLight", default)
            }
        ];
    }

    /// <summary>
    /// NM03: states what the micronutrient totals cover. Presentation parity alone never claims data
    /// coverage, and a response without the additive field simply shows no note.
    /// </summary>
    public static string DescribeCoverage(MicronutrientCoverage? coverage)
    {
        if (coverage is null)
            return string.Empty;

        var value = coverage.GetValueOrDefault();
        var status = value.Status switch
        {
            "Complete" => UiTextCatalog.Get("Nutrition_Breakdown_CoverageComplete"),
            "Partial" => UiTextCatalog.Format("Nutrition_Breakdown_CoveragePartial",
                value.CoveredItemCount, value.CoveredItemCount + value.UncoveredItemCount),
            "Unavailable" => UiTextCatalog.Get("Nutrition_Breakdown_CoverageUnavailable"),
            "KnownZero" => UiTextCatalog.Get("Nutrition_Breakdown_CoverageKnownZero"),
            _ => string.Empty
        };

        if (value.UnavailableItemNames.Count == 0)
            return status;

        var names = UiTextCatalog.Format("Nutrition_Breakdown_CoverageNames",
            string.Join(", ", value.UnavailableItemNames));
        return string.IsNullOrEmpty(status) ? names : $"{status} {names}";
    }

    /// <summary>The share of the calories a macro contributes.</summary>
    private static float CalculateMacroShare(float calories, float totalCalories) =>
        totalCalories <= 0f ? 0f : Math.Clamp(calories / totalCalories, 0f, 1f);
}
