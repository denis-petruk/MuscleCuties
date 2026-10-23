using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Recipes.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.Recipes;

public interface ISaveMealTemplateUseCase
{
    Task<MealTemplateDto> ExecuteAsync(SaveMealAsTemplateRequest request, CancellationToken cancellationToken = default);
    Task<MealTemplateDto> ExecuteAsync(Guid userId, SaveMealAsTemplateRequest request, CancellationToken cancellationToken = default);
}

public sealed class SaveMealTemplateUseCase : ISaveMealTemplateUseCase
{
    private readonly IRecipeStore _recipeStore;

    public SaveMealTemplateUseCase(IRecipeStore recipeStore)
    {
        _recipeStore = recipeStore ?? throw new ArgumentNullException(nameof(recipeStore));
    }

    public async Task<MealTemplateDto> ExecuteAsync(SaveMealAsTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var template = BuildTemplate(request);
        return await _recipeStore.SaveMealTemplateAsync(template, cancellationToken);
    }

    public async Task<MealTemplateDto> ExecuteAsync(Guid userId, SaveMealAsTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var template = BuildTemplate(request);
        return await _recipeStore.SaveMealTemplateAsync(userId, template, cancellationToken);
    }

    private static MealTemplateDto BuildTemplate(SaveMealAsTemplateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var domainItems = (request.Items ?? [])
            .Select(item => item is null ? null! : new SavedMealTemplateItem(item.Id, item.Name, item.ServingSize,
                item.ServingUnit, item.ServingsCount, item.Calories, item.Protein, item.Carbs, item.Fats))
            .ToList();
        var draft = SavedMealTemplate.Create(request.TemplateName, (MealType)request.MealType, domainItems);

        return new MealTemplateDto(
            Id: Guid.NewGuid(),
            Name: draft.Name,
            MealType: (int)draft.MealType,
            TotalCalories: draft.TotalCalories,
            TotalProtein: draft.TotalProtein,
            TotalCarbs: draft.TotalCarbs,
            TotalFats: draft.TotalFats,
            ItemCount: draft.Items.Count,
            Items: domainItems.Select(item => new MealTemplateItemDto(item.Id, item.Name,
                item.ServingSize, item.ServingUnit, item.ServingsCount, item.Calories,
                item.Protein, item.Carbs, item.Fats)).ToList(),
            CreatedAtUtc: DateTime.UtcNow
        );
    }
}
