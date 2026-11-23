using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;

namespace MuscleCuties.Core.Tests;

public sealed class MealPlanDefaultsTests
{
    [Fact]
    public void Contract_Defaults_Come_From_The_Single_Owner()
    {
        var request = new NutritionOverviewRequest(DateOnly.FromDateTime(DateTime.UtcNow));
        Assert.Equal(MealPlanDefaults.BreakfastPreference, request.BreakfastPreference);
        Assert.Equal("Savoury", MealPlanDefaults.BreakfastPreference);
        Assert.Equal("Lunch", MealPlanDefaults.DefaultMealType);
    }

    [Fact]
    public void Meal_Names_Match_The_Rows_The_Stores_Write()
    {
        Assert.Equal("Breakfast", MealPlanDefaults.BreakfastMealName);
        Assert.Equal("Lunch", MealPlanDefaults.LunchMealName);
    }
}
