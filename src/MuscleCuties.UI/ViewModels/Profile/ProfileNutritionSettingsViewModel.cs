using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.UI.Models.UI.Common;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UI.ViewModels.Common;
using MuscleCuties.UseCases.Features.Profile.NutritionSettings;

namespace MuscleCuties.UI.ViewModels.Profile;

/// <summary>
///     Nutrition settings state: dietary preferences, per-meal targets, and unsaved-change tracking.
/// </summary>
public partial class ProfileNutritionSettingsViewModel : ObservableObject, IPageLoadAware
{
    private const float MinCaloriesKcal = 800f;
    private const float MaxCaloriesKcal = 8000f;
    private const float MinProteinGrams = 20f;
    private const float MaxProteinGrams = 500f;
    private const float MinCarbsGrams = 0f;
    private const float MaxCarbsGrams = 1000f;
    private const float MinFatsGrams = 15f;
    private const float MaxFatsGrams = 300f;

    private readonly Func<Task> _navigateBackAsync;
    private readonly Func<Task> _navigateAfterSaveAsync;
    private readonly IProfileRefreshNotifier _refreshNotifier;
    private readonly IProfileNutritionSettingsUseCase _useCase;
    private bool _hasLoadedOnce;
    private string _initialSnapshot = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AdvancedSettingsButtonText))]
    private bool _areAdvancedSettingsVisible;
    [ObservableProperty] private string _calciumGoal = string.Empty;
    [ObservableProperty] private string _caloriesGoal = string.Empty;
    [ObservableProperty] private string _carbsGoal = string.Empty;
    [ObservableProperty] private string _fatsGoal = string.Empty;
    [ObservableProperty] private string _fiberGoal = string.Empty;
    [ObservableProperty] private string _folateGoal = string.Empty;

    [ObservableProperty] private UserGoal _goal;
    private bool _hasSavedCustomGoals;
    [ObservableProperty] private string _ironGoal = string.Empty;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsPageLoading))]
    private bool _isBusy;
    [ObservableProperty] private bool _isLoadError;
    [ObservableProperty] private bool _isStatusError;
    [ObservableProperty] private bool _isGlutenFree;
    [ObservableProperty] private bool _isLactoseFree;
    private bool _isSyncingTargets;
    [ObservableProperty] private bool _isVegan;
    [ObservableProperty] private bool _isVegetarian;
    private NutritionSettingsProfileDto? _loadedProfile;
    [ObservableProperty] private string _magnesiumGoal = string.Empty;
    [ObservableProperty] private string _potassiumGoal = string.Empty;
    [ObservableProperty] private string _proteinGoal = string.Empty;
    [ObservableProperty] private SelectionOption<UserGoal>? _selectedGoalOption;
    [ObservableProperty] private SelectionOption<WeightGoalPace>? _selectedWeightGoalPaceOption;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _vitaminAGoal = string.Empty;
    [ObservableProperty] private string _vitaminB12Goal = string.Empty;
    [ObservableProperty] private string _vitaminB6Goal = string.Empty;
    [ObservableProperty] private string _vitaminCGoal = string.Empty;
    [ObservableProperty] private string _vitaminDGoal = string.Empty;
    [ObservableProperty] private string _waterGoal = string.Empty;
    [ObservableProperty] private WeightGoalPace _weightGoalPace;
    [ObservableProperty] private string _zincGoal = string.Empty;

    /// <summary>Takes the settings use case and the navigation callbacks.</summary>
    public ProfileNutritionSettingsViewModel(
        IProfileNutritionSettingsUseCase useCase,
        IProfileRefreshNotifier refreshNotifier,
        Func<Task> navigateBackAsync,
        Func<Task> navigateAfterSaveAsync)
    {
        _useCase = useCase;
        _refreshNotifier = refreshNotifier;
        _navigateBackAsync = navigateBackAsync;
        _navigateAfterSaveAsync = navigateAfterSaveAsync;
        BackCommand = new AsyncRelayCommand(BackAsync);
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        ResetToCalculatedCommand = new RelayCommand(ResetToCalculated);
        ToggleVegetarianCommand = new RelayCommand(ToggleVegetarian);
        ToggleVeganCommand = new RelayCommand(ToggleVegan);
        ToggleGlutenFreeCommand = new RelayCommand(ToggleGlutenFree);
        ToggleLactoseFreeCommand = new RelayCommand(ToggleLactoseFree);
        ToggleAdvancedSettingsCommand = new RelayCommand(ToggleAdvancedSettings);
        SelectedGoalOption = GoalOptions.First(option => option.Value == Goal);
        SelectedWeightGoalPaceOption = WeightGoalPaceOptions.First(option => option.Value == WeightGoalPace);
    }

    /// <summary>The first-load indicator, so saving or recalculating does not blank a page that is already on screen.</summary>
    public bool IsPageLoading => AppPageLoadGate.IsLoading(IsBusy, _hasLoadedOnce);
    /// <summary>Compares a snapshot of the editable state with the one loaded, so changing a value and changing it back</summary>
    /// correctly reads as no change.</summary>
    public bool HasUnsavedChanges =>
        _hasLoadedOnce && !string.Equals(BuildSnapshot(), _initialSnapshot, StringComparison.Ordinal);

    /// <summary>The settings snapshot used to detect changes.</summary>
    private string BuildSnapshot() =>
        $"{Goal}|{WeightGoalPace}|{BuildDietaryTags()}|{CaloriesGoal}|{ProteinGoal}|{CarbsGoal}|{FatsGoal}|{FiberGoal}|{WaterGoal}|{IronGoal}|{VitaminB12Goal}|{VitaminCGoal}|{VitaminDGoal}|{VitaminAGoal}|{VitaminB6Goal}|{FolateGoal}|{CalciumGoal}|{MagnesiumGoal}|{ZincGoal}|{PotassiumGoal}";

    /// <summary>Leaves the page, asking first when there are unsaved changes.</summary>
    private async Task BackAsync() =>
        await ProfileNavigationGuard.TryNavigateBackAsync(HasUnsavedChanges, ProfileDiscardPrompt.ConfirmAsync,
            _navigateBackAsync);

    /// <summary>After the calorie field is left, rebalances the macros to match — but only for a value inside the accepted</summary>
    /// range, and never while the targets are being filled in programmatically, which would loop.</summary>
    public void CommitCaloriesEdit()
    {
        if (!_isSyncingTargets && ParseOptionalGoal(CaloriesGoal) is { } calories &&
            calories is >= MinCaloriesKcal and <= MaxCaloriesKcal)
            RebalanceMacrosFromCalories(calories);
    }

    /// <summary>After a macro field is left, recomputes the calories those macros add up to.</summary>
    public void CommitMacroEdit()
    {
        RecalculateCaloriesFromMacros();
    }

    /// <summary>Restores the targets the plan calculates for this profile, discarding the user's own numbers.</summary>
    private void ResetToCalculated()
    {
        if (_loadedProfile is null)
            return;
        ApplyGoals(CreateCalculatedGoals());
        StatusMessage = string.Empty;
        IsStatusError = false;
    }

    /// <summary>Selectable goals with their localized labels.</summary>
    public IReadOnlyList<SelectionOption<UserGoal>> GoalOptions { get; } =
    [
        new(UserGoal.FatLoss, "Fat Loss"),
        new(UserGoal.MuscleTone, "Muscle Tone"),
        new(UserGoal.Strength, "Strength"),
        new(UserGoal.MaintainHealth, "Maintain Health")
    ];

    /// <summary>The weight pace options.</summary>
    public IReadOnlyList<SelectionOption<WeightGoalPace>> WeightGoalPaceOptions { get; } =
    [
        new(WeightGoalPace.Steady, "Steady"),
        new(WeightGoalPace.Aggressive, "Aggressive")
    ];

    /// <summary>Weight-goal pace is only meaningful for fat loss and strength, so the choice is hidden for other goals.</summary>
    public bool IsGoalPaceVisible => Goal is UserGoal.FatLoss or UserGoal.Strength;

    /// <summary>The micronutrient toggle names the action it will take.</summary>
    public string AdvancedSettingsButtonText => AreAdvancedSettingsVisible
        ? "Profile_Nutrition_HideMicronutrients"
        : "Profile_Nutrition_ShowMicronutrients";

    /// <summary>Returns to the profile.</summary>
    public AsyncRelayCommand BackCommand { get; }
    /// <summary>Loads the settings.</summary>
    public AsyncRelayCommand LoadDataCommand { get; }
    /// <summary>Saves the settings.</summary>
    public AsyncRelayCommand SaveCommand { get; }
    /// <summary>Resets the targets to the calculated values.</summary>
    public RelayCommand ResetToCalculatedCommand { get; }
    /// <summary>Shows or hides the advanced settings.</summary>
    public RelayCommand ToggleAdvancedSettingsCommand { get; }
    /// <summary>Toggles the vegetarian filter.</summary>
    public RelayCommand ToggleVegetarianCommand { get; }
    /// <summary>Toggles the vegan filter.</summary>
    public RelayCommand ToggleVeganCommand { get; }
    /// <summary>Toggles the gluten-free filter.</summary>
    public RelayCommand ToggleGlutenFreeCommand { get; }
    /// <summary>Toggles the lactose-free filter.</summary>
    public RelayCommand ToggleLactoseFreeCommand { get; }

    /// <summary>Loads the profile and its saved goals, and keeps the loaded state as the baseline the unsaved-changes flag</summary>
    /// compares against.</summary>
    private async Task LoadDataAsync()
    {
        IsBusy = true;
        IsLoadError = false;
        StatusMessage = string.Empty;
        IsStatusError = false;
        try
        {
            var profile = await _useCase.LoadAsync();
            if (profile is null)
            {
                _hasLoadedOnce = true;
                _initialSnapshot = BuildSnapshot();
                return;
            }

            _loadedProfile = profile;

            _isSyncingTargets = true;
            try
            {
                Goal = profile.Goal;
                WeightGoalPace = profile.Goal is UserGoal.FatLoss or UserGoal.Strength
                    ? profile.WeightGoalPace
                    : WeightGoalPace.Steady;
                SelectedGoalOption = GoalOptions.First(option => option.Value == Goal);
                SelectedWeightGoalPaceOption = WeightGoalPaceOptions.First(option => option.Value == WeightGoalPace);
                ApplyDietaryTags(profile.DietaryTags);
            }
            finally
            {
                _isSyncingTargets = false;
            }

            var calculatedGoals = CreateCalculatedGoals();
            var savedGoals = profile.SavedGoals;
            _hasSavedCustomGoals = _useCase.HasOverrides(savedGoals, CreateCalculatedGoals());
            ApplyGoals(_hasSavedCustomGoals
                ? _useCase.MergeGoals(savedGoals, calculatedGoals)
                : calculatedGoals);
            _hasLoadedOnce = true;
            _initialSnapshot = BuildSnapshot();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ProfileNutritionSettingsViewModel] Load failed ({ex.GetType().Name}).");
            IsLoadError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Validates the form and saves it; the validation failures become the page's error list rather than a generic</summary>
    /// failure.</summary>
    private async Task SaveAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        StatusMessage = string.Empty;
        IsStatusError = false;
        var settingsSaved = false;
        try
        {
            if (_loadedProfile is null)
            {
                IsStatusError = true;
                StatusMessage = "Ui_Profile_Status_CompletePersonalInfo";
                return;
            }

            NutritionGoalsDto editedGoals = BuildGoals();
            var validationErrors = CollectValidationErrors(editedGoals);
            if (validationErrors.Count > 0)
            {
                IsStatusError = true;
                StatusMessage = string.Join(" ", validationErrors);
                return;
            }

            var saved = await _useCase.SaveAsync(new NutritionSettingsSaveDto(
                _loadedProfile, CurrentSelection(), editedGoals));
            if (!saved)
            {
                IsStatusError = true;
                StatusMessage = "Ui_Profile_Status_CompletePersonalInfo";
                return;
            }

            settingsSaved = true;
            _hasSavedCustomGoals = _useCase.HasOverrides(editedGoals, CreateCalculatedGoals());
            _initialSnapshot = BuildSnapshot();
            _refreshNotifier.InvalidateAll();
            IsStatusError = false;
            StatusMessage = "Ui_Profile_Status_NutritionSaved";
            await _navigateAfterSaveAsync();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ProfileNutritionSettingsViewModel] Save failed ({ex.GetType().Name}).");
            IsStatusError = true;
            StatusMessage = settingsSaved
                ? "Settings saved, but Nutrition could not open. Use the Nutrition tab to continue."
                : "Nutrition settings could not be saved. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Per-field range errors, reported only for fields the user actually filled in, so an empty optional target</summary>
    /// is not an error.</summary>
    private List<string> CollectValidationErrors(NutritionGoalsDto goals)
    {
        var errors = new List<string>();
        if (!string.IsNullOrWhiteSpace(CaloriesGoal) &&
            (goals.Calories is null || goals.Calories < MinCaloriesKcal || goals.Calories > MaxCaloriesKcal))
            errors.Add(UiTextCatalog.Get("Profile_Nutrition_InvalidCalories"));

        if (!string.IsNullOrWhiteSpace(ProteinGoal) &&
            (goals.Protein is null || goals.Protein < MinProteinGrams || goals.Protein > MaxProteinGrams))
            errors.Add(UiTextCatalog.Get("Profile_Nutrition_InvalidProtein"));

        if (!string.IsNullOrWhiteSpace(CarbsGoal) &&
            (goals.Carbs is null || goals.Carbs < MinCarbsGrams || goals.Carbs > MaxCarbsGrams))
            errors.Add(UiTextCatalog.Get("Profile_Nutrition_InvalidCarbs"));

        if (!string.IsNullOrWhiteSpace(FatsGoal) &&
            (goals.Fats is null || goals.Fats < MinFatsGrams || goals.Fats > MaxFatsGrams))
            errors.Add(UiTextCatalog.Get("Profile_Nutrition_InvalidFats"));

        return errors;
    }

    /// <summary>Turns the stored comma-separated tags into the four switches.</summary>
    private void ApplyDietaryTags(string tags)
    {
        IsVegetarian = HasTag(tags, DietaryTag.Vegetarian);
        IsVegan = HasTag(tags, DietaryTag.Vegan);
        IsGlutenFree = HasTag(tags, DietaryTag.GlutenFree);
        IsLactoseFree = HasTag(tags, DietaryTag.LactoseFree);
    }

    /// <summary>The switches back in the stored form, so a round trip does not reorder or duplicate a tag.</summary>
    private string BuildDietaryTags()
    {
        var tags = new List<string>();

        if (IsVegetarian)
            tags.Add(DietaryTag.Vegetarian.ToString());
        if (IsVegan)
            tags.Add(DietaryTag.Vegan.ToString());
        if (IsGlutenFree)
            tags.Add(DietaryTag.GlutenFree.ToString());
        if (IsLactoseFree)
            tags.Add(DietaryTag.LactoseFree.ToString());

        return string.Join(",", tags);
    }

    /// <summary>Flips the vegetarian filter.</summary>
    private void ToggleVegetarian() => IsVegetarian = !IsVegetarian;
    /// <summary>Flips the vegan filter.</summary>
    private void ToggleVegan() => IsVegan = !IsVegan;
    /// <summary>Flips the gluten-free filter.</summary>
    private void ToggleGlutenFree() => IsGlutenFree = !IsGlutenFree;
    /// <summary>Flips the lactose-free filter.</summary>
    private void ToggleLactoseFree() => IsLactoseFree = !IsLactoseFree;
    /// <summary>Flips the advanced settings visibility.</summary>
    private void ToggleAdvancedSettings() => AreAdvancedSettingsVisible = !AreAdvancedSettingsVisible;

    /// <summary>Writes goals into the fields behind a guard, so the field-change handlers do not re-derive the numbers they</summary>
    /// are being given.</summary>
    private void ApplyGoals(NutritionGoalsDto goals)
    {
        _isSyncingTargets = true;
        try
        {
            CaloriesGoal = FormatGoal(goals.Calories);
            ProteinGoal = FormatGoal(goals.Protein);
            CarbsGoal = FormatGoal(goals.Carbs);
            FatsGoal = FormatGoal(goals.Fats);
            FiberGoal = FormatGoal(goals.Fiber);
            WaterGoal = FormatGoal(goals.WaterLiters);
            IronGoal = FormatGoal(goals.Iron);
            VitaminB12Goal = FormatGoal(goals.VitaminB12);
            VitaminCGoal = FormatGoal(goals.VitaminC);
            VitaminDGoal = FormatGoal(goals.VitaminD);
            VitaminAGoal = FormatGoal(goals.VitaminA);
            VitaminB6Goal = FormatGoal(goals.VitaminB6);
            FolateGoal = FormatGoal(goals.Folate);
            CalciumGoal = FormatGoal(goals.Calcium);
            MagnesiumGoal = FormatGoal(goals.Magnesium);
            ZincGoal = FormatGoal(goals.Zinc);
            PotassiumGoal = FormatGoal(goals.Potassium);
        }
        finally
        {
            _isSyncingTargets = false;
        }
    }

    /// <summary>The goal, its pace and the dietary tags as one selection; pace falls back to steady when the goal does not</summary>
    /// use one, so the saved value is never left over from a previous goal.</summary>
    private NutritionSettingsSelectionDto CurrentSelection()
    {
        return new(Goal, IsGoalPaceVisible ? WeightGoalPace : WeightGoalPace.Steady, BuildDietaryTags());
    }

    /// <summary>The targets the use case calculates for the current selection, or empty before a profile is loaded.</summary>
    private NutritionGoalsDto CreateCalculatedGoals()
    {
        return _loadedProfile is null
            ? NutritionGoalsDto.Empty
            : _useCase.CalculateGoals(_loadedProfile, CurrentSelection());
    }

    /// <summary>Recalculates the targets after the goal or the tags change, skipped while a programmatic update is running.</summary>
    private void RefreshCalculatedTargets()
    {
        if (_loadedProfile is null || _isSyncingTargets)
            return;

        ApplyGoals(CreateCalculatedGoals());
    }

    /// <summary>Redistributes the three macros to fit a calorie value, keeping their proportions instead of scaling one.</summary>
    private void RebalanceMacrosFromCalories(float calories)
    {
        if (_isSyncingTargets || _loadedProfile is null)
            return;

        var macros = _useCase.RebalanceMacros(_loadedProfile, CurrentSelection(), calories);
        if (macros is null)
            return;
        _isSyncingTargets = true;
        try
        {
            ProteinGoal = FormatGoal(macros.Protein);
            CarbsGoal = FormatGoal(macros.Carbs);
            FatsGoal = FormatGoal(macros.Fats);
            FiberGoal = FormatGoal(macros.Fiber);
        }
        finally
        {
            _isSyncingTargets = false;
        }
    }

    /// <summary>The inverse direction: adds the macros' calories up, which needs all three to be present.</summary>
    private void RecalculateCaloriesFromMacros()
    {
        if (_isSyncingTargets || _loadedProfile is null ||
            ParseOptionalGoal(ProteinGoal) is not { } protein ||
            ParseOptionalGoal(CarbsGoal) is not { } carbs ||
            ParseOptionalGoal(FatsGoal) is not { } fats)
            return;

        var targets = _useCase.CalculateCalories(_loadedProfile, protein, carbs, fats);
        if (targets is null)
            return;
        _isSyncingTargets = true;
        try
        {
            CaloriesGoal = FormatGoal(targets.Calories);
            FiberGoal = FormatGoal(targets.Fiber);
        }
        finally
        {
            _isSyncingTargets = false;
        }
    }

    /// <summary>The form as the goals it will save, with blank optional fields staying unset rather than zero.</summary>
    private NutritionGoalsDto BuildGoals()
    {
        return new NutritionGoalsDto(
            ParseOptionalGoal(CaloriesGoal),
            ParseOptionalGoal(ProteinGoal),
            ParseOptionalGoal(CarbsGoal),
            ParseOptionalGoal(FatsGoal),
            ParseOptionalGoal(FiberGoal),
            ParseOptionalGoal(WaterGoal),
            ParseOptionalGoal(IronGoal),
            ParseOptionalGoal(VitaminB12Goal),
            ParseOptionalGoal(VitaminCGoal),
            ParseOptionalGoal(VitaminDGoal),
            ParseOptionalGoal(VitaminAGoal),
            ParseOptionalGoal(VitaminB6Goal),
            ParseOptionalGoal(FolateGoal),
            ParseOptionalGoal(CalciumGoal),
            ParseOptionalGoal(MagnesiumGoal),
            ParseOptionalGoal(ZincGoal),
            ParseOptionalGoal(PotassiumGoal));
    }

    /// <summary>Whether the stored tag list contains one tag, compared case-insensitively and ignoring padding.</summary>
    private static bool HasTag(string tags, DietaryTag tag)
    {
        return tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(value => string.Equals(value, tag.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A goal value, where an empty field means the target is not set.</summary>
    private static float? ParseOptionalGoal(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var current))
            return Math.Max(0f, current);

        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariant)
            ? Math.Max(0f, invariant)
            : null;
    }

    /// <summary>A goal value as the field's text, showing that it is unset rather than a zero the user never entered.</summary>
    private static string FormatGoal(float? value)
    {
        return value is > 0f ? value.Value.ToString("0.##", CultureInfo.CurrentCulture) : string.Empty;
    }

    /// <summary>Recalculates the targets when the goal changes.</summary>
    partial void OnGoalChanged(UserGoal value)
    {
        var selected = GoalOptions.FirstOrDefault(option => option.Value == value);
        if (selected is not null && SelectedGoalOption?.Value != value)
            SelectedGoalOption = selected;

        if (value is not (UserGoal.FatLoss or UserGoal.Strength))
            WeightGoalPace = WeightGoalPace.Steady;

        OnPropertyChanged(nameof(IsGoalPaceVisible));
        RefreshCalculatedTargets();
    }

    /// <summary>Recalculates the targets when the pace changes.</summary>
    partial void OnWeightGoalPaceChanged(WeightGoalPace value)
    {
        var selected = WeightGoalPaceOptions.FirstOrDefault(option => option.Value == value);
        if (selected is not null && SelectedWeightGoalPaceOption?.Value != value)
            SelectedWeightGoalPaceOption = selected;

        RefreshCalculatedTargets();
    }

    /// <summary>Keeps the goal selection in step with the chosen option.</summary>
    partial void OnSelectedGoalOptionChanged(SelectionOption<UserGoal>? value)
    {
        if (value is not null && Goal != value.Value)
            Goal = value.Value;
    }

    /// <summary>Keeps the pace selection in step with the chosen option.</summary>
    partial void OnSelectedWeightGoalPaceOptionChanged(SelectionOption<WeightGoalPace>? value)
    {
        if (value is not null && WeightGoalPace != value.Value)
            WeightGoalPace = value.Value;
    }
}
