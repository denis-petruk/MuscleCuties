using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Nutrition;
using MuscleCuties.UI.Models.UI.Nutrition;
using MuscleCuties.UI.Services.Formatting;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UI.Services.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Barcode;
using MuscleCuties.UseCases.Features.Nutrition.DailyLog;
using MuscleCuties.UseCases.Features.Nutrition.Overview;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.Recipes;
using MuscleCuties.UseCases.Features.Nutrition.Recipes.Contracts;
using MuscleCuties.UseCases.Features.Hydration.Contracts;

namespace MuscleCuties.UI.ViewModels.Nutrition;

public partial class FoodLogViewModel : ObservableObject, IDisposable
{
    private const int PreviousDayOffset = -1;
    private readonly ILookupBarcodeUseCase? _barcodeUseCase;
    private readonly IBarcodeScannerHardwareService? _barcodeHardwareService;
    private readonly ISaveMealTemplateUseCase? _saveMealTemplateUseCase;
    private readonly IGetRecipesAndTemplatesUseCase? _getRecipesAndTemplatesUseCase;
    private readonly ICreateRecipeUseCase? _createRecipeUseCase;
    private readonly IRecipePortionUseCase _recipePortionUseCase;
    private readonly INutritionChangeNotifier? _changeNotifier;
    private readonly INutritionDailyLogUseCase _dailyLogUseCase;
    private readonly INutritionOverviewUseCase? _overviewUseCase;
    private readonly SemaphoreSlim _dateLoadLock = new(1, 1);
    // F02: hydration writes are serialized and only the confirmed total is trusted. A failed
    // write rolls the visible total back and keeps a retry target instead of silently claiming
    // the amount was saved.
    private readonly SemaphoreSlim _waterWriteLock = new(1, 1);
    private int _confirmedWaterIntakeMl;
    private int? _pendingWaterIntakeMl;
    private readonly Func<Task>? _navigateBackAsync;
    private Guid _currentLogId;
    private int _dateLoadVersion;

    private DateOnly _draftDate;
    private DateTime _draftTimestamp;
    private int? _editingLoggedMealId;
    private IReadOnlyList<NutritionMealIngredient> _editingMealIngredients = [];

    [ObservableProperty] private double _editingCalories = 250;

    [ObservableProperty] private double _editingCarbs = 25;

    [ObservableProperty] private double _editingFat = 8;

    [ObservableProperty] private Guid _editingItemId;

    [ObservableProperty] private string _editingItemName = string.Empty;

    private DateTime _editingLoggedAtTimestamp;

    [ObservableProperty] private MealType _editingMealType;

    [ObservableProperty] private double _editingProtein = 20;

    [ObservableProperty] private double _editingServingsCount = 1.0;

    [ObservableProperty] private double _editingServingSize = 100;

    [ObservableProperty] private string _editingServingUnit = "g";

    private bool _isActive;

    [ObservableProperty] private bool _isDatePickerOpen;

    [ObservableProperty] private bool _isEditingExisting;
    [ObservableProperty] private bool _isMealContextLocked;

    // Modal state
    [ObservableProperty] private bool _isEditModalVisible;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsPlainFoodEditor))]
    private bool _isGroupedEditor;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanChangeHydration))]
    private bool _isLoadError;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanChangeHydration))]
    private bool _isLoading;

    [ObservableProperty] private bool _isStatusVisible;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanChangeHydration))]
    private bool _isWriting;

    [ObservableProperty] private string _notesText = string.Empty;

    [ObservableProperty] private DateTime _pickerDate;

    private QuickFood? _quickDraft;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(FormattedSelectedDate), nameof(IsSelectedDateToday))]
    private DateOnly _selectedDate;

    [ObservableProperty] private string _statusMessage = string.Empty;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CalorieProgressRatio), nameof(CalorieRemainingText))]
    private double _targetCalories = 2000;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CarbsProgressRatio))]
    private double _targetCarbs = 220;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(FatProgressRatio))]
    private double _targetFat = 65;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ProteinProgressRatio))]
    private double _targetProtein = 130;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CalorieProgressRatio), nameof(CalorieRemainingText))]
    private double _totalCalories;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CarbsProgressRatio))]
    private double _totalCarbs;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(FatProgressRatio))]
    private double _totalFat;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ProteinProgressRatio))]
    private double _totalProtein;

    [ObservableProperty] private int _waterIntakeMl;

    [ObservableProperty] private bool _isSavingWater;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasWaterSaveError))]
    private string _waterSaveError = string.Empty;

    public bool HasWaterSaveError => !string.IsNullOrWhiteSpace(WaterSaveError);

    public FoodLogViewModel(
        INutritionDailyLogUseCase dailyLogUseCase,
        IRecipePortionUseCase recipePortionUseCase,
        WaterQuickAddDefaults waterQuickAddDefaults,
        Func<Task>? navigateBackAsync = null,
        INutritionChangeNotifier? changeNotifier = null,
        ILookupBarcodeUseCase? barcodeUseCase = null,
        IBarcodeScannerHardwareService? barcodeHardwareService = null,
        ISaveMealTemplateUseCase? saveMealTemplateUseCase = null,
        IGetRecipesAndTemplatesUseCase? getRecipesAndTemplatesUseCase = null,
        ICreateRecipeUseCase? createRecipeUseCase = null,
        INutritionOverviewUseCase? overviewUseCase = null,
        HydrationClient? hydrationClient = null)
    {
        _dailyLogUseCase = dailyLogUseCase;
        _recipePortionUseCase = recipePortionUseCase;
        WaterPresetVolumesMl = new ObservableCollection<int>(waterQuickAddDefaults.VolumesMillilitres);
        _navigateBackAsync = navigateBackAsync;
        _changeNotifier = changeNotifier;
        _barcodeUseCase = barcodeUseCase;
        _barcodeHardwareService = barcodeHardwareService;
        _saveMealTemplateUseCase = saveMealTemplateUseCase;
        _getRecipesAndTemplatesUseCase = getRecipesAndTemplatesUseCase;
        _createRecipeUseCase = createRecipeUseCase;
        _overviewUseCase = overviewUseCase;
        _hydrationClient = hydrationClient;
        Breakdown = new NutritionBreakdownPresenter();

        MealSections.Add(BreakfastSection);
        MealSections.Add(LunchSection);
        MealSections.Add(DinnerSection);
        MealSections.Add(SnackSection);

        var today = DateOnly.FromDateTime(DateTime.Today);
        _selectedDate = today;
        _pickerDate = DateTime.Today;

        PopulateDateStrip(today);
    }

    /// <summary>RM05: the same breakdown presentation the Nutrition page uses, loaded for this page's day.</summary>
    public NutritionBreakdownPresenter Breakdown { get; }

    public bool IsPlainFoodEditor => !IsGroupedEditor;
    public bool CanDeleteEditingFood => IsEditingExisting && !IsMealContextLocked;
    public string MealContextSummary =>
        $"{UiTextCatalog.Get($"Nutrition_MealType_{EditingMealType}")} · {MealTime.Text(_draftTimestamp)}";
    public string EditModalTitle => IsEditingExisting
        ? (IsMealContextLocked
            ? UiTextCatalog.Get("Nutrition_Breakdown_EditMeal")
            : UiTextCatalog.Get("Nutrition_FoodLog_EditEntryTitle"))
        : UiTextCatalog.Get("Nutrition_FoodLog_LogEntryTitle");

    /// <summary>Draft totals for the meal being edited, so the sheet can open on what it contains.</summary>
    public string EditDraftCaloriesText => NutritionFormat.Calories(EditingIngredients.Sum(i => (double)i.MacrosForDraft.Calories));
    public string EditDraftProteinText => NutritionFormat.Grams(EditingIngredients.Sum(i => (double)i.MacrosForDraft.Protein));
    public string EditDraftCarbsText => NutritionFormat.Grams(EditingIngredients.Sum(i => (double)i.MacrosForDraft.Carbs));
    public string EditDraftFatsText => NutritionFormat.Grams(EditingIngredients.Sum(i => (double)i.MacrosForDraft.Fats));
    public ObservableCollection<HistoryIngredientEditor> EditingIngredients { get; } = [];
    public IReadOnlyList<MealType> MealCategories { get; } = Enum.GetValues<MealType>();

    /// <summary>Days shown either side of the selected day, so the seven cells fit one row.</summary>
    private const int DaysAroundSelectedDate = 3;

    public ObservableCollection<DateStripDayItem> DateStripDays { get; } = [];

    public MealSectionUiGroup BreakfastSection { get; } = new(MealType.Breakfast, "Breakfast");
    public MealSectionUiGroup LunchSection { get; } = new(MealType.Lunch, "Lunch");
    public MealSectionUiGroup DinnerSection { get; } = new(MealType.Dinner, "Dinner");
    public MealSectionUiGroup SnackSection { get; } = new(MealType.Snack, "Snacks");

    public ObservableCollection<MealSectionUiGroup> MealSections { get; } = [];

    public double CalorieProgressRatio =>
        TargetCalories > 0 ? Math.Clamp(TotalCalories / TargetCalories, 0.0, 1.0) : 0.0;

    public double ProteinProgressRatio => TargetProtein > 0 ? Math.Clamp(TotalProtein / TargetProtein, 0.0, 1.0) : 0.0;
    public double CarbsProgressRatio => TargetCarbs > 0 ? Math.Clamp(TotalCarbs / TargetCarbs, 0.0, 1.0) : 0.0;
    public double FatProgressRatio => TargetFat > 0 ? Math.Clamp(TotalFat / TargetFat, 0.0, 1.0) : 0.0;

    public string CalorieRemainingText
    {
        get
        {
            var diff = TargetCalories - TotalCalories;
            return diff >= 0
                ? $"{NutritionFormat.Calories(Math.Round(diff, 0))} remaining"
                : $"{NutritionFormat.Calories(Math.Abs(Math.Round(diff, 0)))} over";
        }
    }

    public bool IsSelectedDateToday => SelectedDate == DateOnly.FromDateTime(DateTime.Today);

    public string FormattedSelectedDate
    {
        get
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (SelectedDate == today)
                return "Today, " + SelectedDate.ToString("m", CultureInfo.CurrentCulture);
            if (SelectedDate == today.AddDays(-1))
                return "Yesterday, " + SelectedDate.ToString("m", CultureInfo.CurrentCulture);
            if (SelectedDate == today.AddDays(1))
                return "Tomorrow, " + SelectedDate.ToString("m", CultureInfo.CurrentCulture);

            return SelectedDate.ToString("ddd", CultureInfo.CurrentCulture) + ", " +
                   SelectedDate.ToString("m", CultureInfo.CurrentCulture);
        }
    }

    public void Dispose()
    {
        _isActive = false;
        ResetHydration();
        Breakdown.Clear();
        _dateLoadLock.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnNutritionChanged(object? sender, NutritionChangedEventArgs e)
    {
        if (_isActive && !IsWriting && e.Date == SelectedDate)
            _ = LoadLogForDateAsync(SelectedDate);
    }

    public async Task InitializeAsync()
    {
        if (!_isActive && _changeNotifier is not null)
            _changeNotifier.NutritionChanged += OnNutritionChanged;
        _isActive = true;
        await LoadLogForDateAsync(SelectedDate);
    }

    public void Deactivate()
    {
        _isActive = false;
        ResetHydration();
        Breakdown.Clear();
        CloseEditModal();
        if (_changeNotifier is not null) _changeNotifier.NutritionChanged -= OnNutritionChanged;
        CancelFoodSearch();
    }

    [RelayCommand]
    private Task RetryLoadAsync()
    {
        return LoadLogForDateAsync(SelectedDate);
    }

    /// <summary>
    /// RM05: opens the shared breakdown for the day this page is showing. The captured load version
    /// and date keep a slower response for a previous day from replacing the sheet.
    /// </summary>
    [RelayCommand]
    private async Task OpenBreakdownAsync()
    {
        var date = SelectedDate;
        var version = Volatile.Read(ref _dateLoadVersion);
        var title = UiTextCatalog.Get("Nutrition_Breakdown_DailyTitle");
        Breakdown.ShowLoading(title);

        if (_overviewUseCase is null)
        {
            Breakdown.ShowError(title, UiTextCatalog.Get("Nutrition_Breakdown_LoadFailed"));
            return;
        }

        try
        {
            var response = await _overviewUseCase.GetOverviewAsync(
                new NutritionOverviewRequest(date, "Savoury"));
            if (version != Volatile.Read(ref _dateLoadVersion) || date != SelectedDate)
                return;

            Breakdown.Show(
                $"{title} · {date:MMM d}",
                NutritionPresentationMapper.FromTotals(
                    response.Consumed.Calories, response.Consumed.Protein,
                    response.Consumed.Carbs, response.Consumed.Fats),
                response.Micronutrients.Select(NutritionPresentationMapper.FromMicronutrientItem),
                null,
                NutritionBreakdownPresenter.DescribeCoverage(
                    NutritionPresentationMapper.FromMicronutrientCoverage(response.MicronutrientCoverage)));
        }
        catch
        {
            if (version == Volatile.Read(ref _dateLoadVersion) && date == SelectedDate)
                Breakdown.ShowError(title, UiTextCatalog.Get("Nutrition_Breakdown_LoadFailed"));
        }
    }

    partial void OnSelectedDateChanged(DateOnly value)
    {
        ResetHydration();
        // The sheet belongs to one day; moving the date closes it before any old response can land.
        Breakdown.Clear();
        CloseEditModal();
        foreach (var section in MealSections)
        {
            section.Items.Clear();
            section.RefreshSummary();
        }

        TotalCalories = TotalProtein = TotalCarbs = TotalFat = 0;
        WaterIntakeMl = 0;
        NotesText = string.Empty;
    }

    public async Task OpenRecordAsync(Guid id)
    {
        try
        {
            var item = await _dailyLogUseCase.GetFoodItemAsync(id);
            if (item is null)
            {
                ShowStatus("This entry was deleted or is unavailable. Refresh your food log.");
                return;
            }

            SelectedDate = DateOnly.FromDateTime(item.LoggedAtTimestamp);
            PickerDate = SelectedDate.ToDateTime(TimeOnly.MinValue);
            PopulateDateStrip(SelectedDate);
            await LoadLogForDateAsync(SelectedDate);
            await OpenEditFoodAsync(NutritionPresentationMapper.FromLoggedFood(item));
        }
        catch { ShowStatus("Could not open this entry. Refresh and try again."); }
    }

    private void PopulateDateStrip(DateOnly centerDate)
    {
        DateStripDays.Clear();
        var today = DateOnly.FromDateTime(DateTime.Today);

        // The strip covers the week around the selected day rather than a longer rolling window.
        for (var offset = -DaysAroundSelectedDate; offset <= DaysAroundSelectedDate; offset++)
        {
            var dayNumber = centerDate.DayNumber + offset;
            if (dayNumber < DateOnly.MinValue.DayNumber || dayNumber > DateOnly.MaxValue.DayNumber)
                continue;
            var date = DateOnly.FromDayNumber(dayNumber);
            var item = new DateStripDayItem(date, date == today)
            {
                IsSelected = date == SelectedDate
            };
            item.Position = DateStripDays.Count;
            DateStripDays.Add(item);
        }
    }

    [RelayCommand]
    public async Task SelectDateAsync(DateStripDayItem day)
    {
        if (day.Date == SelectedDate)
            return;

        SelectedDate = day.Date;
        PickerDate = day.Date.ToDateTime(TimeOnly.MinValue);

        foreach (var d in DateStripDays)
            d.IsSelected = d.Date == SelectedDate;

        await LoadLogForDateAsync(SelectedDate);
    }

    [RelayCommand]
    public async Task JumpToTodayAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (SelectedDate == today)
            return;

        SelectedDate = today;
        PickerDate = DateTime.Today;
        PopulateDateStrip(today);

        foreach (var d in DateStripDays)
            d.IsSelected = d.Date == SelectedDate;

        await LoadLogForDateAsync(SelectedDate);
    }

    [RelayCommand]
    public async Task PreviousDayAsync()
    {
        if (SelectedDate == DateOnly.MinValue)
            return;
        var prev = SelectedDate.AddDays(-1);
        SelectedDate = prev;
        PickerDate = prev.ToDateTime(TimeOnly.MinValue);

        var match = DateStripDays.FirstOrDefault(d => d.Date == prev);
        if (match is null)
            PopulateDateStrip(prev);
        else
            foreach (var d in DateStripDays)
                d.IsSelected = d.Date == SelectedDate;

        await LoadLogForDateAsync(SelectedDate);
    }

    [RelayCommand]
    public async Task NextDayAsync()
    {
        if (SelectedDate == DateOnly.MaxValue)
            return;
        var next = SelectedDate.AddDays(1);
        SelectedDate = next;
        PickerDate = next.ToDateTime(TimeOnly.MinValue);

        var match = DateStripDays.FirstOrDefault(d => d.Date == next);
        if (match is null)
            PopulateDateStrip(next);
        else
            foreach (var d in DateStripDays)
                d.IsSelected = d.Date == SelectedDate;

        await LoadLogForDateAsync(SelectedDate);
    }

    [RelayCommand]
    public void ToggleDatePicker()
    {
        IsDatePickerOpen = !IsDatePickerOpen;
    }

    [RelayCommand]
    public async Task DatePickerSelectedAsync(DateTime newDate)
    {
        IsDatePickerOpen = false;
        var dateOnly = DateOnly.FromDateTime(newDate);
        if (dateOnly == SelectedDate)
            return;

        SelectedDate = dateOnly;
        PickerDate = newDate;
        PopulateDateStrip(dateOnly);

        foreach (var d in DateStripDays)
            d.IsSelected = d.Date == SelectedDate;

        await LoadLogForDateAsync(SelectedDate);
    }

    [RelayCommand]
    public Task OpenAddFoodAsync(MealType mealType) => OpenAddFoodCoreAsync(mealType, null);

    private async Task OpenAddFoodCoreAsync(MealType mealType, int? minuteOfDay)
    {
        if (IsLoading || IsWriting || IsLoadError)
            return;
        _draftDate = SelectedDate;
        // The Nutrition meal-entry sheet may hand over the time chosen there; otherwise, now.
        var hasLogTime = minuteOfDay is int minutes &&
                         minutes >= TimeSpan.Zero.TotalMinutes &&
                         minutes < TimeSpan.FromDays(1).TotalMinutes;
        var loggedAt = hasLogTime
            ? TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minuteOfDay!.Value))
            : TimeOnly.FromDateTime(DateTime.Now);
        _draftTimestamp = SelectedDate.ToDateTime(loggedAt);
        _quickDraft = null;
        _editingLoggedMealId = null;
        _editingMealIngredients = [];
        IsMealContextLocked = false;
        SetIngredients([]);
        IsEditingExisting = false;
        EditingItemId = Guid.NewGuid();
        _editingLoggedAtTimestamp = default;
        EditingMealType = mealType;
        EditingItemName = string.Empty;
        EditingServingSize = 100;
        EditingServingUnit = "g";
        EditingServingsCount = 1.0;
        EditingCalories = 200;
        EditingProtein = 15;
        EditingCarbs = 25;
        EditingFat = 5;
        IsQuickAddCaloriesVisible = false;
        IsBarcodeScannerVisible = false;
        IsFastLoggingVisible = true;
        IsEditModalVisible = true;
        await LoadFastLoggingShelvesAsync();
    }

    /// <summary>
    /// Interprets the add-food intent carried by a navigation query. The page only moves the
    /// raw values, so the meaning of a meal name or a minute of day stays in this layer.
    /// </summary>
    public async Task OpenAddFoodIntentAsync(string? mealTypeName, int? minuteOfDay)
    {
        if (!Enum.TryParse<MealType>(mealTypeName, true, out var mealType) || !Enum.IsDefined(mealType))
            return;

        await OpenAddFoodCoreAsync(mealType, minuteOfDay);
    }

    // Kept for callers that pass the meal category as text.
    public void OpenAddFood(string mealTypeStr)
    {
        if (!Enum.TryParse<MealType>(mealTypeStr, true, out var mealType))
            mealType = MealType.Breakfast;
        _ = OpenAddFoodAsync(mealType);
    }

    [RelayCommand]
    public Task OpenEditFoodAsync(FoodLogEntry item) => OpenEditFoodCoreAsync(item, false);

    [RelayCommand]
    private async Task EditMealAsync(MealSectionUiGroup? section)
    {
        if (section?.CanEditMeal == true && section.Items.Count == 1)
            await OpenEditFoodCoreAsync(section.Items[0], true);
    }

    private async Task OpenEditFoodCoreAsync(FoodLogEntry item, bool lockContext)
    {
        if (IsLoading || IsWriting || IsLoadError)
            return;

        _draftDate = item.LoggedAtTimestamp != default ? DateOnly.FromDateTime(item.LoggedAtTimestamp) : SelectedDate;
        _draftTimestamp = item.LoggedAtTimestamp;
        _quickDraft = null;
        _editingLoggedMealId = item.LoggedMealId;
        _editingMealIngredients = [];
        SetIngredients(item.Ingredients);

        if (item.LoggedMealId is > 0 && _overviewUseCase is not null)
        {
            List<NutritionMealIngredient> ingredients;
            try
            {
                ingredients = (await _overviewUseCase.GetMealIngredientsAsync(item.LoggedMealId.Value))
                    .Select(NutritionPresentationMapper.ToMealIngredient)
                    .ToList();
            }
            catch
            {
                ShowStatus("Could not open this meal. Refresh and try again.");
                return;
            }

            if (ingredients.Count == 0)
            {
                ShowStatus("This meal has no editable ingredients. Refresh and try again.");
                return;
            }

            _editingMealIngredients = ingredients;
            SetMealIngredients(_editingMealIngredients);
        }

        IsFastLoggingVisible = false;
        IsQuickAddCaloriesVisible = false;
        IsBarcodeScannerVisible = false;
        IsEditingExisting = true;
        IsMealContextLocked = lockContext || item.LoggedMealId is > 0;
        EditingItemId = item.Id;
        _editingLoggedAtTimestamp = item.LoggedAtTimestamp;
        EditingMealType = item.MealType;
        EditingItemName = item.Name;
        EditingServingSize = item.ServingSize;
        EditingServingUnit = item.ServingUnit;
        EditingServingsCount = item.ServingsCount;
        EditingCalories = item.Calories;
        EditingProtein = item.Protein;
        EditingCarbs = item.Carbs;
        EditingFat = item.Fat;

        IsEditModalVisible = true;
    }

    [RelayCommand]
    public void CloseEditModal()
    {
        ResetRecipeBuilder();
        IsEditModalVisible = false;
        IsQuickAddCaloriesVisible = false;
        IsBarcodeScannerVisible = false;
        _editingLoggedMealId = null;
        _editingMealIngredients = [];
        IsMealContextLocked = false;
        CancelFoodSearch();
    }

    partial void OnIsMealContextLockedChanged(bool value) =>
        NotifyEditingContextProperties();

    partial void OnIsEditingExistingChanged(bool value) =>
        NotifyEditingContextProperties();

    partial void OnEditingMealTypeChanged(MealType value) =>
        OnPropertyChanged(nameof(MealContextSummary));

    private void NotifyEditingContextProperties()
    {
        OnPropertyChanged(nameof(CanDeleteEditingFood));
        OnPropertyChanged(nameof(MealContextSummary));
        OnPropertyChanged(nameof(EditModalTitle));
    }

    private void SetIngredients(IReadOnlyList<FoodHistoryIngredient> ingredients)
    {
        EditingIngredients.Clear();
        foreach (var ingredient in ingredients)
            EditingIngredients.Add(new HistoryIngredientEditor(ingredient));
        IsGroupedEditor = ingredients.Count > 0;
        TrackEditingIngredientTotals();
    }

    /// <summary>
    /// The meal being edited arrives with per-100 g macros, so each row can show the nutrition of the
    /// portion it is about to save and the sheet can total them while quantities change.
    /// </summary>
    private void SetMealIngredients(IReadOnlyList<NutritionMealIngredient> ingredients)
    {
        EditingIngredients.Clear();
        foreach (var ingredient in ingredients)
            EditingIngredients.Add(new HistoryIngredientEditor(
                new FoodHistoryIngredient(ingredient.FoodItemId, ingredient.Name, ingredient.Grams),
                ingredient.Calories, ingredient.Protein, ingredient.Carbs, ingredient.Fats));
        IsGroupedEditor = ingredients.Count > 0;
        TrackEditingIngredientTotals();
    }

    private void TrackEditingIngredientTotals()
    {
        foreach (var editor in EditingIngredients)
        {
            editor.PropertyChanged -= OnEditingIngredientChanged;
            editor.PropertyChanged += OnEditingIngredientChanged;
        }

        NotifyEditingIngredientTotals();
    }

    private void OnEditingIngredientChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HistoryIngredientEditor.GramsText))
            NotifyEditingIngredientTotals();
    }

    private void NotifyEditingIngredientTotals()
    {
        OnPropertyChanged(nameof(EditDraftCaloriesText));
        OnPropertyChanged(nameof(EditDraftProteinText));
        OnPropertyChanged(nameof(EditDraftCarbsText));
        OnPropertyChanged(nameof(EditDraftFatsText));
    }

    /// <summary>
    /// NM02: an incomplete or invalid amount draft blocks the commit instead of quietly resending
    /// the previous value for that ingredient.
    /// </summary>
    private bool ValidateEditingIngredients()
    {
        if (EditingIngredients.All(ingredient => ingredient.TryGetGrams(out _)))
            return true;

        ShowStatus(UiTextCatalog.Get("Nutrition_FoodLog_AmountError"));
        return false;
    }

    private FoodLogEntry BuildDraft()
    {
        return new(EditingItemId, _currentLogId, EditingMealType,
            EditingItemName.Trim(), EditingServingSize, EditingServingUnit, EditingServingsCount,
            EditingCalories, EditingProtein, EditingCarbs, EditingFat, _draftTimestamp)
        {
            Ingredients = EditingIngredients.Select(i => new FoodHistoryIngredient(i.FoodItemId, i.Name, i.Grams))
                .ToList(),
            LoggedMealId = _editingLoggedMealId
        };
    }

    [RelayCommand]
    public async Task SaveFoodItemAsync()
    {
        if (IsWriting || !IsEditModalVisible) return;
        if (string.IsNullOrWhiteSpace(EditingItemName))
        {
            ShowStatus("Please enter a food name.");
            return;
        }

        if (!ValidateEditingIngredients())
            return;

        var draft = BuildDraft();
        var date = _draftDate;
        var editing = IsEditingExisting;
        var quick = _quickDraft;
        var mealId = _editingLoggedMealId;

        // NM02: snapshot the validated portions and the meal context before the awaited write. Rows are
        // matched by row identity, so two occurrences of one food keep their own amounts, and an
        // untouched row keeps its original record instead of being resaved from a display value.
        var resolvedGrams = GroupedMealEditProjection.ResolveAmounts(
            _editingMealIngredients.Select(ingredient => ingredient.Grams).ToArray(),
            _editingMealIngredients.Select(ingredient => ingredient.FoodItemId).ToArray(),
            EditingIngredients
                .Select((row, index) => row.IsUnchanged
                    ? (IngredientPortionEdit?)null
                    : new IngredientPortionEdit(index, row.FoodItemId, row.Grams))
                .ToArray());
        var ingredientSnapshot = _editingMealIngredients
            .Select((original, index) => resolvedGrams[index] == original.Grams
                ? original
                : original with { Grams = resolvedGrams[index], Amount = resolvedGrams[index] })
            .ToArray();
        var mealType = EditingMealType;
        var mealTime = _draftTimestamp.TimeOfDay;

        await CommitFoodAsync(date, async () =>
        {
            var loggedItem = NutritionPresentationMapper.ToLoggedFood(draft);
            if (mealId is > 0)
            {
                if (_overviewUseCase is null)
                    throw new InvalidOperationException("Meal editing is unavailable.");

                var ingredientDtos = NutritionPresentationMapper.ToMealIngredients(ingredientSnapshot);
                await _overviewUseCase.UpdateMealAsync(new UpdateMealRequest(
                    mealId.Value, date, mealType.ToString(), mealTime, ingredientDtos));
            }
            else if (editing) await _dailyLogUseCase.SaveFoodItemAsync(date, loggedItem, true);
            else if (quick is not null)
                await _dailyLogUseCase.ReLogFoodAsync(date, draft.MealType,
                    NutritionPresentationMapper.ToQuickSnapshot(quick), loggedItem);
            else await _dailyLogUseCase.SaveFoodItemAsync(date, loggedItem);
        }, editing ? "Item updated." : "Item added.");
    }

    private Task CommitFoodAsync(DateOnly date, Func<Task> save, string success)
    {
        return CommitFoodAsync(date, save, () => success);
    }

    private async Task CommitFoodAsync(DateOnly date, Func<Task> save, Func<string> successMessageProvider)
    {
        if (IsWriting) return;
        IsWriting = true;
        var committed = false;
        await _dateLoadLock.WaitAsync();
        try
        {
            await save();
            committed = true;
            if (date == SelectedDate)
            {
                IsEditModalVisible = false;
                IsFastLoggingVisible = false;
                IsQuickAddCaloriesVisible = false;
                IsBarcodeScannerVisible = false;
            }

            CancelFoodSearch();
        }
        catch (ArgumentException)
        {
            ShowStatus("Check the portion and nutrition values. Use positive portions and non-negative numbers.");
        }
        catch (InvalidOperationException)
        {
            ShowStatus("Failed to save: entry could not be saved. Reverted changes. Refresh and try again.");
        }
        catch
        {
            ShowStatus(
                "Failed to save: entry could not be saved. Reverted changes. Your entry is still here; try again.");
        }
        finally { _dateLoadLock.Release(); }

        if (committed)
        {
            await LoadLogForDateAsync(SelectedDate);
            if (Breakdown.IsVisible)
                await OpenBreakdownAsync();
            var success = successMessageProvider();
            if (!string.IsNullOrWhiteSpace(success))
            {
                ShowStatus(IsLoadError ? "Saved, but totals could not refresh. Tap Retry." :
                    date == SelectedDate ? success : $"{success} ({date:MMM d})");
            }
        }

        IsWriting = false;
    }

    [RelayCommand]
    public async Task DeleteFoodItemAsync()
    {
        if (IsWriting || !CanDeleteEditingFood || !IsEditModalVisible) return;
        var id = EditingItemId;
        await CommitFoodAsync(_draftDate, () => _dailyLogUseCase.DeleteFoodItemAsync(id), "Item deleted.");
    }

    [RelayCommand]
    public async Task CopyYesterdayMealAsync(object? param)
    {
        if (param is MealType mt)
            await CopyYesterdayMealInternalAsync(mt);
        else if (param is string s && Enum.TryParse<MealType>(s, true, out var parsed))
            await CopyYesterdayMealInternalAsync(parsed);
    }

    public async Task CopyYesterdayMealInternalAsync(MealType mealType)
    {
        if (IsWriting || IsLoading) return;
        var targetDate = SelectedDate;
        var sourceDate = targetDate.AddDays(PreviousDayOffset);

        var count = 0;
        await CommitFoodAsync(targetDate, async () =>
        {
            count = await _dailyLogUseCase.CopyPreviousMealAsync(sourceDate, targetDate, mealType);
        }, () => count == 0 ? $"No foods found in yesterday's {mealType.ToString().ToLowerInvariant()}." : $"Copied {count} {(count == 1 ? "food" : "foods")} from yesterday.");
    }

    [RelayCommand]
    public async Task CopyYesterdayDiaryAsync()
    {
        if (IsWriting || IsLoading) return;
        var targetDate = SelectedDate;
        var sourceDate = targetDate.AddDays(PreviousDayOffset);

        var count = 0;
        await CommitFoodAsync(targetDate, async () =>
        {
            count = await _dailyLogUseCase.CopyEntireDayAsync(sourceDate, targetDate);
        }, () => count == 0 ? "No foods found in yesterday's diary." : $"Copied {count} {(count == 1 ? "food" : "foods")} from yesterday.");
    }

    [ObservableProperty] private DateOnly _copySourceDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
    [ObservableProperty] private bool _copyOverwriteMode;
    [ObservableProperty] private TimeSpan _editingConsumptionTime = DateTime.Now.TimeOfDay;
    [ObservableProperty] private bool _waterReminderOptedIn;
    [ObservableProperty] private TimeOnly _waterReminderQuietStart = new(22, 0);
    [ObservableProperty] private TimeOnly _waterReminderQuietEnd = new(7, 0);

    public ObservableCollection<int> WaterPresetVolumesMl { get; }

    public ObservableCollection<FoodLogEntry> TimelineEntries { get; } = [];
    public Dictionary<MealType, double> PerMealCalorieTargets { get; } = new();

    [RelayCommand]
    public async Task CopyFromSelectedSourceDateAsync(MealType? mealType = null)
    {
        if (IsWriting || IsLoading) return;
        var targetDate = SelectedDate;
        var sourceDate = CopySourceDate;
        var count = 0;
        await CommitFoodAsync(targetDate, async () =>
        {
            count = mealType.HasValue
                ? await _dailyLogUseCase.CopyPreviousMealAsync(sourceDate, targetDate, mealType.Value)
                : await _dailyLogUseCase.CopyEntireDayAsync(sourceDate, targetDate);
        }, () => count == 0 ? $"No foods found on {sourceDate:MMM d}." : $"Copied {count} {(count == 1 ? "food" : "foods")} from {sourceDate:MMM d}.");
    }

    public void SetMealCalorieTarget(MealType mealType, double targetKcal)
    {
        if (double.IsFinite(targetKcal) && targetKcal >= 0)
            PerMealCalorieTargets[mealType] = Math.Round(targetKcal, 0);
    }

    [RelayCommand]
    public async Task RetryWaterSaveAsync()
    {
        var date = SelectedDate;
        var target = _pendingWaterIntakeMl ?? WaterIntakeMl;
        await WriteWaterAsync(date, target);
    }

    private async Task ChangeWaterAsync(int deltaMl)
    {
        var date = SelectedDate;
        await _waterWriteLock.WaitAsync();
        try
        {
            if (date != SelectedDate)
                return;

            var target = Math.Max(0, WaterIntakeMl + deltaMl);
            if (target == WaterIntakeMl)
                return;

            await PersistWaterAsync(date, target);
        }
        finally
        {
            _waterWriteLock.Release();
        }
    }

    private async Task WriteWaterAsync(DateOnly date, int target)
    {
        await _waterWriteLock.WaitAsync();
        try
        {
            if (date != SelectedDate)
                return;

            await PersistWaterAsync(date, target);
        }
        finally
        {
            _waterWriteLock.Release();
        }
    }

    private async Task PersistWaterAsync(DateOnly date, int target)
    {
        var previous = _confirmedWaterIntakeMl;
        WaterIntakeMl = target;
        WaterSaveError = string.Empty;
        IsSavingWater = true;
        try
        {
            await _dailyLogUseCase.UpdateWaterIntakeAsync(date, target);
            if (date != SelectedDate)
                return;

            _confirmedWaterIntakeMl = target;
            _pendingWaterIntakeMl = null;
            WaterIntakeMl = target;
        }
        catch
        {
            if (date != SelectedDate)
                return;

            WaterIntakeMl = previous;
            _pendingWaterIntakeMl = target;
            WaterSaveError = UiTextCatalog.Get("Nutrition_FoodLog_WaterSaveFailed");
        }
        finally
        {
            IsSavingWater = false;
        }
    }

    [RelayCommand]
    public async Task NavigateBackAsync()
    {
        if (_navigateBackAsync is not null)
            await _navigateBackAsync();
    }

    private async Task LoadLogForDateAsync(DateOnly date)
    {
        var version = Interlocked.Increment(ref _dateLoadVersion);
        await _dateLoadLock.WaitAsync();
        try
        {
            if (version != _dateLoadVersion)
                return;
            IsLoading = true;
            IsLoadError = false;
            var log = await _dailyLogUseCase.GetLogForDateAsync(date);
            if (version != _dateLoadVersion || date != SelectedDate)
                return;

            _currentLogId = log.Id;
            TargetCalories = log.TargetCalories > 0 ? log.TargetCalories : 2000;
            TargetProtein = log.TargetProtein > 0 ? log.TargetProtein : 130;
            TargetCarbs = log.TargetCarbs > 0 ? log.TargetCarbs : 220;
            TargetFat = log.TargetFat > 0 ? log.TargetFat : 65;
            if (_hydrationDay is null)
                WaterIntakeMl = log.WaterIntakeMl;
            _confirmedWaterIntakeMl = log.WaterIntakeMl;
            _pendingWaterIntakeMl = null;
            WaterSaveError = string.Empty;
            NotesText = log.Notes ?? string.Empty;

            BreakfastSection.Items.Clear();
            LunchSection.Items.Clear();
            DinnerSection.Items.Clear();
            SnackSection.Items.Clear();

            foreach (var item in log.Entries)
            {
                var section = GetSectionForMealType(item.MealType);
                section.Items.Add(NutritionPresentationMapper.FromLoggedFood(item));
            }

            foreach (var section in MealSections)
                section.RefreshSummary((float)TargetCalories);

            TotalCalories = log.TotalCalories;
            TotalProtein = log.TotalProtein;
            TotalCarbs = log.TotalCarbs;
            TotalFat = log.TotalFat;
            await LoadHydrationAsync(date);
        }
        catch
        {
            if (version == _dateLoadVersion)
            {
                IsLoadError = true;
                foreach (var section in MealSections)
                {
                    section.Items.Clear();
                    section.RefreshSummary();
                }

                TotalCalories = TotalProtein = TotalCarbs = TotalFat = 0;
                ShowStatus("Could not load this day. Tap Retry.");
            }
        }
        finally
        {
            if (version == _dateLoadVersion)
                IsLoading = false;
            _dateLoadLock.Release();
        }
    }

    private MealSectionUiGroup GetSectionForMealType(MealType mealType)
    {
        return mealType switch
        {
            MealType.Breakfast => BreakfastSection,
            MealType.Lunch => LunchSection,
            MealType.Dinner => DinnerSection,
            _ => SnackSection
        };
    }

    private void RecalculateTotalsFromUi()
    {
        var allItems = MealSections.SelectMany(s => s.Items).ToList();
        TotalCalories = Math.Round(allItems.Sum(i => i.Calories), 1);
        TotalProtein = Math.Round(allItems.Sum(i => i.Protein), 1);
        TotalCarbs = Math.Round(allItems.Sum(i => i.Carbs), 1);
        TotalFat = Math.Round(allItems.Sum(i => i.Fat), 1);
    }

    private void ShowStatus(string message)
    {
        StatusMessage = message;
        IsStatusVisible = true;
        _ = HideStatusAfterDelayAsync(message);
    }

    private async Task HideStatusAfterDelayAsync(string message)
    {
        await Task.Delay(3000);
        if (StatusMessage == message)
            IsStatusVisible = false;
    }
}
