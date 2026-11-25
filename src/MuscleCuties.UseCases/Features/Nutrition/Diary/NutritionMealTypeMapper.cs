using ContractMealType = MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts.MealType;
using DomainMealType = MuscleCuties.Core.Models.Enums.Nutrition.MealType;

namespace MuscleCuties.UseCases.Features.Nutrition.Diary;

/// <summary>Maps between the wire meal type and the stored one, so a rename never reaches storage.</summary>
internal static class NutritionMealTypeMapper
{
    /// <summary>The domain meal type for its contract value.</summary>
    public static DomainMealType ToDomain(ContractMealType mealType) => mealType switch
    {
        ContractMealType.Breakfast => DomainMealType.Breakfast,
        ContractMealType.Lunch => DomainMealType.Lunch,
        ContractMealType.Dinner => DomainMealType.Dinner,
        ContractMealType.Snack => DomainMealType.Snack,
        _ => throw new ArgumentOutOfRangeException(nameof(mealType), mealType, "Unknown meal type.")
    };

    /// <summary>The contract meal type for its domain value.</summary>
    public static ContractMealType ToContract(DomainMealType mealType) => mealType switch
    {
        DomainMealType.Breakfast => ContractMealType.Breakfast,
        DomainMealType.Lunch => ContractMealType.Lunch,
        DomainMealType.Dinner => ContractMealType.Dinner,
        DomainMealType.Snack => ContractMealType.Snack,
        _ => throw new ArgumentOutOfRangeException(nameof(mealType), mealType, "Unknown meal type.")
    };
}
