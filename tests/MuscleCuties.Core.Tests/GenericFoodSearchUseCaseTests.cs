using MuscleCuties.UseCases.Features.Nutrition.DailyLog;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class GenericFoodSearchUseCaseTests
{
    private const string GenericFoodQuery = "Chicken Breast";
    private const string NormalizedFoodQuery = "chicken breast";
    private const string PersonalOrUnrecognizedQuery = "Denis meal plan";

    [Fact]
    public void SearchValidationReturnsOnlyNormalizedGenericQueries()
    {
        var useCase = new NutritionDailyLogUseCase(null!);

        var accepted = useCase.ValidateOnlineFoodSearchQuery(GenericFoodQuery);
        var rejected = useCase.ValidateOnlineFoodSearchQuery(PersonalOrUnrecognizedQuery);

        Assert.True(accepted.IsAccepted);
        Assert.Equal(NormalizedFoodQuery, accepted.NormalizedQuery);
        Assert.False(rejected.IsAccepted);
        Assert.Empty(rejected.NormalizedQuery);
    }
}
