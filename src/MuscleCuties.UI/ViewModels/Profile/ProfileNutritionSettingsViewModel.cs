using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.UI.Models.UI.Common;
using MuscleCuties.UI.ViewModels.Common;
using MuscleCuties.UseCases.Features.Profile.NutritionSettings;

namespace MuscleCuties.UI.ViewModels.Profile;

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

    public bool IsPageLoading => IsBusy && !_hasLoadedOnce;
    public bool HasUnsavedChanges =>
        _hasLoadedOnce && !string.Equals(BuildSnapshot(), _initialSnapshot, StringComparison.Ordinal);

    private string BuildSnapshot() =>
        $"{Goal}|{WeightGoalPace}|{BuildDietaryTags()}|{CaloriesGoal}|{ProteinGoal}|{CarbsGoal}|{FatsGoal}|{FiberGoal}|{WaterGoal}|{IronGoal}|{VitaminB12Goal}|{VitaminCGoal}|{VitaminDGoal}|{VitaminAGoal}|{VitaminB6Goal}|{FolateGoal}|{CalciumGoal}|{MagnesiumGoal}|{ZincGoal}|{PotassiumGoal}";

    private async Task BackAsync()
    {
        if (HasUnsavedChanges && Shell.Current is { } shell)
        {
            var discard = await shell.DisplayAlertAsync(
                UiTextCatalog.Get("Profile_UnsavedChanges_Title"),
                UiTextCatalog.Get("Profile_UnsavedChanges_Message"),
                UiTextCatalog.Get("Profile_UnsavedChanges_Discard"),
                UiTextCatalog.Get("Profile_UnsavedChanges_KeepEditing"));
            if (!discard)
                return;
        }

        await _navigateBackAsync();
    }

    public void CommitCaloriesEdit()
    {
        if (!_isSyncingTargets && ParseOptionalGoal(CaloriesGoal) is { } calories &&
            calories is >= MinCaloriesKcal and <= MaxCaloriesKcal)
            RebalanceMacrosFromCalories(calories);
    }

    public void CommitMacroEdit()
    {
        RecalculateCaloriesFromMacros();
    }

    private void ResetToCalculated()
    {
        if (_loadedProfile is null)
            return;
        ApplyGoals(CreateCalculatedGoals());
        StatusMessage = string.Empty;
        IsStatusError = false;
    }

    public IReadOnlyList<SelectionOption<UserGoal>> GoalOptions { get; } =
    [
        new(UserGoal.FatLoss, "Fat Loss"),
        new(UserGoal.MuscleTone, "Muscle Tone"),
        new(UserGoal.Strength, "Strength"),
        new(UserGoal.MaintainHealth, "Maintain Health")
    ];

    public IReadOnlyList<SelectionOption<WeightGoalPace>> WeightGoalPaceOptions { get; } =
    [
        new(WeightGoalPace.Steady, "Steady"),
        new(WeightGoalPace.Aggressive, "Aggressive")
    ];

    public bool IsGoalPaceVisible => Goal is UserGoal.FatLoss or UserGoal.Strength;

    public string AdvancedSettingsButtonText => AreAdvancedSettingsVisible
        ? "Profile_Nutrition_HideMicronutrients"
        : "Profile_Nutrition_ShowMicronutrients";

    public AsyncRelayCommand BackCommand { get; }
    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand ResetToCalculatedCommand { get; }
    public RelayCommand ToggleAdvancedSettingsCommand { get; }
    public RelayCommand ToggleVegetarianCommand { get; }
    public RelayCommand ToggleVeganCommand { get; }
    public RelayCommand ToggleGlutenFreeCommand { get; }
    public RelayCommand ToggleLactoseFreeCommand { get; }

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

    private void ApplyDietaryTags(string tags)
    {
        IsVegetarian = HasTag(tags, DietaryTag.Vegetarian);
        IsVegan = HasTag(tags, DietaryTag.Vegan);
        IsGlutenFree = HasTag(tags, DietaryTag.GlutenFree);
        IsLactoseFree = HasTag(tags, DietaryTag.LactoseFree);
    }

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

    private void ToggleVegetarian() => IsVegetarian = !IsVegetarian;
    private void ToggleVegan() => IsVegan = !IsVegan;
    private void ToggleGlutenFree() => IsGlutenFree = !IsGlutenFree;
    private void ToggleLactoseFree() => IsLactoseFree = !IsLactoseFree;
    private void ToggleAdvancedSettings() => AreAdvancedSettingsVisible = !AreAdvancedSettingsVisible;

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

    private NutritionSettingsSelectionDto CurrentSelection()
    {
        return new(Goal, IsGoalPaceVisible ? WeightGoalPace : WeightGoalPace.Steady, BuildDietaryTags());
    }

    private NutritionGoalsDto CreateCalculatedGoals()
    {
        return _loadedProfile is null
            ? NutritionGoalsDto.Empty
            : _useCase.CalculateGoals(_loadedProfile, CurrentSelection());
    }

    private void RefreshCalculatedTargets()
    {
        if (_loadedProfile is null || _isSyncingTargets)
            return;

        ApplyGoals(CreateCalculatedGoals());
    }

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

    private static bool HasTag(string tags, DietaryTag tag)
    {
        return tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(value => string.Equals(value, tag.ToString(), StringComparison.OrdinalIgnoreCase));
    }

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

    private static string FormatGoal(float? value)
    {
        return value is > 0f ? value.Value.ToString("0.##", CultureInfo.CurrentCulture) : string.Empty;
    }

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

    partial void OnWeightGoalPaceChanged(WeightGoalPace value)
    {
        var selected = WeightGoalPaceOptions.FirstOrDefault(option => option.Value == value);
        if (selected is not null && SelectedWeightGoalPaceOption?.Value != value)
            SelectedWeightGoalPaceOption = selected;

        RefreshCalculatedTargets();
    }

    partial void OnSelectedGoalOptionChanged(SelectionOption<UserGoal>? value)
    {
        if (value is not null && Goal != value.Value)
            Goal = value.Value;
    }

    partial void OnSelectedWeightGoalPaceOptionChanged(SelectionOption<WeightGoalPace>? value)
    {
        if (value is not null && WeightGoalPace != value.Value)
            WeightGoalPace = value.Value;
    }
}
