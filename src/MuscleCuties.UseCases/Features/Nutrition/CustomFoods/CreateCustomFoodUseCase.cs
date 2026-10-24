using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.CustomFoods.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.CustomFoods;

public sealed class CreateCustomFoodUseCase(ICreateCustomFoodStore store) : ICreateCustomFoodUseCase
{
    public async Task<CustomFoodDto> ExecuteAsync(
        CreateCustomFoodRequest request,
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (ownerUserId == Guid.Empty)
            throw new ArgumentException("A non-empty authenticated user ID is required.", nameof(ownerUserId));
        if (!CustomFoodCreationRule.TryCreate(request.Name, request.ServingAmount, request.ServingUnit,
                request.Calories, request.Protein, request.Carbs, request.Fats, out var nutrition, out var error))
            throw new ArgumentException(error, nameof(request));

        if (!CustomFoodCreationRule.TryNormalizeMicronutrients(
                request.IronMilligrams,
                request.VitaminB12Micrograms,
                request.VitaminDMicrograms,
                request.CalciumMilligrams,
                nutrition.ServingGrams,
                out var micronutrients))
            throw new ArgumentException("Micronutrients are outside the supported range.", nameof(request));

        var saved = await store.CreateAsync(ownerUserId, nutrition, micronutrients, request.CompanySource, cancellationToken).ConfigureAwait(false);
        return new CustomFoodDto(
            $"custom:{saved.Id:N}",
            saved.Name,
            saved.Calories,
            saved.Protein,
            saved.Carbs,
            saved.Fats,
            saved.ServingSize,
            saved.ServingSizeUnit,
            saved.CompanySource,
            saved.IronMilligrams,
            saved.VitaminB12Micrograms,
            saved.VitaminDMicrograms,
            saved.CalciumMilligrams);
    }
}
