using ContractMealType = MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts.MealType;
using DomainMealType = MuscleCuties.Core.Models.Enums.Nutrition.MealType;

namespace MuscleCuties.UseCases.Features.Nutrition.Diary;

internal static class NutritionMealTypeMapper
{
    public static DomainMealType ToDomain(ContractMealType mealType) => mealType switch
    {
        ContractMealType.Breakfast => DomainMealType.Breakfast,
        ContractMealType.Lunch => DomainMealType.Lunch,
        ContractMealType.Dinner => DomainMealType.Dinner,
        ContractMealType.Snack => DomainMealType.Snack,
        _ => throw new ArgumentOutOfRangeException(nameof(mealType), mealType, "Unknown meal type.")
    };

    public static ContractMealType ToContract(DomainMealType mealType) => mealType switch
    {
        DomainMealType.Breakfast => ContractMealType.Breakfast,
        DomainMealType.Lunch => ContractMealType.Lunch,
        DomainMealType.Dinner => ContractMealType.Dinner,
        DomainMealType.Snack => ContractMealType.Snack,
        _ => throw new ArgumentOutOfRangeException(nameof(mealType), mealType, "Unknown meal type.")
    };
}
