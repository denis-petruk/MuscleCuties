using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.Core.Domain.Nutrition;

public sealed record SavedMealTemplateItem(
    Guid Id, string Name, float ServingSize, string ServingUnit, float ServingsCount,
    float Calories, float Protein, float Carbs, float Fats);

public sealed class SavedMealTemplate
{
    public const int MaxNameLength = 160;
    public const int MaxItemNameLength = 160;
    public const int MaxServingUnitLength = 40;
    public const int MinimumItemCount = 1;
    public const float MinimumNutritionValue = 0f;
    public const float MinimumPortionValue = 0f;

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

    public string Name { get; }
    public MealType MealType { get; }
    public IReadOnlyList<SavedMealTemplateItem> Items { get; }
    public float TotalCalories { get; }
    public float TotalProtein { get; }
    public float TotalCarbs { get; }
    public float TotalFats { get; }

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

    private static bool ValidNutrient(float value) => float.IsFinite(value) && value >= MinimumNutritionValue;
}
