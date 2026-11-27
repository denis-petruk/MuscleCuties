using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Nutrition;
using MuscleCuties.UI.Services.Nutrition;
using MuscleCuties.UseCases.Features.Hydration.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.Barcode;
using MuscleCuties.UseCases.Features.Nutrition.DailyLog;
using MuscleCuties.UseCases.Features.Nutrition.Overview;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.Recipes;
using MuscleCuties.UseCases.Features.Nutrition.Recipes.Contracts;

namespace MuscleCuties.UI.ViewModels.Nutrition;

/// <summary>The diary for one day: the meal sections and their entries, the date strip, notes and water, plus the</summary>
/// editor that adds or changes one item.</summary>
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
    // write rolls the visible total back instead of silently claiming the amount was saved.
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPlainFoodEditor))]
    private bool _isGroupedEditor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChangeHydration))]
    private bool _isLoadError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChangeHydration))]
    private bool _isLoading;

    [ObservableProperty] private bool _isStatusVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChangeHydration), nameof(CanSaveNotes))]
    private bool _isWriting;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveNotes))]
    private string _notesText = string.Empty;

    private string _savedNotes = string.Empty;

    /// <summary>Notes can be saved once they differ from what is stored, so the save action is not offered for an</summary>
    /// unchanged day.</summary>
    public bool CanSaveNotes => !IsWriting &&
                                !string.Equals(NotesText, _savedNotes, StringComparison.Ordinal);

    [ObservableProperty] private DateTime _pickerDate;

    private QuickFood? _quickDraft;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedSelectedDate), nameof(IsSelectedDateToday))]
    private DateOnly _selectedDate;

    [ObservableProperty] private string _statusMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CalorieProgressRatio), nameof(CalorieRemainingText))]
    private double _targetCalories = 2000;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CarbsProgressRatio))]
    private double _targetCarbs = 220;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FatProgressRatio))]
    private double _targetFat = 65;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProteinProgressRatio))]
    private double _targetProtein = 130;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CalorieProgressRatio), nameof(CalorieRemainingText))]
    private double _totalCalories;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CarbsProgressRatio))]
    private double _totalCarbs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FatProgressRatio))]
    private double _totalFat;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProteinProgressRatio))]
    private double _totalProtein;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WaterSummaryAccessibility))]
    private int _waterIntakeMl;

    /// <summary>The water total as it is read aloud, with its unit and not just the number.</summary>
    public string WaterSummaryAccessibility =>
        UiTextCatalog.Format("Nutrition_Hydration_SummaryAccessibilityFormat", WaterIntakeMl);

    /// <summary>The meal a new entry most likely belongs to: from the clock for today, and breakfast for any other day,</summary>
    /// because there is no time of day to infer from.</summary>
    public MealType SuggestedAddMealType => IsSelectedDateToday
        ? DateTime.Now.Hour switch
        {
            < 11 => MealType.Breakfast,
            < 15 => MealType.Lunch,
            < 20 => MealType.Dinner,
            _ => MealType.Snack
        }
        : MealType.Breakfast;


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWaterSaveError))]
    private string _waterSaveError = string.Empty;

    /// <summary>True when saving the water entry failed.</summary>
    public bool HasWaterSaveError => !string.IsNullOrWhiteSpace(WaterSaveError);

    /// <summary>Takes the use cases and the callbacks the diary reports to.</summary>
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

    /// <summary>True when the editor holds a single food.</summary>
    public bool IsPlainFoodEditor => !IsGroupedEditor;
    /// <summary>True when the edited food can be deleted.</summary>
    public bool CanDeleteEditingFood => IsEditingExisting && !IsMealContextLocked;
    /// <summary>Which meal and what time the draft is being written for, for the locked context row.</summary>
    public string MealContextSummary =>
        $"{UiTextCatalog.Get($"Nutrition_MealType_{EditingMealType}")} · {DisplayFormat.Time(_draftTimestamp)}";
    /// <summary>The sheet's heading, which tells editing apart from logging and names a whole meal when the editor was</summary>
    /// opened from the meal itself.</summary>
    public string EditModalTitle => IsEditingExisting
        ? (IsMealContextLocked
            ? UiTextCatalog.Get("Nutrition_Breakdown_EditMeal")
            : UiTextCatalog.Get("Nutrition_FoodLog_EditEntryTitle"))
        : UiTextCatalog.Get("Nutrition_FoodLog_LogEntryTitle");

    /// <summary>Draft totals for the meal being edited, so the sheet can open on what it contains.</summary>
    public string EditDraftCaloriesText => DisplayFormat.Calories(EditingIngredients.Sum(i => (double)i.MacrosForDraft.Calories));
    /// <summary>The draft's protein, summed over its ingredient editors as they are changed.</summary>
    public string EditDraftProteinText => DisplayFormat.Grams(EditingIngredients.Sum(i => (double)i.MacrosForDraft.Protein));
    /// <summary>The draft's carbohydrate, summed the same way.</summary>
    public string EditDraftCarbsText => DisplayFormat.Grams(EditingIngredients.Sum(i => (double)i.MacrosForDraft.Carbs));
    /// <summary>The draft's fat, summed the same way.</summary>
    public string EditDraftFatsText => DisplayFormat.Grams(EditingIngredients.Sum(i => (double)i.MacrosForDraft.Fats));
    /// <summary>The ingredients of the food being edited.</summary>
    public ObservableCollection<HistoryIngredientEditor> EditingIngredients { get; } = [];
    /// <summary>The meals a food can be logged in.</summary>
    public IReadOnlyList<MealType> MealCategories { get; } = Enum.GetValues<MealType>();

    /// <summary>Days shown either side of the selected day, so the seven cells fit one row.</summary>
    private const int DaysAroundSelectedDate = 3;

    /// <summary>The days shown in the date strip.</summary>
    public ObservableCollection<DateStripDayItem> DateStripDays { get; } = [];

    /// <summary>The breakfast group of the timeline.</summary>
    public MealSectionUiGroup BreakfastSection { get; } = new(MealType.Breakfast, "Breakfast");
    /// <summary>The lunch group of the timeline.</summary>
    public MealSectionUiGroup LunchSection { get; } = new(MealType.Lunch, "Lunch");
    /// <summary>The dinner group of the timeline.</summary>
    public MealSectionUiGroup DinnerSection { get; } = new(MealType.Dinner, "Dinner");
    /// <summary>The snack group of the timeline.</summary>
    public MealSectionUiGroup SnackSection { get; } = new(MealType.Snack, "Snacks");

    /// <summary>The meal groups in timeline order.</summary>
    public ObservableCollection<MealSectionUiGroup> MealSections { get; } = [];

    /// <summary>Calories as a 0–1 fraction of the target, capped so an overshoot does not draw past the end of the ring.</summary>
    public double CalorieProgressRatio =>
        TargetCalories > 0 ? Math.Clamp(TotalCalories / TargetCalories, 0.0, 1.0) : 0.0;
    /// <summary>The day's calories as a whole number.</summary>
    public string TotalCaloriesText => DisplayFormat.WholeNumber(TotalCalories);

    /// <summary>Protein as a capped fraction of its target.</summary>
    public double ProteinProgressRatio => TargetProtein > 0 ? Math.Clamp(TotalProtein / TargetProtein, 0.0, 1.0) : 0.0;
    /// <summary>Carbohydrate as a capped fraction of its target.</summary>
    public double CarbsProgressRatio => TargetCarbs > 0 ? Math.Clamp(TotalCarbs / TargetCarbs, 0.0, 1.0) : 0.0;
    /// <summary>Fat as a capped fraction of its target.</summary>
    public double FatProgressRatio => TargetFat > 0 ? Math.Clamp(TotalFat / TargetFat, 0.0, 1.0) : 0.0;

    public string CalorieRemainingText
    {
        get
        {
            var diff = TargetCalories - TotalCalories;
            var amount = DisplayFormat.Calories(Math.Abs(Math.Round(diff, 0)));
            return diff >= 0
                ? UiTextCatalog.Format("Nutrition_FoodLog_CaloriesRemaining", amount)
                : UiTextCatalog.Format("Nutrition_FoodLog_CaloriesOver", amount);
        }
    }

    /// <summary>Whether the diary is showing today, which decides whether a new entry defaults to the clock's meal or to</summary>
    /// breakfast.</summary>
    public bool IsSelectedDateToday => SelectedDate == DateOnly.FromDateTime(DateTime.Today);

    public string FormattedSelectedDate
    {
        get
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var prefix = SelectedDate == today
                ? UiTextCatalog.Get("Common_Today")
                : SelectedDate == today.AddDays(-1)
                    ? UiTextCatalog.Get("Common_Yesterday")
                    : SelectedDate == today.AddDays(1)
                        ? UiTextCatalog.Get("Common_Tomorrow")
                        : DisplayFormat.DayMonthWithWeekday(SelectedDate);

            return SelectedDate == today || SelectedDate == today.AddDays(-1) || SelectedDate == today.AddDays(1)
                ? UiTextCatalog.Format("Common_Format_RelativeDate", prefix, DisplayFormat.DayMonth(SelectedDate))
                : prefix;
        }
    }

    /// <summary>Releases the view model's subscriptions.</summary>
    public void Dispose()
    {
        _isActive = false;
        ResetHydration();
        Breakdown.Clear();
        _barcodeScan?.Dispose();
        _dateLoadLock.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Reloads the diary when the nutrition data changes.</summary>
    private void OnNutritionChanged(object? sender, NutritionChangedEventArgs e)
    {
        if (_isActive && !IsWriting && e.Date == SelectedDate)
            _ = LoadLogForDateAsync(SelectedDate);
    }

    /// <summary>Starts listening for changes elsewhere in the app and loads the diary the page opens on.</summary>
    public async Task InitializeAsync()
    {
        if (!_isActive && _changeNotifier is not null)
            _changeNotifier.NutritionChanged += OnNutritionChanged;
        _isActive = true;
        // The hydration scope is created on activation, not in the constructor: a page can be
        // reactivated after Deactivate, and LoadHydrationAsync silently no-ops without a scope.
        if (_hydrationScope is null)
            ResetHydration();
        await LoadLogForDateAsync(SelectedDate);
    }

    /// <summary>Stops listening, drops hydration state and forgets the day, so a closed diary is not reloaded by another</summary>
    /// screen's changes.</summary>
    public void Deactivate()
    {
        _isActive = false;
        ResetHydration();
        Breakdown.Clear();
        CloseEditModal();
        if (_changeNotifier is not null) _changeNotifier.NutritionChanged -= OnNutritionChanged;
        CancelFoodSearch();
    }

    /// <summary>Retries the failed load.</summary>
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

    /// <summary>Loads the newly selected day.</summary>
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
        _savedNotes = string.Empty;
        NotesText = string.Empty;
    }

    /// <summary>Opens one logged item for editing, and reports on the page when it cannot be read.</summary>
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

    /// <summary>Builds the surrounding days, marking today so the strip has an anchor.</summary>
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

    /// <summary>Selects a day from the strip.</summary>
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

    /// <summary>Moves the log to today and reloads that day's entries.</summary>
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

    /// <summary>Selects the previous day.</summary>
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

    /// <summary>Selects the next day.</summary>
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

    /// <summary>Shows or hides the date picker.</summary>
    [RelayCommand]
    public void ToggleDatePicker()
    {
        IsDatePickerOpen = !IsDatePickerOpen;
    }

    /// <summary>Selects the day the picker returned.</summary>
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

    /// <summary>Opens the editor for a new food in the meal.</summary>
    [RelayCommand]
    public Task OpenAddFoodAsync(MealType mealType) => OpenAddFoodCoreAsync(mealType, null);

    /// <summary>Opens an empty entry for a meal, optionally at a given minute, and refuses while the day is loading,</summary>
    /// writing or in error because the entry would target unknown data.</summary>
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

    /// <summary>Opens the editor for an existing entry.</summary>
    [RelayCommand]
    public Task OpenEditFoodAsync(FoodLogEntry item) => OpenEditFoodCoreAsync(item, false);

    /// <summary>Opens the meal's entries for editing.</summary>
    [RelayCommand]
    private async Task EditMealAsync(MealSectionUiGroup? section)
    {
        if (section?.CanEditMeal == true && section.Items.Count == 1)
            await OpenEditFoodCoreAsync(section.Items[0], true);
    }

    /// <summary>Opens an item for editing, locking the meal and time when the editor was reached from that meal's own</summary>
    /// sheet.</summary>
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

    /// <summary>Closes the food editor.</summary>
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

    /// <summary>Refreshes the editing context when the lock changes.</summary>
    partial void OnIsMealContextLockedChanged(bool value) =>
        NotifyEditingContextProperties();

    /// <summary>Refreshes the editing flags when the mode changes.</summary>
    partial void OnIsEditingExistingChanged(bool value) =>
        NotifyEditingContextProperties();

    /// <summary>Refreshes the editing flags when the meal changes.</summary>
    partial void OnEditingMealTypeChanged(MealType value) =>
        OnPropertyChanged(nameof(MealContextSummary));

    /// <summary>Raises the editing properties the page binds.</summary>
    private void NotifyEditingContextProperties()
    {
        OnPropertyChanged(nameof(CanDeleteEditingFood));
        OnPropertyChanged(nameof(MealContextSummary));
        OnPropertyChanged(nameof(EditModalTitle));
    }

    /// <summary>Fills the ingredient editors from a stored meal and starts tracking their totals.</summary>
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

    /// <summary>Subscribes to each ingredient editor so a changed weight updates the draft's totals.</summary>
    private void TrackEditingIngredientTotals()
    {
        foreach (var editor in EditingIngredients)
        {
            editor.PropertyChanged -= OnEditingIngredientChanged;
            editor.PropertyChanged += OnEditingIngredientChanged;
        }

        NotifyEditingIngredientTotals();
    }

    /// <summary>Recalculates the editing totals when a field changes.</summary>
    private void OnEditingIngredientChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HistoryIngredientEditor.GramsText))
            NotifyEditingIngredientTotals();
    }

    /// <summary>Re-publishes the four draft totals after an ingredient changed.</summary>
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

    /// <summary>The entry the editor would save: its identity, context and the values currently entered.</summary>
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

    /// <summary>Saves the food entry.</summary>
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

    /// <summary>Saves an entry with a fixed success message.</summary>
    private Task CommitFoodAsync(DateOnly date, Func<Task> save, string success)
    {
        return CommitFoodAsync(date, save, () => success);
    }

    /// <summary>The one write path for an entry: refuses a second concurrent write, reloads the day afterwards and reports</summary>
    /// a failure without losing the draft.</summary>
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

    /// <summary>Deletes the food entry.</summary>
    [RelayCommand]
    public async Task DeleteFoodItemAsync()
    {
        if (IsWriting || !CanDeleteEditingFood || !IsEditModalVisible) return;
        var confirmed = await ConfirmAsync(
            UiTextCatalog.Get("Nutrition_FoodLog_DeleteItemTitle"),
            UiTextCatalog.Get("Nutrition_FoodLog_DeleteItemMessage"),
            UiTextCatalog.Get("Common_Delete"),
            UiTextCatalog.Get("Common_Cancel"));
        if (!confirmed) return;
        var id = EditingItemId;
        await CommitFoodAsync(_draftDate, () => _dailyLogUseCase.DeleteFoodItemAsync(id), "Item deleted.");
    }

    /// <summary>Copies yesterday's entries into today.</summary>
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

    /// <summary>The volumes the water buttons offer.</summary>
    public ObservableCollection<int> WaterPresetVolumesMl { get; }

    /// <summary>Returns to the previous page.</summary>
    [RelayCommand]
    public async Task NavigateBackAsync()
    {
        if (_navigateBackAsync is not null)
            await _navigateBackAsync();
    }

    /// <summary>Loads one day under a lock and a version, so switching dates quickly cannot leave the older day's entries</summary>
    /// on screen.</summary>
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
            WaterSaveError = string.Empty;
            _savedNotes = log.Notes ?? string.Empty;
            NotesText = _savedNotes;

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

    /// <summary>The section a meal type belongs to.</summary>
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

    /// <summary>Shows a transient status line from the diary's own actions.</summary>
    private void ShowStatus(string message)
    {
        StatusMessage = message;
        IsStatusVisible = true;
        _ = HideStatusAfterDelayAsync(message);
    }

    /// <summary>Clears the status line after its delay, but only if it still shows the message this call was given:</summary>
    /// a newer status must not be wiped by an older timer.</summary>
    private async Task HideStatusAfterDelayAsync(string message)
    {
        await Task.Delay(3000);
        if (StatusMessage == message)
            IsStatusVisible = false;
    }
}
