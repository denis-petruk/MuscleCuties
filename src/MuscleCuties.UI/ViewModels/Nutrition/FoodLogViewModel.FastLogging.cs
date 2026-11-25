// Fast logging, barcode capture, and quick-add flows.
// Second half of FoodLogViewModel; see its declaration for the full contract.

using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Nutrition;
using MuscleCuties.UI.Services.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Barcode;
using MuscleCuties.UseCases.Features.Nutrition.Barcode.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.DailyLog;
using MuscleCuties.UseCases.Features.Nutrition.Recipes.Contracts;

namespace MuscleCuties.UI.ViewModels.Nutrition;

/// <summary>Fast logging: the recent, suggested and saved shelves, multi-select of several foods at once, and the food</summary>
/// search behind them.</summary>
public partial class FoodLogViewModel
{
    private const double DefaultQuickAddCalories = 200.0;
    private const double DefaultQuickAddProtein = 0.0;
    private const double DefaultQuickAddCarbs = 0.0;
    private const double DefaultQuickAddFat = 0.0;
    private const double DefaultQuickAddServingSize = 1.0;
    private const double DefaultQuickAddServingsCount = 1.0;
    private const string DefaultQuickAddServingUnit = "serving";
    /// <summary>Name given to a quick add that has no name of its own.</summary>
    private static string DefaultQuickAddTitle => UiTextCatalog.Get("Nutrition_QuickAdd_DefaultName");
    private const string FoodStatusEnterTwoLettersKey = "Nutrition_FoodLog_StatusEnterTwoLetters";
    private const string FoodStatusSavedFoodsKey = "Nutrition_FoodLog_StatusSavedFoods";
    private const string FoodStatusNoSavedMatchesKey = "Nutrition_FoodLog_StatusNoSavedMatches";
    private const string FoodStatusSavedSearchUnavailableKey = "Nutrition_FoodLog_StatusSavedSearchUnavailable";
    private const string FoodStatusUsdaGenericOnlyKey = "Nutrition_FoodLog_StatusUsdaGenericOnly";
    private const string FoodStatusUsdaSearchingKey = "Nutrition_FoodLog_StatusUsdaSearching";
    private const string FoodStatusUsdaResultsKey = "Nutrition_FoodLog_StatusUsdaResults";
    private const string FoodStatusUsdaNoResultsKey = "Nutrition_FoodLog_StatusUsdaNoResults";
    private const string FoodStatusUsdaUnavailableKey = "Nutrition_FoodLog_StatusUsdaUnavailable";

    private CancellationTokenSource? _foodSearchCancellation;
    private BarcodeScanCoordinator? _barcodeScan;

    [ObservableProperty] private string _foodSearchQuery = string.Empty;

    [ObservableProperty] private string _foodSearchStatus = string.Empty;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsManualEditorVisible))]
    private bool _isFastLoggingVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsManualEditorVisible))]
    private bool _isQuickAddCaloriesVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsManualEditorVisible))]
    private bool _isBarcodeScannerVisible;

    [ObservableProperty] private string _barcodeInputText = string.Empty;
    [ObservableProperty] private string _barcodeScanStatus = string.Empty;
    [ObservableProperty] private bool _isProcessingBarcode;

    [ObservableProperty] private double _quickCaloriesValue = DefaultQuickAddCalories;
    [ObservableProperty] private double _quickProteinValue = DefaultQuickAddProtein;
    [ObservableProperty] private double _quickCarbsValue = DefaultQuickAddCarbs;
    [ObservableProperty] private double _quickFatValue = DefaultQuickAddFat;
    [ObservableProperty] private string _quickCaloriesName = string.Empty;

    [ObservableProperty] private bool _isLoadingFastLogging;

    [ObservableProperty] private bool _isSearchingFood;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedQuickFoods), nameof(SelectedQuickFoodsSummaryText))]
    private int _selectedQuickFoodsCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedQuickFoodsSummaryText))]
    private double _selectedQuickFoodsCalories;

    /// <summary>The recently logged ingredients.</summary>
    public ObservableCollection<QuickFoodItemUiModel> RecentIngredients { get; } = [];
    /// <summary>The recently accepted suggestions.</summary>
    public ObservableCollection<QuickFoodItemUiModel> RecentSuggestions { get; } = [];
    /// <summary>The food search results.</summary>
    public ObservableCollection<QuickFoodItemUiModel> FoodSearchResults { get; } = [];
    /// <summary>The user's saved recipes.</summary>
    public ObservableCollection<RecipeSummaryItem> SavedRecipes { get; } = [];

    /// <summary>True when the manual food editor is shown.</summary>
    public bool IsManualEditorVisible => !IsFastLoggingVisible && !IsQuickAddCaloriesVisible &&
                                         !IsBarcodeScannerVisible && !IsRecipeBuilderVisible;
    /// <summary>True when there are recent ingredients.</summary>
    public bool HasRecentIngredients => RecentIngredients.Count > 0;
    /// <summary>True when there are recent suggestions.</summary>
    public bool HasRecentSuggestions => RecentSuggestions.Count > 0;
    /// <summary>True when the search returned foods.</summary>
    public bool HasFoodSearchResults => FoodSearchResults.Count > 0;
    /// <summary>True when the user has recipes.</summary>
    public bool HasSavedRecipes => SavedRecipes.Count > 0;
    /// <summary>Whether any shelf item is ticked for a multi-add.</summary>
    public bool HasSelectedQuickFoods => SelectedQuickFoodsCount > 0;

    /// <summary>How many items are ticked and what they add up to, so a multi-add can be checked before it is committed.</summary>
    public string SelectedQuickFoodsSummaryText =>
        $"Add {SelectedQuickFoodsCount} {(SelectedQuickFoodsCount == 1 ? "item" : "items")} • {DisplayFormat.Calories(SelectedQuickFoodsCalories)}";

    /// <summary>Refreshes the selection total when a row is toggled.</summary>
    private void OnQuickFoodSelectionChanged(QuickFoodItemUiModel model)
    {
        UpdateMultiSelectionSummary();
    }

    /// <summary>Recomputes the multi-select summary and its visibility from the current ticks.</summary>
    private void UpdateMultiSelectionSummary()
    {
        var allSelected = RecentIngredients
            .Concat(RecentSuggestions)
            .Concat(FoodSearchResults)
            .Where(x => x.IsSelected)
            .ToList();

        SelectedQuickFoodsCount = allSelected.Count;
        SelectedQuickFoodsCalories = allSelected.Sum(x => x.Calories);
    }

    [RelayCommand]
    /// <summary>Toggles a row's selection.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "RelayCommand for ViewModel binding.")]
    public void ToggleQuickFoodSelection(QuickFoodItemUiModel? item)
    {
        if (item is null) return;
        item.IsSelected = !item.IsSelected;
    }

    /// <summary>Clears every selected row.</summary>
    [RelayCommand]
    public void ClearQuickFoodSelection()
    {
        foreach (var item in RecentIngredients) item.IsSelected = false;
        foreach (var item in RecentSuggestions) item.IsSelected = false;
        foreach (var item in FoodSearchResults) item.IsSelected = false;
        UpdateMultiSelectionSummary();
    }

    /// <summary>Logs the selected foods in one batch.</summary>
    [RelayCommand]
    public async Task BatchLogSelectedFoodsAsync()
    {
        if (IsWriting || IsLoading || !IsEditModalVisible || !IsFastLoggingVisible) return;
        var selected = RecentIngredients
            .Concat(RecentSuggestions)
            .Concat(FoodSearchResults)
            .Where(x => x.IsSelected)
            .ToList();

        if (selected.Count == 0) return;

        var date = SelectedDate;
        var category = EditingMealType;
        var timestamp = date.ToDateTime(TimeOnly.FromDateTime(DateTime.Now));

        var drafts = selected.Select(item => new LoggedFoodEntry(
            Guid.NewGuid(),
            _currentLogId,
            category,
            item.Name,
            item.ServingSize,
            string.IsNullOrWhiteSpace(item.ServingUnit) ? "g" : item.ServingUnit,
            item.ServingsCount > 0 ? item.ServingsCount : 1.0,
            Math.Max(0, item.Calories),
            Math.Max(0, item.Protein),
            Math.Max(0, item.Carbs),
            Math.Max(0, item.Fat),
            timestamp,
            item.Ingredients.Select(i => new LoggedFoodIngredient(i.FoodItemId, i.Name, i.Grams)).ToList()
        )).ToList();

        var count = drafts.Count;
        await CommitFoodAsync(date,
            () => _dailyLogUseCase.BatchSaveFoodItemsAsync(date, NutritionPresentationMapper.ToLoggedFoods(drafts)),
            count == 1 ? $"Logged {selected[0].Name}." : $"Logged {count} items.");

        ClearQuickFoodSelection();
    }

    /// <summary>Loads the recent, suggested and saved shelves for the date and meal being edited.</summary>
    private async Task LoadFastLoggingShelvesAsync()
    {
        var date = SelectedDate;
        var mealType = EditingMealType;
        IsLoadingFastLogging = true;
        FoodSearchQuery = string.Empty;
        FoodSearchStatus = string.Empty;
        ClearQuickFoodSelection();
        RecentIngredients.Clear();
        RecentSuggestions.Clear();
        FoodSearchResults.Clear();
        NotifyShelfVisibility();

        await _dateLoadLock.WaitAsync();
        try
        {
            // Keep the dependent phase, ingredient, and suggestion loads sequential.
            var phase = await _dailyLogUseCase.GetPhaseForDateAsync(date);
            var ingredients = await _dailyLogUseCase.GetRecentIngredientsAsync(date, mealType);
            var suggestions = await _dailyLogUseCase.GetRecentAcceptedSuggestionsAsync(date, mealType, phase);
            if (!IsEditModalVisible || !IsFastLoggingVisible ||
                date != SelectedDate || mealType != EditingMealType)
                return;

            foreach (var item in ingredients)
                RecentIngredients.Add(new QuickFoodItemUiModel(NutritionPresentationMapper.FromQuickSnapshot(item), OnQuickFoodSelectionChanged));
            foreach (var item in suggestions)
                RecentSuggestions.Add(new QuickFoodItemUiModel(NutritionPresentationMapper.FromQuickSnapshot(item), OnQuickFoodSelectionChanged));

            if (_getRecipesAndTemplatesUseCase is not null)
            {
                var bundle = await _getRecipesAndTemplatesUseCase.ExecuteAsync((int)mealType);
                SavedRecipes.Clear();
                foreach (var r in bundle.Recipes)
                    SavedRecipes.Add(NutritionPresentationMapper.FromRecipeSummary(r));
            }

            NotifyShelfVisibility();
        }
        catch
        {
            FoodSearchStatus = UiTextCatalog.Get("Food_Log_RecentUnavailable");
        }
        finally
        {
            IsLoadingFastLogging = false;
            _dateLoadLock.Release();
        }
    }

    /// <summary>Logs one food straight away.</summary>
    [RelayCommand]
    public async Task QuickLogFoodAsync(object? param)
    {
        var item = param switch
        {
            QuickFoodItemUiModel ui => ui.Item,
            QuickFood dto => dto,
            _ => null
        };
        if (item is null || IsWriting || IsLoading || !IsEditModalVisible || !IsFastLoggingVisible) return;
        var date = SelectedDate;
        var category = EditingMealType;
        var draft = new FoodLogEntry(Guid.NewGuid(), _currentLogId, category, item.Name,
                item.ServingSize, item.ServingUnit, item.ServingsCount, item.Calories, item.Protein, item.Carbs,
                item.Fat,
                date.ToDateTime(TimeOnly.FromDateTime(DateTime.Now)))
            { Ingredients = item.Ingredients };
        await CommitFoodAsync(date,
            () => _dailyLogUseCase.ReLogFoodAsync(date, category,
                NutritionPresentationMapper.ToQuickSnapshot(item), NutritionPresentationMapper.ToLoggedFood(draft)),
            $"Logged {item.Name}.");
    }

    /// <summary>Marks the food as selected.</summary>
    [RelayCommand]
    private void SelectQuickFood(object? param)
    {
        var item = param switch
        {
            QuickFoodItemUiModel ui => ui.Item,
            QuickFood dto => dto,
            _ => null
        };
        if (item is null || IsWriting)
            return;
        _quickDraft = item;
        SetIngredients(item.Ingredients);
        EditingItemName = item.Name;
        EditingServingSize = item.ServingSize;
        EditingServingUnit = string.IsNullOrWhiteSpace(item.ServingUnit) ? "g" : item.ServingUnit;
        EditingServingsCount = item.ServingsCount;
        EditingCalories = Math.Max(0, item.Calories);
        EditingProtein = Math.Max(0, item.Protein);
        EditingCarbs = Math.Max(0, item.Carbs);
        EditingFat = Math.Max(0, item.Fat);
        IsFastLoggingVisible = false;
        IsQuickAddCaloriesVisible = false;
        IsBarcodeScannerVisible = false;
        CancelFoodSearch();
    }

    /// <summary>Opens the manual editor.</summary>
    [RelayCommand]
    private void UseManualFood()
    {
        if (IsWriting) return;
        _quickDraft = null;
        SetIngredients([]);
        IsFastLoggingVisible = false;
        IsQuickAddCaloriesVisible = false;
        IsBarcodeScannerVisible = false;
        CancelFoodSearch();
    }

    /// <summary>Opens the quick calorie entry.</summary>
    [RelayCommand]
    public void OpenQuickAddCalories()
    {
        if (IsWriting) return;
        QuickCaloriesValue = DefaultQuickAddCalories;
        QuickProteinValue = DefaultQuickAddProtein;
        QuickCarbsValue = DefaultQuickAddCarbs;
        QuickFatValue = DefaultQuickAddFat;
        QuickCaloriesName = string.Empty;
        IsFastLoggingVisible = false;
        IsBarcodeScannerVisible = false;
        IsQuickAddCaloriesVisible = true;
        CancelFoodSearch();
    }

    /// <summary>Closes the quick calorie entry.</summary>
    [RelayCommand]
    public void CancelQuickAddCalories()
    {
        if (IsWriting) return;
        IsQuickAddCaloriesVisible = false;
        IsBarcodeScannerVisible = false;
        IsFastLoggingVisible = true;
    }

    /// <summary>Opens the barcode scanner.</summary>
    [RelayCommand]
    public Task OpenBarcodeScannerAsync()
    {
        if (IsWriting) return Task.CompletedTask;
        BarcodeScan.Cancel();
        BarcodeScan.NextRevision();
        BarcodeInputText = string.Empty;
        BarcodeScanStatus = UiTextCatalog.Get(BarcodeScanCoordinator.StatusEnterDigitsKey);
        IsFastLoggingVisible = false;
        IsQuickAddCaloriesVisible = false;
        IsBarcodeScannerVisible = true;
        CancelFoodSearch();

        return Task.CompletedTask;
    }

    /// <summary>The scan coordinator, created on first use so the diary never touches the camera unless asked to.</summary>
    private BarcodeScanCoordinator BarcodeScan =>
        _barcodeScan ??= new BarcodeScanCoordinator(_barcodeHardwareService);

    /// <summary>True when the scanner sheet is open.</summary>
    private bool IsBarcodeScannerOpen => IsBarcodeScannerVisible && IsEditModalVisible;

    /// <summary>Starts the camera scan.</summary>
    [RelayCommand]
    public async Task ScanBarcodeWithCameraAsync()
    {
        if (IsWriting || IsProcessingBarcode || !IsBarcodeScannerVisible) return;

        var scan = await BarcodeScan.ScanAsync(() => IsBarcodeScannerOpen);
        if (scan.BarcodeText is { } barcodeText)
        {
            BarcodeInputText = barcodeText;
            await LookupBarcodeInternalAsync(barcodeText, scan.Symbology, scan.Revision);
            return;
        }

        if (scan.StatusText is { } statusText)
            BarcodeScanStatus = statusText;
    }

    /// <summary>Closes the scanner.</summary>
    [RelayCommand]
    public void CloseBarcodeScanner()
    {
        if (IsWriting) return;
        BarcodeScan.Cancel();
        BarcodeScan.NextRevision();
        IsBarcodeScannerVisible = false;
        IsFastLoggingVisible = true;
        BarcodeScanStatus = string.Empty;
    }

    /// <summary>Looks up the given or last scanned barcode.</summary>
    [RelayCommand]
    public async Task LookupBarcodeAsync(string? barcodeString = null)
    {
        var sessionRevision = BarcodeScan.NextRevision();
        await LookupBarcodeInternalAsync(barcodeString, BarcodeSymbology.Auto, sessionRevision);
    }

    /// <summary>Looks the barcode up and shows the food it finds.</summary>
    private async Task LookupBarcodeInternalAsync(
        string? barcodeString,
        BarcodeSymbology symbology,
        int expectedSessionRevision)
    {
        if (IsWriting || IsProcessingBarcode || _barcodeUseCase is null) return;

        var attempt = await BarcodeScan.LookupAsync(
            _barcodeUseCase,
            barcodeString,
            BarcodeInputText,
            symbology,
            expectedSessionRevision,
            () => IsBarcodeScannerOpen,
            NutritionPresentationMapper.FromCatalogItem,
            setProcessing: isProcessing => IsProcessingBarcode = isProcessing,
            setStatus: statusText => BarcodeScanStatus = statusText);

        if (attempt.Food is not { } food)
            return;

        _quickDraft = null;
        SetIngredients([]);
        EditingItemName = food.Name;
        EditingCalories = Math.Max(0.0, food.Calories!.Value);
        EditingProtein = Math.Max(0.0, food.Protein!.Value);
        EditingCarbs = Math.Max(0.0, food.Carbs!.Value);
        EditingFat = Math.Max(0.0, food.Fats!.Value);
        EditingServingSize = food.ServingSize is > 0f ? food.ServingSize.Value : DefaultQuickAddServingSize;
        EditingServingUnit = string.IsNullOrWhiteSpace(food.ServingSizeUnit) ? "g" : food.ServingSizeUnit;
        EditingServingsCount = DefaultQuickAddServingsCount;

        IsBarcodeScannerVisible = false;
        IsFastLoggingVisible = false;
        IsQuickAddCaloriesVisible = false;

        ShowStatus(UiTextCatalog.Format(
            BarcodeScanCoordinator.StatusFoundInCatalogFormatKey, food.Name,
            Math.Round(food.Calories.Value)));
    }

    /// <summary>Logs the quick calorie entry.</summary>
    [RelayCommand]
    public async Task SaveQuickCaloriesAsync()
    {
        if (IsWriting || IsLoading || !IsEditModalVisible || !IsQuickAddCaloriesVisible) return;

        var date = SelectedDate;
        var category = EditingMealType;
        var name = string.IsNullOrWhiteSpace(QuickCaloriesName) ? DefaultQuickAddTitle : QuickCaloriesName.Trim();
        var calories = Math.Max(0.0, QuickCaloriesValue);
        var protein = Math.Max(0.0, QuickProteinValue);
        var carbs = Math.Max(0.0, QuickCarbsValue);
        var fat = Math.Max(0.0, QuickFatValue);

        var draft = new LoggedFoodEntry(
            Guid.NewGuid(),
            _currentLogId,
            category,
            name,
            DefaultQuickAddServingSize,
            DefaultQuickAddServingUnit,
            DefaultQuickAddServingsCount,
            calories,
            protein,
            carbs,
            fat,
            date.ToDateTime(TimeOnly.FromDateTime(DateTime.Now)),
            []
        );

        await CommitFoodAsync(date,
            () => _dailyLogUseCase.SaveFoodItemAsync(date, NutritionPresentationMapper.ToLoggedFood(draft)),
            $"Logged {name} ({DisplayFormat.Calories(calories)}).");

        IsQuickAddCaloriesVisible = false;
        IsBarcodeScannerVisible = false;
    }

    /// <summary>Searches the local catalog.</summary>
    [RelayCommand]
    private async Task SearchFoodAsync()
    {
        CancelFoodSearch();
        var query = FoodSearchQuery.Trim();
        FoodSearchResults.Clear();
        OnPropertyChanged(nameof(HasFoodSearchResults));
        if (query.Length < 2)
        {
            FoodSearchStatus = UiTextCatalog.Get(FoodStatusEnterTwoLettersKey);
            return;
        }

        IsSearchingFood = true;
        FoodSearchStatus = string.Empty;
        var cancellation = new CancellationTokenSource();
        _foodSearchCancellation = cancellation;
        try
        {
            var foods = await _dailyLogUseCase.SearchLocalFoodsAsync(query, 12, cancellation.Token);
            if (cancellation.IsCancellationRequested || query != FoodSearchQuery.Trim())
                return;
            SetSearchResults(foods.Select(NutritionPresentationMapper.FromFastLookup));
            FoodSearchStatus = foods.Count == 0
                ? UiTextCatalog.Get(FoodStatusNoSavedMatchesKey)
                : UiTextCatalog.Get(FoodStatusSavedFoodsKey);
        }
        catch
        {
            FoodSearchStatus = UiTextCatalog.Get(FoodStatusSavedSearchUnavailableKey);
        }
        finally
        {
            if (_foodSearchCancellation == cancellation)
            {
                _foodSearchCancellation = null;
                IsSearchingFood = false;
            }

            cancellation.Dispose();
        }
    }

    /// <summary>Searches the USDA catalog.</summary>
    [RelayCommand]
    private async Task SearchUsdaAsync()
    {
        CancelFoodSearch();
        var query = FoodSearchQuery.Trim();
        var validation = _dailyLogUseCase.ValidateOnlineFoodSearchQuery(query);
        if (!validation.IsAccepted)
        {
            FoodSearchStatus = UiTextCatalog.Get(FoodStatusUsdaGenericOnlyKey);
            return;
        }

        IsSearchingFood = true;
        FoodSearchStatus = UiTextCatalog.Get(FoodStatusUsdaSearchingKey);
        var cancellation = new CancellationTokenSource();
        _foodSearchCancellation = cancellation;
        try
        {
            var foods = await _dailyLogUseCase.SearchOnlineFoodsAsync(validation.NormalizedQuery, 12, cancellation.Token);
            if (cancellation.IsCancellationRequested || query != FoodSearchQuery.Trim())
                return;
            SetSearchResults(foods.Select(NutritionPresentationMapper.FromFastLookup));
            FoodSearchStatus = foods.Count == 0
                ? UiTextCatalog.Get(FoodStatusUsdaNoResultsKey)
                : UiTextCatalog.Get(FoodStatusUsdaResultsKey);
        }
        catch
        {
            FoodSearchStatus = UiTextCatalog.Get(FoodStatusUsdaUnavailableKey);
        }
        finally
        {
            if (_foodSearchCancellation == cancellation)
            {
                _foodSearchCancellation = null;
                IsSearchingFood = false;
            }

            cancellation.Dispose();
        }
    }

    /// <summary>Replaces the search results with a page of hits.</summary>
    private void SetSearchResults(IEnumerable<QuickFood> results)
    {
        FoodSearchResults.Clear();
        foreach (var item in results)
            FoodSearchResults.Add(new QuickFoodItemUiModel(item, OnQuickFoodSelectionChanged));
        OnPropertyChanged(nameof(HasFoodSearchResults));
        UpdateMultiSelectionSummary();
    }

    private const float DefaultRecipeServingGrams = 100f;
    private const float DefaultRecipeServingsCount = 1f;
    private const string GramsUnit = "g";

    /// <summary>Logs one recipe as a meal.</summary>
    [RelayCommand]
    public async Task LogRecipeAsync(RecipeSummaryItem? recipe)
    {
        if (recipe is null || IsWriting || IsLoading) return;
        var date = SelectedDate;
        var mealType = EditingMealType;

        var servingGrams = recipe.PerServingGrams > 0f ? recipe.PerServingGrams : DefaultRecipeServingGrams;
        var servingsCount = DefaultRecipeServingsCount;
        var calories = recipe.PerServingCalories;
        var protein = recipe.PerServingProtein;
        var carbs = recipe.PerServingCarbs;
        var fats = recipe.PerServingFats;

        var loggedItem = new LoggedFoodEntry(
            Id: Guid.NewGuid(),
            DailyLogId: _currentLogId,
            MealType: mealType,
            Name: recipe.Name,
            ServingSize: servingGrams,
            ServingUnit: GramsUnit,
            ServingsCount: servingsCount,
            Calories: calories,
            Protein: protein,
            Carbs: carbs,
            Fat: fats,
            LoggedAtTimestamp: date.ToDateTime(TimeOnly.FromDateTime(DateTime.Now)),
            Ingredients: []
        );

        await CommitFoodAsync(date,
            () => _dailyLogUseCase.SaveFoodItemAsync(date, NutritionPresentationMapper.ToLoggedFood(loggedItem)),
            $"Logged {DisplayFormat.Decimal2(servingsCount)} {(Math.Abs(servingsCount - 1f) < 0.001f ? "serving" : "servings")} of '{recipe.Name}' ({DisplayFormat.Calories(calories)}).");
    }

    /// <summary>Saves the edited meal as a template.</summary>
    [RelayCommand]
    public async Task SaveMealAsTemplateAsync(object? param)
    {
        if (IsWriting || _saveMealTemplateUseCase is null) return;
        var mealType = param switch
        {
            MealType m => m,
            string s when Enum.TryParse<MealType>(s, out var parsed) => parsed,
            _ => EditingMealType
        };

        var section = GetSectionForMealType(mealType);
        if (section.Items.Count == 0)
        {
            ShowStatus($"No food items in {section.Title} to save as a template.");
            return;
        }

        var templateItems = section.Items.Select(item => new MealTemplateItem(
            Id: Guid.NewGuid(),
            Name: item.Name,
            ServingSize: (float)item.ServingSize,
            ServingUnit: item.ServingUnit,
            ServingsCount: (float)item.ServingsCount,
            Calories: (float)item.Calories,
            Protein: (float)item.Protein,
            Carbs: (float)item.Carbs,
            Fats: (float)item.Fat
        )).ToList();

        var templateName = UiTextCatalog.Format(
            "Nutrition_Template_NameFormat",
            UiTextCatalog.Get($"Nutrition_MealType_{mealType}"),
            DateTime.Today);
        var request = NutritionPresentationMapper.CreateSaveMealTemplateRequest(templateName, (int)mealType, templateItems);
        IsWriting = true;
        try
        {
            await _saveMealTemplateUseCase.ExecuteAsync(request);
            ShowStatus(UiTextCatalog.Format("Nutrition_Template_SavedFormat", templateName));
        }
        catch
        {
            ShowStatus(UiTextCatalog.Get("Nutrition_Template_SaveFailed"));
        }
        finally
        {
            IsWriting = false;
        }
    }

    /// <summary>Saves the entry's notes.</summary>
    [RelayCommand]
    public async Task SaveNotesAsync()
    {
        if (IsWriting || IsLoading || !CanSaveNotes) return;
        IsWriting = true;
        try
        {
            var saved = string.IsNullOrWhiteSpace(NotesText) ? null : NotesText.Trim();
            await _dailyLogUseCase.UpdateNotesAsync(SelectedDate, saved);
            _savedNotes = saved ?? string.Empty;
            NotesText = _savedNotes;
            ShowStatus(UiTextCatalog.Get("Nutrition_FoodLog_NotesSaved"));
        }
        catch
        {
            ShowStatus(UiTextCatalog.Get("Nutrition_FoodLog_NotesSaveFailed"));
        }
        finally
        {
            IsWriting = false;
        }
    }

    /// <summary>Opens the recipe for editing.</summary>
    [RelayCommand]
    public async Task EditRecipeAsync(RecipeSummaryItem? recipe)
    {
        if (recipe is null || IsWriting || IsLoading || _getRecipesAndTemplatesUseCase is null) return;
        IsLoadingFastLogging = true;
        try
        {
            var detailsDto = await _getRecipesAndTemplatesUseCase.GetRecipeAsync(recipe.Id);
            var details = detailsDto is null ? null : NutritionPresentationMapper.FromRecipeDetails(detailsDto);
            if (details is null)
            {
                ShowStatus(UiTextCatalog.Get("Nutrition_Recipe_Unavailable"));
                return;
            }

            LoadSavedRecipeIntoEditor(details);
        }
        catch
        {
            ShowStatus(UiTextCatalog.Get("Nutrition_Recipe_Unavailable"));
        }
        finally
        {
            IsLoadingFastLogging = false;
        }
    }

    /// <summary>Deletes the recipe after confirmation.</summary>
    [RelayCommand]
    public async Task DeleteRecipeAsync(RecipeSummaryItem? recipe)
    {
        if (recipe is null || IsWriting || IsLoading || _getRecipesAndTemplatesUseCase is null) return;

        var confirmed = await ConfirmAsync(
            UiTextCatalog.Get("Nutrition_Recipe_DeleteTitle"),
            UiTextCatalog.Format("Nutrition_Recipe_DeleteMessage", recipe.Name),
            UiTextCatalog.Get("Common_Delete"),
            UiTextCatalog.Get("Common_Cancel"));
        if (!confirmed) return;

        IsWriting = true;
        try
        {
            await _getRecipesAndTemplatesUseCase.DeleteRecipeAsync(recipe.Id);
            var stale = SavedRecipes.FirstOrDefault(saved => saved.Id == recipe.Id);
            if (stale is not null)
                SavedRecipes.Remove(stale);
            NotifyShelfVisibility();
            ShowStatus(UiTextCatalog.Get("Nutrition_Recipe_Deleted"));
        }
        catch
        {
            ShowStatus(UiTextCatalog.Get("Nutrition_Recipe_DeleteFailed"));
        }
        finally
        {
            IsWriting = false;
        }
    }

    /// <summary>Asks the user to confirm the action.</summary>
    private static Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        Shell.Current is { } shell
            ? shell.DisplayAlertAsync(title, message, accept, cancel)
            : Task.FromResult(true);

    /// <summary>Re-publishes whether each shelf has anything to show after a load.</summary>
    private void NotifyShelfVisibility()
    {
        OnPropertyChanged(nameof(HasRecentIngredients));
        OnPropertyChanged(nameof(HasRecentSuggestions));
        OnPropertyChanged(nameof(HasSavedRecipes));
    }

    /// <summary>Abandons a search in flight so its results cannot land after the finder moved on.</summary>
    private void CancelFoodSearch()
    {
        _foodSearchCancellation?.Cancel();
        _foodSearchCancellation = null;
        IsSearchingFood = false;
    }
}
