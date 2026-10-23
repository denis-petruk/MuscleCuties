using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Nutrition;
using MuscleCuties.UI.Models.UI.Nutrition;
using MuscleCuties.UI.Services.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Barcode;
using MuscleCuties.UseCases.Features.Nutrition.DailyLog;
using MuscleCuties.UseCases.Features.Nutrition.Recipes;
using MuscleCuties.UseCases.Features.Nutrition.Recipes.Contracts;

namespace MuscleCuties.UI.ViewModels.Nutrition;

public partial class FoodLogViewModel : ObservableObject, IDisposable
{
    private const int PreviousDayOffset = -1;
    private readonly ILookupBarcodeUseCase? _barcodeUseCase;
    private readonly IBarcodeScannerHardwareService? _barcodeHardwareService;
    private readonly ISaveMealTemplateUseCase? _saveMealTemplateUseCase;
    private readonly IGetRecipesAndTemplatesUseCase? _getRecipesAndTemplatesUseCase;
    private readonly ICreateRecipeUseCase? _createRecipeUseCase;
    private readonly INutritionChangeNotifier? _changeNotifier;
    private readonly INutritionDailyLogUseCase _dailyLogUseCase;
    private readonly SemaphoreSlim _dateLoadLock = new(1, 1);
    private readonly Func<Task>? _navigateBackAsync;
    private Guid _currentLogId;
    private int _dateLoadVersion;

    private DateOnly _draftDate;
    private DateTime _draftTimestamp;

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

    // Modal state
    [ObservableProperty] private bool _isEditModalVisible;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsPlainFoodEditor))]
    private bool _isGroupedEditor;

    [ObservableProperty] private bool _isLoadError;

    [ObservableProperty] private bool _isLoading;

    [ObservableProperty] private bool _isStatusVisible;

    [ObservableProperty] private bool _isWriting;

    [ObservableProperty] private string _notesText = string.Empty;

    [ObservableProperty] private DateTime _pickerDate;

    private QuickFoodItemDto? _quickDraft;

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

    public FoodLogViewModel(
        INutritionDailyLogUseCase dailyLogUseCase,
        Func<Task>? navigateBackAsync = null,
        INutritionChangeNotifier? changeNotifier = null,
        ILookupBarcodeUseCase? barcodeUseCase = null,
        IBarcodeScannerHardwareService? barcodeHardwareService = null,
        ISaveMealTemplateUseCase? saveMealTemplateUseCase = null,
        IGetRecipesAndTemplatesUseCase? getRecipesAndTemplatesUseCase = null,
        ICreateRecipeUseCase? createRecipeUseCase = null)
    {
        _dailyLogUseCase = dailyLogUseCase;
        _navigateBackAsync = navigateBackAsync;
        _changeNotifier = changeNotifier;
        _barcodeUseCase = barcodeUseCase;
        _barcodeHardwareService = barcodeHardwareService;
        _saveMealTemplateUseCase = saveMealTemplateUseCase;
        _getRecipesAndTemplatesUseCase = getRecipesAndTemplatesUseCase;
        _createRecipeUseCase = createRecipeUseCase;


        MealSections.Add(BreakfastSection);
        MealSections.Add(LunchSection);
        MealSections.Add(DinnerSection);
        MealSections.Add(SnackSection);

        var today = DateOnly.FromDateTime(DateTime.Today);
        _selectedDate = today;
        _pickerDate = DateTime.Today;

        PopulateDateStrip(today);
    }

    public bool IsPlainFoodEditor => !IsGroupedEditor;
    public ObservableCollection<HistoryIngredientEditor> EditingIngredients { get; } = [];
    public IReadOnlyList<MealType> MealCategories { get; } = Enum.GetValues<MealType>();

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
                ? $"{Math.Round(diff, 0):F0} kcal remaining"
                : $"{Math.Abs(Math.Round(diff, 0)):F0} kcal over";
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
        CloseEditModal();
        if (_changeNotifier is not null) _changeNotifier.NutritionChanged -= OnNutritionChanged;
        CancelFoodSearch();
    }

    [RelayCommand]
    private Task RetryLoadAsync()
    {
        return LoadLogForDateAsync(SelectedDate);
    }

    partial void OnSelectedDateChanged(DateOnly value)
    {
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
            OpenEditFood(ToFoodItemEntryDto(item));
        }
        catch { ShowStatus("Could not open this entry. Refresh and try again."); }
    }

    private void PopulateDateStrip(DateOnly centerDate)
    {
        DateStripDays.Clear();
        var today = DateOnly.FromDateTime(DateTime.Today);

        // 14-day rolling window centered around today/selected
        for (var offset = -7; offset <= 7; offset++)
        {
            var dayNumber = centerDate.DayNumber + offset;
            if (dayNumber < DateOnly.MinValue.DayNumber || dayNumber > DateOnly.MaxValue.DayNumber)
                continue;
            var date = DateOnly.FromDayNumber(dayNumber);
            var item = new DateStripDayItem(date, date == today)
            {
                IsSelected = date == SelectedDate
            };
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
    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "RelayCommand for ViewModel binding.")]
    public void ToggleMealSection(MealSectionUiGroup section)
    {
        section.IsExpanded = !section.IsExpanded;
    }

    [RelayCommand]
    public async Task OpenAddFoodAsync(MealType mealType)
    {
        if (IsLoading || IsWriting || IsLoadError)
            return;
        _draftDate = SelectedDate;
        _draftTimestamp = SelectedDate.ToDateTime(TimeOnly.FromDateTime(DateTime.Now));
        _quickDraft = null;
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

    // Kept for callers that pass the meal category as text.
    public void OpenAddFood(string mealTypeStr)
    {
        if (!Enum.TryParse<MealType>(mealTypeStr, true, out var mealType))
            mealType = MealType.Breakfast;
        _ = OpenAddFoodAsync(mealType);
    }

    [RelayCommand]
    public void OpenEditFood(FoodItemEntryDto item)
    {
        if (IsLoading || IsWriting || IsLoadError)
            return;
        _draftDate = item.LoggedAtTimestamp != default ? DateOnly.FromDateTime(item.LoggedAtTimestamp) : SelectedDate;
        _draftTimestamp = item.LoggedAtTimestamp;
        _quickDraft = null;
        SetIngredients(item.Ingredients);
        IsFastLoggingVisible = false;
        IsQuickAddCaloriesVisible = false;
        IsBarcodeScannerVisible = false;
        IsEditingExisting = true;
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
        CancelFoodSearch();
    }

    private void SetIngredients(IReadOnlyList<HistoryIngredientDto> ingredients)
    {
        EditingIngredients.Clear();
        foreach (var ingredient in ingredients)
            EditingIngredients.Add(new HistoryIngredientEditor(ingredient));
        IsGroupedEditor = ingredients.Count > 0;
    }

    private FoodItemEntryDto BuildDraft()
    {
        return new(EditingItemId, _currentLogId, EditingMealType,
            EditingItemName.Trim(), EditingServingSize, EditingServingUnit, EditingServingsCount,
            EditingCalories, EditingProtein, EditingCarbs, EditingFat, _draftTimestamp)
        {
            Ingredients = EditingIngredients.Select(i => new HistoryIngredientDto(i.FoodItemId, i.Name, i.Grams))
                .ToList()
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

        var draft = BuildDraft();
        var date = _draftDate;
        var editing = IsEditingExisting;
        var quick = _quickDraft;
        await CommitFoodAsync(date, async () =>
        {
            var loggedItem = ToLoggedFoodItemDto(draft);
            if (editing) await _dailyLogUseCase.SaveFoodItemAsync(date, loggedItem, true);
            else if (quick is not null)
                await _dailyLogUseCase.ReLogFoodAsync(date, draft.MealType, ToQuickSnapshot(quick), loggedItem);
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
        if (IsWriting || !IsEditingExisting || !IsEditModalVisible) return;
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

    public ObservableCollection<int> WaterPresetVolumesMl { get; } =
    [
        MuscleCuties.Core.Domain.Hydration.DefaultWaterPresets.SmallMillilitres,
        MuscleCuties.Core.Domain.Hydration.DefaultWaterPresets.MediumMillilitres,
        MuscleCuties.Core.Domain.Hydration.DefaultWaterPresets.LargeMillilitres
    ];

    public ObservableCollection<FoodItemEntryDto> TimelineEntries { get; } = [];
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
    public async Task AddWaterAsync(int amountMl)
    {
        WaterIntakeMl = Math.Max(0, WaterIntakeMl + amountMl);
        try
        {
            await _dailyLogUseCase.UpdateWaterIntakeAsync(SelectedDate, WaterIntakeMl);
        }
        catch
        {
            // Silent fallback
        }
    }

    [RelayCommand]
    public async Task RemoveWaterAsync(int amountMl)
    {
        WaterIntakeMl = Math.Max(0, WaterIntakeMl - amountMl);
        try
        {
            await _dailyLogUseCase.UpdateWaterIntakeAsync(SelectedDate, WaterIntakeMl);
        }
        catch
        {
            // Silent fallback
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
            WaterIntakeMl = log.WaterIntakeMl;
            NotesText = log.Notes ?? string.Empty;

            BreakfastSection.Items.Clear();
            LunchSection.Items.Clear();
            DinnerSection.Items.Clear();
            SnackSection.Items.Clear();

            foreach (var item in log.Entries)
            {
                var section = GetSectionForMealType(item.MealType);
                section.Items.Add(ToFoodItemEntryDto(item));
            }

            foreach (var section in MealSections)
                section.RefreshSummary();

            TotalCalories = log.TotalCalories;
            TotalProtein = log.TotalProtein;
            TotalCarbs = log.TotalCarbs;
            TotalFat = log.TotalFat;
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

    private static FoodItemEntryDto ToFoodItemEntryDto(LoggedFoodItemDto item)
    {
        return new FoodItemEntryDto(item.Id, item.DailyLogId, item.MealType, item.Name, item.ServingSize,
            item.ServingUnit,
            item.ServingsCount, item.Calories, item.Protein, item.Carbs, item.Fat, item.LoggedAtTimestamp)
        {
            Ingredients =
                item.Ingredients.Select(i => new HistoryIngredientDto(i.FoodItemId, i.Name, i.Grams)).ToList(),
            AcceptedSuggestionName = item.AcceptedSuggestionName,
            AcceptedSuggestionPhase = item.AcceptedSuggestionPhase
        };
    }

    private static LoggedFoodItemDto ToLoggedFoodItemDto(FoodItemEntryDto item)
    {
        return new LoggedFoodItemDto(item.Id, item.DailyLogId, item.MealType, item.Name, item.ServingSize,
            item.ServingUnit,
            item.ServingsCount, item.Calories, item.Protein, item.Carbs, item.Fat, item.LoggedAtTimestamp,
            item.Ingredients.Select(i => new LoggedIngredientDto(i.FoodItemId, i.Name, i.Grams)).ToList(),
            item.AcceptedSuggestionName, item.AcceptedSuggestionPhase);
    }

    private static QuickFoodItemSnapshot ToQuickSnapshot(QuickFoodItemDto item)
    {
        return new QuickFoodItemSnapshot(item.Name, item.ServingSize, item.ServingUnit, item.ServingsCount,
            item.Calories, item.Protein,
            item.Carbs, item.Fat, item.Detail, item.SourceId, item.IngredientFoodItemId,
            item.Ingredients.Select(i => new LoggedIngredientDto(i.FoodItemId, i.Name, i.Grams)).ToList());
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
