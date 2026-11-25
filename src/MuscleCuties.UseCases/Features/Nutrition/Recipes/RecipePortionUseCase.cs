using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Recipes.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.Recipes;

/// <summary>Converting a portion into grams.</summary>
public interface IRecipePortionUseCase
{
    RecipePortionPreviewDto Calculate(CalculateRecipePortionRequest request);
    FoodMassConversionOutputDto ConvertMassToGrams(float amount, string? unit);
    FoodPortionCatalogOutputDto GetCatalog();
}

/// <summary>Converts a portion using the food's stored measures, answering failure rather than guessing.</summary>
public sealed class RecipePortionUseCase : IRecipePortionUseCase
{
    /// <summary>Scales the recipe's nutrients to the requested portion, refusing a portion with no measure.</summary>
    public RecipePortionPreviewDto Calculate(CalculateRecipePortionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var batchWeight = request.PerServingGrams * request.ServingsCount;
        var batch = new RecipeBatchTotals(
            request.ServingsCount,
            batchWeight,
            batchWeight,
            request.PerServingCalories * request.ServingsCount,
            request.PerServingProtein * request.ServingsCount,
            request.PerServingCarbs * request.ServingsCount,
            request.PerServingNetCarbs * request.ServingsCount,
            request.PerServingFats * request.ServingsCount);
        var portion = RecipeBatchCalculationRule.CalculatePortion(
            batch, request.RequestedServingsCount, request.RequestedGrams);

        return new RecipePortionPreviewDto(
            request.RecipeId,
            request.RecipeName,
            portion.Batch.ServingsCount,
            portion.Batch.TotalRawWeightGrams,
            portion.Batch.CookedWeightGrams,
            portion.Batch.TotalCalories,
            portion.Batch.TotalProtein,
            portion.Batch.TotalCarbs,
            portion.Batch.TotalNetCarbs,
            portion.Batch.TotalFats,
            portion.SelectedServingsCount,
            portion.SelectedGrams,
            portion.Calories,
            portion.Protein,
            portion.Carbs,
            portion.NetCarbs,
            portion.Fats);
    }

    /// <summary>Converts a measure into grams, answering failure rather than assuming a density.</summary>
    public FoodMassConversionOutputDto ConvertMassToGrams(float amount, string? unit)
    {
        var succeeded = FoodPortionMassRule.TryConvertMassToGrams(amount, unit, out var grams);
        return new FoodMassConversionOutputDto(succeeded, succeeded ? grams : null);
    }

    /// <summary>The portion sizes the recipe screen offers.</summary>
    public FoodPortionCatalogOutputDto GetCatalog() => new(
        FoodPortionMassRule.GramUnit,
        Array.AsReadOnly(new[]
        {
            FoodPortionMassRule.GramUnit,
            FoodPortionMassRule.OunceUnit,
            FoodPortionMassRule.PoundUnit
        }),
        Array.AsReadOnly(new[]
        {
            new FoodServingOptionOutputDto(
                FoodPortionMassRule.GramUnit, FoodPortionMassRule.GramUnit, FoodPortionMassRule.OneGram),
            new FoodServingOptionOutputDto(
                FoodPortionMassRule.OunceUnit, FoodPortionMassRule.OunceUnit,
                FoodPortionMassRule.GramsPerOunce),
            new FoodServingOptionOutputDto(
                FoodPortionMassRule.PoundUnit, FoodPortionMassRule.PoundUnit,
                FoodPortionMassRule.GramsPerPound)
        }));
}
