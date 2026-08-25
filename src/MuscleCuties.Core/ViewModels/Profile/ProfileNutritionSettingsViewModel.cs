using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.UI.Common;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Nutrition.Planning;

namespace MuscleCuties.Core.ViewModels.Profile;

public partial class ProfileNutritionSettingsViewModel : ObservableObject
{
    private const float MinimumEditableCalories = 1000f;
    private const float MaximumEditableCalories = 5000f;

    private readonly IAuthService _authService;
    private readonly IUserRepository _userRepository;
    private readonly ICycleService _cycleService;
    private readonly INutritionPlanner _nutritionPlanner;
    private readonly Action _navigateBack;
    private UserProfile? _loadedProfile;
    private CyclePhase _currentPhase = CyclePhase.Follicular;
    private bool _hasSavedCustomGoals;
    private bool _isSyncingTargets;

    [ObservableProperty] private UserGoal _goal;
    [ObservableProperty] private WeightGoalPace _weightGoalPace;
    [ObservableProperty] private SelectionOption<UserGoal>? _selectedGoalOption;
    [ObservableProperty] private SelectionOption<WeightGoalPace>? _selectedWeightGoalPaceOption;
    [ObservableProperty] private bool _isVegetarian;
    [ObservableProperty] private bool _isVegan;
    [ObservableProperty] private bool _isGlutenFree;
    [ObservableProperty] private bool _isLactoseFree;
    [ObservableProperty] private bool _areAdvancedSettingsVisible;
    [ObservableProperty] private string _caloriesGoal = string.Empty;
    [ObservableProperty] private string _proteinGoal = string.Empty;
    [ObservableProperty] private string _carbsGoal = string.Empty;
    [ObservableProperty] private string _fatsGoal = string.Empty;
    [ObservableProperty] private string _fiberGoal = string.Empty;
    [ObservableProperty] private string _waterGoal = string.Empty;
    [ObservableProperty] private string _ironGoal = string.Empty;
    [ObservableProperty] private string _vitaminB12Goal = string.Empty;
    [ObservableProperty] private string _vitaminCGoal = string.Empty;
    [ObservableProperty] private string _vitaminDGoal = string.Empty;
    [ObservableProperty] private string _vitaminAGoal = string.Empty;
    [ObservableProperty] private string _vitaminB6Goal = string.Empty;
    [ObservableProperty] private string _folateGoal = string.Empty;
    [ObservableProperty] private string _calciumGoal = string.Empty;
    [ObservableProperty] private string _magnesiumGoal = string.Empty;
    [ObservableProperty] private string _zincGoal = string.Empty;
    [ObservableProperty] private string _potassiumGoal = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public IReadOnlyList<SelectionOption<UserGoal>> GoalOptions { get; } = ProfileSelectionOptions.Goals;
    public IReadOnlyList<SelectionOption<WeightGoalPace>> WeightGoalPaceOptions { get; } =
        ProfileSelectionOptions.WeightGoalPaces;
    public bool IsGoalPaceVisible => ProfileSelectionOptions.UsesWeightGoalPace(Goal);
    public string AdvancedSettingsButtonText => AreAdvancedSettingsVisible
        ? "Hide advanced settings"
        : "Advanced settings";
    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand BackCommand { get; }
    public RelayCommand ToggleAdvancedSettingsCommand { get; }

    public ProfileNutritionSettingsViewModel(
        IAuthService authService,
        IUserRepository userRepository,
        ICycleService cycleService,
        INutritionPlanner nutritionPlanner,
        Action navigateBack)
    {
        _authService = authService;
        _userRepository = userRepository;
        _cycleService = cycleService;
        _nutritionPlanner = nutritionPlanner;
        _navigateBack = navigateBack;
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        BackCommand = new RelayCommand(_navigateBack);
        ToggleAdvancedSettingsCommand = new RelayCommand(ToggleAdvancedSettings);
        SelectedGoalOption = GoalOptions.First(option => option.Value == Goal);
        SelectedWeightGoalPaceOption = WeightGoalPaceOptions.First(option => option.Value == WeightGoalPace);
    }

    private async Task LoadDataAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var profile = await _userRepository.GetProfileAsync(userId);
            if (profile is null)
                return;

            _loadedProfile = profile;
            _currentPhase = await _cycleService.GetCurrentPhaseAsync(userId);

            _isSyncingTargets = true;
            try
            {
                Goal = profile.Goal;
                WeightGoalPace = ProfileSelectionOptions.UsesWeightGoalPace(profile.Goal)
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
            var savedGoals = ProfileNutritionGoals.FromJson(profile.NutritionGoalsJson);
            _hasSavedCustomGoals = savedGoals.HasAnyValue;
            ApplyGoals(_hasSavedCustomGoals
                ? savedGoals.WithFallbacks(calculatedGoals)
                : calculatedGoals);
            AreAdvancedSettingsVisible = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var profile = await _userRepository.GetProfileAsync(userId);
            if (profile is null)
            {
                StatusMessage = "Complete personal info before nutrition settings.";
                return;
            }

            profile.Goal = Goal;
            profile.WeightGoalPace = IsGoalPaceVisible ? WeightGoalPace : WeightGoalPace.Steady;
            profile.DietaryTags = BuildDietaryTags();
            if (AreAdvancedSettingsVisible || _hasSavedCustomGoals)
            {
                var goals = BuildGoals();
                profile.NutritionGoalsJson = goals.HasAnyValue ? goals.ToJson() : string.Empty;
                _hasSavedCustomGoals = goals.HasAnyValue;
            }
            profile.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateProfileAsync(profile);
            await _userRepository.AddSnapshotAsync(new UserProfileSnapshot
            {
                UserId = userId,
                SnapshotReason = "NutritionSettingsUpdate",
                ProfileJson = JsonSerializer.Serialize(new
                {
                    Goal = profile.Goal.ToString(),
                    WeightGoalPace = profile.WeightGoalPace.ToString(),
                    profile.DietaryTags,
                    NutritionGoals = ProfileNutritionGoals.FromJson(profile.NutritionGoalsJson)
                }),
                CreatedAt = DateTime.UtcNow
            });

            StatusMessage = "Nutrition settings saved.";
        }
        finally
        {
            IsBusy = false;
        }
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

    private void ToggleAdvancedSettings()
    {
        AreAdvancedSettingsVisible = !AreAdvancedSettingsVisible;
    }

    private void ApplyGoals(ProfileNutritionGoals goals)
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

    private ProfileNutritionGoals CreateCalculatedGoals()
    {
        if (_loadedProfile is null)
            return _nutritionPlanner.CreateFallbackPlan(_currentPhase).Goals;

        var planningProfile = new UserProfile
        {
            Id = _loadedProfile.Id,
            UserId = _loadedProfile.UserId,
            Name = _loadedProfile.Name,
            DateOfBirth = _loadedProfile.DateOfBirth,
            Height = _loadedProfile.Height,
            Weight = _loadedProfile.Weight,
            Goal = Goal,
            WeightGoalPace = IsGoalPaceVisible ? WeightGoalPace : WeightGoalPace.Steady,
            TrainingExperienceLevel = _loadedProfile.TrainingExperienceLevel,
            CycleTrackingMode = _loadedProfile.CycleTrackingMode,
            WorkoutDaysPerWeek = _loadedProfile.WorkoutDaysPerWeek,
            CycleLength = _loadedProfile.CycleLength,
            DietaryTags = BuildDietaryTags(),
            PreferredWorkoutActivityTypes = _loadedProfile.PreferredWorkoutActivityTypes,
            UnitSystem = _loadedProfile.UnitSystem,
            BodyWeightUnit = _loadedProfile.BodyWeightUnit,
            FoodMassUnit = _loadedProfile.FoodMassUnit,
            HeightUnit = _loadedProfile.HeightUnit,
            DistanceUnit = _loadedProfile.DistanceUnit,
            EnergyUnit = _loadedProfile.EnergyUnit,
            NutritionGoalsJson = string.Empty,
            UpdatedAt = _loadedProfile.UpdatedAt
        };

        return _nutritionPlanner.CreateDailyPlan(planningProfile, _currentPhase, DateTime.Today).Goals;
    }

    private void RefreshCalculatedTargets()
    {
        if (_loadedProfile is null || _isSyncingTargets)
            return;

        ApplyGoals(CreateCalculatedGoals());
    }

    private void RebalanceMacrosFromCalories(float calories)
    {
        if (_isSyncingTargets || calories is < MinimumEditableCalories or > MaximumEditableCalories)
            return;

        var macros = CalculateMacrosForCalories(calories);
        _isSyncingTargets = true;
        try
        {
            ProteinGoal = FormatGoal(macros.Protein);
            CarbsGoal = FormatGoal(macros.Carbs);
            FatsGoal = FormatGoal(macros.Fats);
            FiberGoal = FormatGoal(CalculateFiberTarget(calories));
        }
        finally
        {
            _isSyncingTargets = false;
        }
    }

    private void RecalculateCaloriesFromMacros()
    {
        if (_isSyncingTargets ||
            ParseOptionalGoal(ProteinGoal) is not { } protein ||
            ParseOptionalGoal(CarbsGoal) is not { } carbs ||
            ParseOptionalGoal(FatsGoal) is not { } fats)
        {
            return;
        }

        var calories = RoundToNearest(protein * 4f + carbs * 4f + fats * 9f, 10f);
        if (calories is < MinimumEditableCalories or > MaximumEditableCalories)
            return;

        _isSyncingTargets = true;
        try
        {
            CaloriesGoal = FormatGoal(calories);
            FiberGoal = FormatGoal(CalculateFiberTarget(calories));
        }
        finally
        {
            _isSyncingTargets = false;
        }
    }

    private (float Protein, float Carbs, float Fats) CalculateMacrosForCalories(float calories)
    {
        var calculatedGoals = CreateCalculatedGoals();
        var ratios = CalculateMacroRatios(calculatedGoals);
        var weight = Math.Max(_loadedProfile?.Weight ?? 0f, 55f);
        var minimumProtein = Math.Min(weight * GetMinimumProteinPerKg(), calories * 0.45f / 4f);
        var minimumFats = Math.Min(Math.Max(weight * 0.5f, calories * 0.18f / 9f), calories * 0.35f / 9f);

        var protein = Math.Max(calories * ratios.Protein / 4f, minimumProtein);
        var fats = Math.Max(calories * ratios.Fats / 9f, minimumFats);

        if (protein * 4f + fats * 9f > calories)
            fats = Math.Max(0f, Math.Min(fats, (calories - protein * 4f) / 9f));

        if (protein * 4f + fats * 9f > calories)
            protein = Math.Max(0f, (calories - fats * 9f) / 4f);

        var carbs = Math.Max((calories - protein * 4f - fats * 9f) / 4f, 0f);
        return (
            RoundToNearest(protein, 1f),
            RoundToNearest(carbs, 1f),
            RoundToNearest(fats, 1f));
    }

    private (float Protein, float Carbs, float Fats) CalculateMacroRatios(ProfileNutritionGoals goals)
    {
        if (goals.Calories is { } calories &&
            goals.Protein is { } protein &&
            goals.Carbs is { } carbs &&
            goals.Fats is { } fats &&
            calories > 0f &&
            protein > 0f &&
            carbs > 0f &&
            fats > 0f)
        {
            var proteinShare = protein * 4f / calories;
            var carbsShare = carbs * 4f / calories;
            var fatsShare = fats * 9f / calories;
            if (proteinShare > 0f && carbsShare > 0f && fatsShare > 0f)
                return NormalizeRatios(proteinShare, carbsShare, fatsShare);
        }

        return Goal switch
        {
            UserGoal.FatLoss => (0.34f, 0.36f, 0.30f),
            UserGoal.Strength => (0.25f, 0.50f, 0.25f),
            UserGoal.MuscleTone => (0.30f, 0.42f, 0.28f),
            _ => (0.25f, 0.45f, 0.30f)
        };
    }

    private float GetMinimumProteinPerKg() => Goal switch
    {
        UserGoal.FatLoss => 1.8f,
        UserGoal.Strength => 1.7f,
        UserGoal.MuscleTone => 1.5f,
        _ => 1.2f
    };

    private float CalculateFiberTarget(float calories)
    {
        var phaseBonus = _currentPhase is CyclePhase.Luteal ? 3f : 0f;
        return RoundToNearest(MathF.Max(25f, calories / 1000f * 14f) + phaseBonus, 1f);
    }

    private static (float Protein, float Carbs, float Fats) NormalizeRatios(
        float protein,
        float carbs,
        float fats)
    {
        var total = protein + carbs + fats;
        return total <= 0f
            ? (0.30f, 0.40f, 0.30f)
            : (protein / total, carbs / total, fats / total);
    }

    private ProfileNutritionGoals BuildGoals() =>
        new(
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

    private static bool HasTag(string tags, DietaryTag tag) =>
        tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(value => string.Equals(value, tag.ToString(), StringComparison.OrdinalIgnoreCase));

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

    private static string FormatGoal(float? value) =>
        value is > 0f ? value.Value.ToString("0.##", CultureInfo.CurrentCulture) : string.Empty;

    private static float RoundToNearest(float value, float nearest) =>
        nearest <= 0f ? value : MathF.Round(value / nearest) * nearest;

    partial void OnGoalChanged(UserGoal value)
    {
        var selected = GoalOptions.FirstOrDefault(option => option.Value == value);
        if (selected is not null && SelectedGoalOption?.Value != value)
            SelectedGoalOption = selected;

        if (!ProfileSelectionOptions.UsesWeightGoalPace(value))
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

    partial void OnAreAdvancedSettingsVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(AdvancedSettingsButtonText));
    }

    partial void OnCaloriesGoalChanged(string value)
    {
        if (!_isSyncingTargets && ParseOptionalGoal(value) is { } calories)
            RebalanceMacrosFromCalories(calories);
    }

    partial void OnProteinGoalChanged(string value)
    {
        RecalculateCaloriesFromMacros();
    }

    partial void OnCarbsGoalChanged(string value)
    {
        RecalculateCaloriesFromMacros();
    }

    partial void OnFatsGoalChanged(string value)
    {
        RecalculateCaloriesFromMacros();
    }
}
