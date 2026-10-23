using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.CustomFoods.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.CustomFoods;

public interface ICreateCustomFoodStore
{
    Task<CustomFoodRecord> CreateAsync(Guid ownerUserId, CustomFoodNutrition nutrition, CancellationToken cancellationToken = default);
}
