using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace MuscleCuties.ViewModels.Nutrition;

public class MealItem
{
    public string Time { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MealType { get; set; } = string.Empty;
    public string CaloriesText { get; set; } = string.Empty;
}

public partial class NutritionViewModel : ObservableObject
{
    [ObservableProperty]
    private string _currentPhaseName = "Follicular";

    [ObservableProperty]
    private string _phaseFocusCopy = "Energy is building — lean on protein and complex carbs.";

    [ObservableProperty]
    private string _caloriesConsumed = "1 240 kcal";

    [ObservableProperty]
    private string _caloriesGoal = "/ 2 000 kcal";

    [ObservableProperty]
    private double _calorieProgress = 0.62;

    [ObservableProperty]
    private string _proteinText = "72 / 100 g";

    [ObservableProperty]
    private double _proteinProgress = 0.72;

    [ObservableProperty]
    private string _carbsText = "116 / 200 g";

    [ObservableProperty]
    private double _carbsProgress = 0.58;

    [ObservableProperty]
    private string _fatsText = "45 / 70 g";

    [ObservableProperty]
    private double _fatsProgress = 0.64;

    public ObservableCollection<MealItem> Meals { get; } = new();

    public NutritionViewModel()
    {
        LoadMeals();
    }

    private void LoadMeals()
    {
        Meals.Add(new MealItem { Time = "8:00",  Name = "Greek yogurt bowl",     MealType = "BREAKFAST", CaloriesText = "340 kcal" });
        Meals.Add(new MealItem { Time = "12:30", Name = "Chicken + quinoa",      MealType = "LUNCH",     CaloriesText = "520 kcal" });
        Meals.Add(new MealItem { Time = "16:00", Name = "Apple + almond butter", MealType = "SNACK",     CaloriesText = "210 kcal" });
        Meals.Add(new MealItem { Time = "19:30", Name = "Salmon + roasted veg",  MealType = "DINNER",    CaloriesText = "480 kcal" });
    }
}