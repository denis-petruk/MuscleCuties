using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Recipes.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.Recipes;

/// <summary>Saving a meal as a template.</summary>
public interface ISaveMealTemplateUseCase
{
    Task<MealTemplateDto> ExecuteAsync(SaveMealAsTemplateRequest request, CancellationToken cancellationToken = default);
    Task<MealTemplateDto> ExecuteAsync(Guid userId, SaveMealAsTemplateRequest request, CancellationToken cancellationToken = default);
    Task<MealTemplateDto> UpdateAsync(Guid userId, Guid id, SaveMealAsTemplateRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Saves a logged meal as a template, refusing one with no foods.</summary>
public sealed class SaveMealTemplateUseCase : ISaveMealTemplateUseCase
{
    private readonly IRecipeStore _recipeStore;

    /// <summary>Takes the recipe store.</summary>
    public SaveMealTemplateUseCase(IRecipeStore recipeStore)
    {
        _recipeStore = recipeStore ?? throw new ArgumentNullException(nameof(recipeStore));
    }

    /// <summary>Saves the logged meal as a template, refusing one with no foods.</summary>
    public async Task<MealTemplateDto> ExecuteAsync(SaveMealAsTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var template = BuildTemplate(request);
        return await _recipeStore.SaveMealTemplateAsync(template, cancellationToken);
    }

    /// <summary>Saves the logged meal as a template, refusing one with no foods.</summary>
    public async Task<MealTemplateDto> ExecuteAsync(Guid userId, SaveMealAsTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var template = BuildTemplate(request);
        return await _recipeStore.SaveMealTemplateAsync(userId, template, cancellationToken);
    }

    /// <summary>Replaces the template's foods from the meal she chose.</summary>
    public async Task<MealTemplateDto> UpdateAsync(Guid userId, Guid id, SaveMealAsTemplateRequest request, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("A template ID is required.", nameof(id));
        ArgumentNullException.ThrowIfNull(request);

        var existing = await _recipeStore.GetMealTemplateByIdAsync(userId, id, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Meal template was not found for this user.");
        var updated = BuildTemplate(request) with { Id = existing.Id, CreatedAtUtc = existing.CreatedAtUtc };
        return await _recipeStore.UpdateMealTemplateAsync(userId, updated, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Builds the template from the meal's foods, keeping the amounts she actually ate.</summary>
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
