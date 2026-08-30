using System.Collections.ObjectModel;
using Microsoft.Maui.Graphics;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.UI.Nutrition;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Nutrition.Planning;

namespace MuscleCuties.Core.ViewModels.Nutrition;

public partial class NutritionViewModel
{
    private static readonly IReadOnlyList<MicronutrientDefinition> MicronutrientDefinitions =
    [
        new("Fiber", "Fiber", "g", 25f, food => food.Fiber),
        new("Vitamins", "Vitamin A", "mcg", 700f, food => food.VitaminA, RequiresDietaryFat: true),
        new("Vitamins", "Vitamin C", "mg", 75f, food => food.VitaminC),
        new("Vitamins", "Vitamin D", "mcg", 15f, food => food.VitaminD, RequiresDietaryFat: true),
        new("Vitamins", "Vitamin B6", "mg", 1.3f, food => food.VitaminB6),
        new("Vitamins", "Vitamin B12", "mcg", 2.4f, food => food.VitaminB12),
        new("Vitamins", "Folate", "mcg", 400f, food => food.Folate),
        new("Minerals", "Calcium", "mg", 1000f, food => food.Calcium),
        new("Minerals", "Iron", "mg", 18f, food => food.Iron),
        new("Minerals", "Magnesium", "mg", 320f, food => food.Magnesium),
        new("Minerals", "Zinc", "mg", 8f, food => food.Zinc),
        new("Minerals", "Potassium", "mg", 2600f, food => food.Potassium)
    ];

    private static IEnumerable<DailyMicronutrientItem> BuildMicronutrients(
        IEnumerable<LoggedMeal> meals,
        ProfileNutritionGoals goals) =>
        BuildMicronutrients(meals, meals, goals);

    private static IEnumerable<DailyMicronutrientItem> BuildMicronutrients(
        IEnumerable<LoggedMeal> sourceMeals,
        IEnumerable<LoggedMeal> dailyMeals,
        ProfileNutritionGoals goals)
    {
        var sourceMealList = sourceMeals.ToList();
        var dailyMealList = dailyMeals.ToList();
        var entryList = sourceMealList
            .SelectMany(meal => meal.Entries)
            .Where(entry => entry.FoodItem is not null)
            .ToList();

        foreach (var definition in MicronutrientDefinitions)
        {
            var amount = definition.RequiresDietaryFat
                ? FatSolubleVitaminAbsorption.SumAbsorbableNutrient(
                    sourceMealList,
                    dailyMealList,
                    definition.ValueSelector)
                : entryList.Sum(entry =>
                    definition.ValueSelector(entry.FoodItem!) * entry.Grams / 100f);

            yield return new DailyMicronutrientItem
            {
                Group = definition.Group,
                Name = definition.Name,
                Unit = definition.Unit,
                Amount = amount,
                Goal = definition.GoalSelector(goals)
            };
        }
    }

    private void OpenDailyBreakdown()
    {
        ApplyBreakdown(
            "Daily nutrition",
            new MacroNutrients(ConsumedCalories, ConsumedProtein, ConsumedCarbs, ConsumedFats),
            Micronutrients,
            null);
    }

    private void OpenMealBreakdown(MealItem? meal)
    {
        if (meal is null)
            return;

        ApplyBreakdown(
            $"{meal.MealType} breakdown",
            meal.Macros,
            meal.Micronutrients,
            meal);
    }

    private void ApplyBreakdown(
        string title,
        MacroNutrients macros,
        IEnumerable<DailyMicronutrientItem> micronutrients,
        MealItem? meal)
    {
        var nutrients = micronutrients.ToList();

        SelectedBreakdownMeal = meal;
        SelectedBreakdownTitle = title;
        SelectedBreakdownCaloriesText = $"{macros.Calories:N0} kcal";
        SelectedBreakdownMacrosText = macros.ToMacroText();
        SelectedBreakdownFiberText = BuildFiberText(nutrients);
        SelectedBreakdownNutrientSummaryText = BuildMicronutrientSummaryText(nutrients);
        SelectedBreakdownProteinCalories = macros.Protein * 4f;
        SelectedBreakdownCarbsCalories = macros.Carbs * 4f;
        SelectedBreakdownFatsCalories = macros.Fats * 9f;
        SelectedBreakdownMacroItems = new ObservableCollection<MacroBreakdownItem>(BuildMacroBreakdownItems(macros));
        SelectedBreakdownMicronutrients = new ObservableCollection<DailyMicronutrientItem>(nutrients);
        IsBreakdownModalVisible = true;
    }

    private void CloseBreakdownModal()
    {
        IsBreakdownModalVisible = false;
        SelectedBreakdownMeal = null;
    }

    private async Task EditSelectedBreakdownMealAsync()
    {
        var meal = SelectedBreakdownMeal;
        if (meal is null)
            return;

        CloseBreakdownModal();
        await EditMealAsync(meal);
    }

    private static IEnumerable<MacroBreakdownItem> BuildMacroBreakdownItems(MacroNutrients macros)
    {
        var proteinCalories = macros.Protein * 4f;
        var carbsCalories = macros.Carbs * 4f;
        var fatsCalories = macros.Fats * 9f;
        var macroCalories = Math.Max(0f, proteinCalories + carbsCalories + fatsCalories);

        return
        [
            new MacroBreakdownItem
            {
                Name = "Protein",
                Grams = macros.Protein,
                Calories = proteinCalories,
                Progress = CalculateMacroShare(proteinCalories, macroCalories),
                Color = Color.FromArgb("#A65AC8")
            },
            new MacroBreakdownItem
            {
                Name = "Carbs",
                Grams = macros.Carbs,
                Calories = carbsCalories,
                Progress = CalculateMacroShare(carbsCalories, macroCalories),
                Color = Color.FromArgb("#E3A13B")
            },
            new MacroBreakdownItem
            {
                Name = "Fats",
                Grams = macros.Fats,
                Calories = fatsCalories,
                Progress = CalculateMacroShare(fatsCalories, macroCalories),
                Color = Color.FromArgb("#6F8E4E")
            }
        ];
    }

    private static float CalculateMacroShare(float calories, float totalCalories) =>
        totalCalories <= 0f ? 0f : Math.Clamp(calories / totalCalories, 0f, 1f);

    private static string BuildFiberText(IEnumerable<DailyMicronutrientItem> micronutrients)
    {
        var fiber = micronutrients.FirstOrDefault(item => item.Name == "Fiber");
        return fiber is null ? "0.0g fiber" : $"{fiber.Amount:N1}g fiber";
    }

    private static string BuildMicronutrientSummaryText(IReadOnlyCollection<DailyMicronutrientItem> micronutrients)
    {
        if (micronutrients.Count == 0)
            return "No micronutrients tracked yet";

        var complete = micronutrients.Count(item => item.IsGoalHit);
        return $"{complete} of {micronutrients.Count} daily targets reached";
    }

    private sealed record MicronutrientDefinition(
        string Group,
        string Name,
        string Unit,
        float Goal,
        Func<FoodItem, float> ValueSelector,
        bool RequiresDietaryFat = false)
    {
        public float GoalSelector(ProfileNutritionGoals goals) =>
            Name switch
            {
                "Fiber" => UseGoal(goals.Fiber, Goal),
                "Vitamin A" => UseGoal(goals.VitaminA, Goal),
                "Vitamin C" => UseGoal(goals.VitaminC, Goal),
                "Vitamin D" => UseGoal(goals.VitaminD, Goal),
                "Vitamin B6" => UseGoal(goals.VitaminB6, Goal),
                "Vitamin B12" => UseGoal(goals.VitaminB12, Goal),
                "Folate" => UseGoal(goals.Folate, Goal),
                "Calcium" => UseGoal(goals.Calcium, Goal),
                "Iron" => UseGoal(goals.Iron, Goal),
                "Magnesium" => UseGoal(goals.Magnesium, Goal),
                "Zinc" => UseGoal(goals.Zinc, Goal),
                "Potassium" => UseGoal(goals.Potassium, Goal),
                _ => Goal
            };

        private static float UseGoal(float? customGoal, float fallback) =>
            customGoal is > 0f ? customGoal.Value : fallback;
    }
}
