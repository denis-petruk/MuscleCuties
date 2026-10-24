using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Nutrition;
using MuscleCuties.UI.Models.UI.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Barcode.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.DailyLog;
using MuscleCuties.UseCases.Features.Nutrition.Recipes.Contracts;

namespace MuscleCuties.UI.ViewModels.Nutrition;

public partial class FoodLogViewModel
{
    private const double DefaultQuickAddCalories = 200.0;
    private const double DefaultQuickAddProtein = 0.0;
    private const double DefaultQuickAddCarbs = 0.0;
    private const double DefaultQuickAddFat = 0.0;
    private const double DefaultQuickAddServingSize = 1.0;
    private const double DefaultQuickAddServingsCount = 1.0;
    private const string DefaultQuickAddServingUnit = "serving";
    private const string DefaultQuickAddTitle = "Quick Calorie Add";

    private CancellationTokenSource? _foodSearchCancellation;
    private CancellationTokenSource? _barcodeCameraCancellation;
    private int _barcodeScannerSessionRevision;

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

    public ObservableCollection<QuickFoodItemUiModel> RecentIngredients { get; } = [];
    public ObservableCollection<QuickFoodItemUiModel> RecentSuggestions { get; } = [];
    public ObservableCollection<QuickFoodItemUiModel> FoodSearchResults { get; } = [];
    public ObservableCollection<MealTemplateDto> SavedMealTemplates { get; } = [];
    public ObservableCollection<RecipeSummaryDto> SavedRecipes { get; } = [];

    public bool IsManualEditorVisible => !IsFastLoggingVisible && !IsQuickAddCaloriesVisible &&
                                         !IsBarcodeScannerVisible && !IsRecipeBuilderVisible;
    public bool HasRecentIngredients => RecentIngredients.Count > 0;
    public bool HasRecentSuggestions => RecentSuggestions.Count > 0;
    public bool HasFoodSearchResults => FoodSearchResults.Count > 0;
    public bool HasSavedTemplates => SavedMealTemplates.Count > 0;
    public bool HasSavedRecipes => SavedRecipes.Count > 0;
    public bool HasSelectedQuickFoods => SelectedQuickFoodsCount > 0;

    public string SelectedQuickFoodsSummaryText =>
        $"Add {SelectedQuickFoodsCount} {(SelectedQuickFoodsCount == 1 ? "item" : "items")} • {Math.Round(SelectedQuickFoodsCalories):F0} kcal";

    private void OnQuickFoodSelectionChanged(QuickFoodItemUiModel model)
    {
        UpdateMultiSelectionSummary();
    }

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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "RelayCommand for ViewModel binding.")]
    public void ToggleQuickFoodSelection(QuickFoodItemUiModel? item)
    {
        if (item is null) return;
        item.IsSelected = !item.IsSelected;
    }

    [RelayCommand]
    public void ClearQuickFoodSelection()
    {
        foreach (var item in RecentIngredients) item.IsSelected = false;
        foreach (var item in RecentSuggestions) item.IsSelected = false;
        foreach (var item in FoodSearchResults) item.IsSelected = false;
        UpdateMultiSelectionSummary();
    }

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

        var drafts = selected.Select(item => new LoggedFoodItemDto(
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
            item.Ingredients.Select(i => new LoggedIngredientDto(i.FoodItemId, i.Name, i.Grams)).ToList()
        )).ToList();

        var count = drafts.Count;
        await CommitFoodAsync(date,
            () => _dailyLogUseCase.BatchSaveFoodItemsAsync(date, drafts),
            count == 1 ? $"Logged {selected[0].Name}." : $"Logged {count} items.");

        ClearQuickFoodSelection();
    }

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
                RecentIngredients.Add(new QuickFoodItemUiModel(ToQuickFoodItemDto(item), OnQuickFoodSelectionChanged));
            foreach (var item in suggestions)
                RecentSuggestions.Add(new QuickFoodItemUiModel(ToQuickFoodItemDto(item), OnQuickFoodSelectionChanged));

            if (_getRecipesAndTemplatesUseCase is not null)
            {
                var bundle = await _getRecipesAndTemplatesUseCase.ExecuteAsync((int)mealType);
                SavedMealTemplates.Clear();
                foreach (var t in bundle.Templates)
                    SavedMealTemplates.Add(t);
                SavedRecipes.Clear();
                foreach (var r in bundle.Recipes)
                    SavedRecipes.Add(r);
            }

            NotifyShelfVisibility();
        }
        catch
        {
            FoodSearchStatus = "Recent foods could not load. You can still search or add manually.";
        }
        finally
        {
            IsLoadingFastLogging = false;
            _dateLoadLock.Release();
        }
    }

    [RelayCommand]
    public async Task QuickLogFoodAsync(object? param)
    {
        var item = param switch
        {
            QuickFoodItemUiModel ui => ui.Item,
            QuickFoodItemDto dto => dto,
            _ => null
        };
        if (item is null || IsWriting || IsLoading || !IsEditModalVisible || !IsFastLoggingVisible) return;
        var date = SelectedDate;
        var category = EditingMealType;
        var draft = new FoodItemEntryDto(Guid.NewGuid(), _currentLogId, category, item.Name,
                item.ServingSize, item.ServingUnit, item.ServingsCount, item.Calories, item.Protein, item.Carbs,
                item.Fat,
                date.ToDateTime(TimeOnly.FromDateTime(DateTime.Now)))
            { Ingredients = item.Ingredients };
        await CommitFoodAsync(date,
            () => _dailyLogUseCase.ReLogFoodAsync(date, category, ToQuickSnapshot(item), ToLoggedFoodItemDto(draft)),
            $"Logged {item.Name}.");
    }

    private static QuickFoodItemDto ToQuickFoodItemDto(QuickFoodItemSnapshot snapshot)
    {
        return new QuickFoodItemDto(snapshot.Name, snapshot.ServingSize, snapshot.ServingUnit, snapshot.ServingsCount,
            snapshot.Calories, snapshot.Protein, snapshot.Carbs, snapshot.Fat, snapshot.Detail)
        {
            SourceId = snapshot.SourceId,
            IngredientFoodItemId = snapshot.IngredientFoodItemId,
            Ingredients = snapshot.Ingredients?.Select(i => new HistoryIngredientDto(i.FoodItemId, i.Name, i.Grams))
                .ToList() ?? []
        };
    }

    [RelayCommand]
    private void SelectQuickFood(object? param)
    {
        var item = param switch
        {
            QuickFoodItemUiModel ui => ui.Item,
            QuickFoodItemDto dto => dto,
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

    [RelayCommand]
    public void CancelQuickAddCalories()
    {
        if (IsWriting) return;
        IsQuickAddCaloriesVisible = false;
        IsBarcodeScannerVisible = false;
        IsFastLoggingVisible = true;
    }

    [RelayCommand]
    public Task OpenBarcodeScannerAsync()
    {
        if (IsWriting) return Task.CompletedTask;
        CancelBarcodeCameraCapture();
        _barcodeScannerSessionRevision++;
        BarcodeInputText = string.Empty;
        BarcodeScanStatus = "Enter barcode digits to search saved foods on this device.";
        IsFastLoggingVisible = false;
        IsQuickAddCaloriesVisible = false;
        IsBarcodeScannerVisible = true;
        CancelFoodSearch();

        return Task.CompletedTask;
    }

    [RelayCommand]
    public async Task ScanBarcodeWithCameraAsync()
    {
        if (IsWriting || IsProcessingBarcode || !IsBarcodeScannerVisible) return;
        if (_barcodeHardwareService is null)
        {
            BarcodeScanStatus = "Camera scanning is unavailable on this device. Enter barcode digits manually.";
            return;
        }

        CancelBarcodeCameraCapture();
        var sessionRevision = ++_barcodeScannerSessionRevision;
        using var cts = new CancellationTokenSource();
        _barcodeCameraCancellation = cts;

        try
        {
            var capture = await _barcodeHardwareService.CaptureBarcodeAsync(cts.Token);
            if (sessionRevision != _barcodeScannerSessionRevision || !IsBarcodeScannerVisible || !IsEditModalVisible)
                return;

            switch (capture.Outcome)
            {
                case BarcodeCaptureOutcomeDto.Decoded when !string.IsNullOrWhiteSpace(capture.BarcodeText):
                    BarcodeInputText = capture.BarcodeText.Trim();
                    await LookupBarcodeInternalAsync(capture.BarcodeText.Trim(), capture.Symbology, sessionRevision);
                    break;
                case BarcodeCaptureOutcomeDto.PermissionDenied:
                    BarcodeScanStatus = "Camera permission was denied. You can still enter barcode digits manually.";
                    break;
                case BarcodeCaptureOutcomeDto.UnsupportedSymbology:
                    BarcodeScanStatus = "Unsupported barcode format. Enter EAN-13, UPC-A, EAN-8, UPC-E, or Code-128 digits manually.";
                    break;
                case BarcodeCaptureOutcomeDto.Cancelled:
                    break;
                default:
                    BarcodeScanStatus = "Camera is unavailable. Enter barcode digits manually.";
                    break;
            }
        }
        finally
        {
            if (ReferenceEquals(_barcodeCameraCancellation, cts))
                _barcodeCameraCancellation = null;
        }
    }

    [RelayCommand]
    public void CloseBarcodeScanner()
    {
        if (IsWriting) return;
        CancelBarcodeCameraCapture();
        _barcodeScannerSessionRevision++;
        IsBarcodeScannerVisible = false;
        IsFastLoggingVisible = true;
        BarcodeScanStatus = string.Empty;
    }

    private void CancelBarcodeCameraCapture()
    {
        _barcodeCameraCancellation?.Cancel();
        _barcodeCameraCancellation = null;
        _barcodeHardwareService?.CancelActiveCapture();
    }

    [RelayCommand]
    public async Task LookupBarcodeAsync(string? barcodeString = null)
    {
        var sessionRevision = ++_barcodeScannerSessionRevision;
        await LookupBarcodeInternalAsync(barcodeString, BarcodeSymbologyDto.Auto, sessionRevision);
    }

    private async Task LookupBarcodeInternalAsync(
        string? barcodeString,
        BarcodeSymbologyDto symbology,
        int expectedSessionRevision)
    {
        if (IsWriting || IsProcessingBarcode || _barcodeUseCase is null) return;

        var rawInput = string.IsNullOrWhiteSpace(barcodeString) ? BarcodeInputText : barcodeString;
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            BarcodeScanStatus = "Enter barcode digits to search.";
            return;
        }

        IsProcessingBarcode = true;
        BarcodeScanStatus = "Searching saved foods on this device…";

        try
        {
            var request = new BarcodeLookupRequest(rawInput.Trim(), symbology);
            var result = await _barcodeUseCase.ExecuteAsync(request);
            if (expectedSessionRevision != _barcodeScannerSessionRevision || !IsBarcodeScannerVisible || !IsEditModalVisible)
                return;

            if (!result.IsValidBarcode)
            {
                BarcodeScanStatus = result.ErrorMessage ?? "Invalid barcode format or checksum.";
                return;
            }

            if (!result.IsFound || result.FoodItem is null)
            {
                BarcodeScanStatus = $"Barcode {result.NormalizedBarcode} not found in saved foods. You can add it manually.";
                return;
            }

            var food = result.FoodItem;
            if (food.Calories is null || food.Protein is null || food.Carbs is null || food.Fats is null)
            {
                BarcodeScanStatus = "Some nutrition values are unknown. Add this food manually before logging.";
                return;
            }

            _quickDraft = null;
            SetIngredients([]);
            EditingItemName = food.Name;
            EditingCalories = Math.Max(0.0, food.Calories.Value);
            EditingProtein = Math.Max(0.0, food.Protein.Value);
            EditingCarbs = Math.Max(0.0, food.Carbs.Value);
            EditingFat = Math.Max(0.0, food.Fats.Value);
            EditingServingSize = food.ServingSize is > 0f ? food.ServingSize.Value : DefaultQuickAddServingSize;
            EditingServingUnit = string.IsNullOrWhiteSpace(food.ServingSizeUnit) ? "g" : food.ServingSizeUnit;
            EditingServingsCount = DefaultQuickAddServingsCount;

            IsBarcodeScannerVisible = false;
            IsFastLoggingVisible = false;
            IsQuickAddCaloriesVisible = false;

            ShowStatus($"Found locally: {food.Name} ({Math.Round(food.Calories.Value):F0} kcal)");
        }
        catch
        {
            if (expectedSessionRevision == _barcodeScannerSessionRevision)
                BarcodeScanStatus = "Barcode lookup failed. Check digits and retry.";
        }
        finally
        {
            IsProcessingBarcode = false;
        }
    }

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

        var draft = new LoggedFoodItemDto(
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
            () => _dailyLogUseCase.SaveFoodItemAsync(date, draft),
            $"Logged {name} ({Math.Round(calories):F0} kcal).");

        IsQuickAddCaloriesVisible = false;
        IsBarcodeScannerVisible = false;
    }

    [RelayCommand]
    private async Task SearchFoodAsync()
    {
        CancelFoodSearch();
        var query = FoodSearchQuery.Trim();
        FoodSearchResults.Clear();
        OnPropertyChanged(nameof(HasFoodSearchResults));
        if (query.Length < 2)
        {
            FoodSearchStatus = "Enter at least two letters to search your device.";
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
            SetSearchResults(foods.Select(ToQuickFood));
            FoodSearchStatus = foods.Count == 0
                ? "No local matches. Search USDA or add manually."
                : "Saved foods on your device";
        }
        catch
        {
            FoodSearchStatus = "Local search is unavailable. Add a food manually.";
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

    [RelayCommand]
    private async Task SearchUsdaAsync()
    {
        CancelFoodSearch();
        var query = FoodSearchQuery.Trim();
        var validation = _dailyLogUseCase.ValidateOnlineFoodSearchQuery(query);
        if (!validation.IsAccepted)
        {
            FoodSearchStatus = "USDA search accepts generic foods only, such as chicken breast.";
            return;
        }

        IsSearchingFood = true;
        FoodSearchStatus = "Searching USDA…";
        var cancellation = new CancellationTokenSource();
        _foodSearchCancellation = cancellation;
        try
        {
            var foods = await _dailyLogUseCase.SearchOnlineFoodsAsync(validation.NormalizedQuery, 12, cancellation.Token);
            if (cancellation.IsCancellationRequested || query != FoodSearchQuery.Trim())
                return;
            SetSearchResults(foods.Select(ToQuickFood));
            FoodSearchStatus = foods.Count == 0
                ? "No USDA results or the 8-second lookup timed out. Your local foods remain available."
                : "USDA results";
        }
        catch
        {
            FoodSearchStatus = "USDA lookup is unavailable. Search your device or add manually.";
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

    private static QuickFoodItemDto ToQuickFood(FastLookupFoodItemDto food)
    {
        return new QuickFoodItemDto(food.Name, 100, "g", 1, food.Calories, food.Protein,
            food.Carbs, food.Fats, food.IsCustom ? "Custom food · per 100 g" : "Per 100 g");
    }

    private void SetSearchResults(IEnumerable<QuickFoodItemDto> results)
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

    [RelayCommand]
    public async Task LogMealTemplateAsync(MealTemplateDto? template)
    {
        if (template is null || IsWriting || IsLoading) return;
        var date = SelectedDate;
        var mealType = EditingMealType;
        var itemsToLog = template.Items.Select(item => new LoggedFoodItemDto(
            Id: Guid.NewGuid(),
            DailyLogId: _currentLogId,
            MealType: mealType,
            Name: item.Name,
            ServingSize: item.ServingSize,
            ServingUnit: item.ServingUnit,
            ServingsCount: item.ServingsCount,
            Calories: item.Calories,
            Protein: item.Protein,
            Carbs: item.Carbs,
            Fat: item.Fats,
            LoggedAtTimestamp: date.ToDateTime(TimeOnly.FromDateTime(DateTime.Now)),
            Ingredients: []
        )).ToList();

        await CommitFoodAsync(date,
            () => _dailyLogUseCase.BatchSaveFoodItemsAsync(date, itemsToLog),
            $"Logged template '{template.Name}' ({template.Items.Count} items).");
    }

    [RelayCommand]
    public async Task LogRecipeAsync(RecipeSummaryDto? recipe)
    {
        if (recipe is null || IsWriting || IsLoading) return;
        var date = SelectedDate;
        var mealType = EditingMealType;

        var preview = SelectedRecipePortionPreview is { } existingPreview && existingPreview.RecipeId == recipe.Id
            ? existingPreview
            : null;

        var servingGrams = preview is not null
            ? preview.SelectedGrams
            : (recipe.PerServingGrams > 0f ? recipe.PerServingGrams : DefaultRecipeServingGrams);
        var servingsCount = preview is not null
            ? preview.SelectedServingsCount
            : DefaultRecipeServingsCount;
        var calories = preview is not null ? preview.Calories : recipe.PerServingCalories;
        var protein = preview is not null ? preview.Protein : recipe.PerServingProtein;
        var carbs = preview is not null ? preview.Carbs : recipe.PerServingCarbs;
        var fats = preview is not null ? preview.Fats : recipe.PerServingFats;

        var loggedItem = new LoggedFoodItemDto(
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
            () => _dailyLogUseCase.SaveFoodItemAsync(date, loggedItem),
            $"Logged {servingsCount:0.##} {(Math.Abs(servingsCount - 1f) < 0.001f ? "serving" : "servings")} of '{recipe.Name}' ({Math.Round(calories):F0} kcal).");
    }

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

        var templateItems = section.Items.Select(item => new MealTemplateItemDto(
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

        var templateName = $"{section.Title} Template ({DateTime.Today:MMM d})";
        var request = new SaveMealAsTemplateRequest(templateName, (int)mealType, templateItems);
        await _saveMealTemplateUseCase.ExecuteAsync(request);
        ShowStatus($"Saved {section.Title} as template '{templateName}'.");
    }

    private void NotifyShelfVisibility()
    {
        OnPropertyChanged(nameof(HasRecentIngredients));
        OnPropertyChanged(nameof(HasRecentSuggestions));
        OnPropertyChanged(nameof(HasSavedTemplates));
        OnPropertyChanged(nameof(HasSavedRecipes));
    }

    private void CancelFoodSearch()
    {
        _foodSearchCancellation?.Cancel();
        _foodSearchCancellation = null;
        IsSearchingFood = false;
    }
}
