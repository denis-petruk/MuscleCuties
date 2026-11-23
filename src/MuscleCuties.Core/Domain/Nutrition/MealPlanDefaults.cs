namespace MuscleCuties.Core.Domain.Nutrition;

/// <summary>
/// The meal-plan defaults that UseCases, the server and the stores each spelled out as a literal. Wording shown
/// to the user still comes from the localization catalog; these are the values contracts carry.
/// </summary>
public static class MealPlanDefaults
{
    public const string BreakfastPreference = "Savoury";
    public const string DefaultMealType = "Lunch";
    public const string BreakfastMealName = "Breakfast";
    public const string LunchMealName = "Lunch";
}
