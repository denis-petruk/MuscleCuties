using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.Core.Domain.Nutrition;

/// <summary>One food of a saved template, with the amount she ate.</summary>
public sealed record SavedMealTemplateItem(
    Guid Id, string Name, float ServingSize, string ServingUnit, float ServingsCount,
    float Calories, float Protein, float Carbs, float Fats);

/// <summary>A meal she saved for reuse, with the foods and amounts it holds.</summary>
public sealed class SavedMealTemplate
{
    public const int MaxNameLength = 160;
    public const int MaxItemNameLength = 160;
    public const int MaxServingUnitLength = 40;
    public const int MinimumItemCount = 1;
    public const float MinimumNutritionValue = 0f;
    public const float MinimumPortionValue = 0f;

    /// <summary>Holds a reusable meal with the nutrition its foods add up to.</summary>
    private SavedMealTemplate(string name, MealType mealType, IReadOnlyList<SavedMealTemplateItem> items,
        float calories, float protein, float carbs, float fats)
    {
        Name = name;
        MealType = mealType;
        Items = items;
        TotalCalories = calories;
        TotalProtein = protein;
        TotalCarbs = carbs;
        TotalFats = fats;
    }

    /// <summary>Name the user gave the template.</summary>
    public string Name { get; }
    /// <summary>Meal of the day the template belongs to.</summary>
    public MealType MealType { get; }
    /// <summary>Foods the template saves.</summary>
    public IReadOnlyList<SavedMealTemplateItem> Items { get; }
    /// <summary>Energy the saved foods add up to.</summary>
    public float TotalCalories { get; }
    /// <summary>Protein the saved foods add up to.</summary>
    public float TotalProtein { get; }
    /// <summary>Carbohydrate the saved foods add up to.</summary>
    public float TotalCarbs { get; }
    /// <summary>Fat the saved foods add up to.</summary>
    public float TotalFats { get; }

    /// <summary>Validates the inputs and sums the items' nutrition into the template.</summary>
    public static SavedMealTemplate Create(string name, MealType mealType,
        IReadOnlyList<SavedMealTemplateItem>? items)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength)
            throw new ArgumentException("Enter a template name within the supported length.", nameof(name));
        if (!Enum.IsDefined(mealType))
            throw new ArgumentOutOfRangeException(nameof(mealType));
        if (items is null || items.Count < MinimumItemCount)
            throw new ArgumentException("Save at least one food item.", nameof(items));

        double calories = MinimumNutritionValue;
        double protein = MinimumNutritionValue;
        double carbs = MinimumNutritionValue;
        double fats = MinimumNutritionValue;
        foreach (var item in items)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Name) ||
                item.Name.Trim().Length > MaxItemNameLength ||
                string.IsNullOrWhiteSpace(item.ServingUnit) ||
                item.ServingUnit.Trim().Length > MaxServingUnitLength ||
                !float.IsFinite(item.ServingSize) || item.ServingSize <= MinimumPortionValue ||
                !float.IsFinite(item.ServingsCount) || item.ServingsCount <= MinimumPortionValue ||
                !ValidNutrient(item.Calories) || !ValidNutrient(item.Protein) ||
                !ValidNutrient(item.Carbs) || !ValidNutrient(item.Fats))
                throw new ArgumentException("Template items need a name, positive portion and finite nutrition.",
                    nameof(items));
            calories += item.Calories;
            protein += item.Protein;
            carbs += item.Carbs;
            fats += item.Fats;
        }

        if (calories > float.MaxValue || protein > float.MaxValue ||
            carbs > float.MaxValue || fats > float.MaxValue)
            throw new ArgumentException("Template totals exceed supported values.", nameof(items));

        return new SavedMealTemplate(name.Trim(), mealType, items.ToArray(),
            (float)calories, (float)protein, (float)carbs, (float)fats);
    }

    /// <summary>True when the nutrient value is finite and not negative.</summary>
    private static bool ValidNutrient(float value) => float.IsFinite(value) && value >= MinimumNutritionValue;
}
