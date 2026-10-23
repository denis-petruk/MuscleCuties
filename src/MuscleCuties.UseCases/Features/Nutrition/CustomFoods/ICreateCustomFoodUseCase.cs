using MuscleCuties.UseCases.Features.Nutrition.CustomFoods.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.CustomFoods;

public interface ICreateCustomFoodUseCase
{
    Task<CustomFoodDto> ExecuteAsync(CreateCustomFoodRequest request, Guid ownerUserId, CancellationToken cancellationToken = default);
}
