using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.Text;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UI.Models.UI.Nutrition;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.Recipes.Contracts;

namespace MuscleCuties.UI.ViewModels.Nutrition;

public partial class FoodLogViewModel
{
    private const float InitialRecipeServings = 1f;
    private const float DefaultCatalogRecipeGrams = 100f;
    private const int FirstSavedRecipeIndex = 0;
    private const int InitialRecipeConcurrencyVersion = 1;
    private int _recipeEditorRevision;
    private static readonly ConcurrentDictionary<string, CompositeFormat> RecipePreviewFormats = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsManualEditorVisible))]
    private bool _isRecipeBuilderVisible;
    [ObservableProperty] private Guid? _editingRecipeId;
    [ObservableProperty] private int _editingRecipeVersion = InitialRecipeConcurrencyVersion;
    [ObservableProperty] private string _recipeName = string.Empty;
    [ObservableProperty] private string _recipeDescription = string.Empty;
    [ObservableProperty] private string _recipeServingsText = InitialRecipeServings.ToString(CultureInfo.CurrentCulture);
    [ObservableProperty] private string _recipeCookedWeightText = string.Empty;
    [ObservableProperty] private string _recipeEditorStatus = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRecipePreview), nameof(RecipePreviewSummary))]
    private RecipeDetailsDto? _recipePreview;

    [ObservableProperty] private string _selectedRecipePortionServingsText = InitialRecipeServings.ToString(CultureInfo.CurrentCulture);
    [ObservableProperty] private string _selectedRecipePortionGramsText = string.Empty;
    [ObservableProperty] private RecipePortionPreviewDto? _selectedRecipePortionPreview;

    public ObservableCollection<RecipeIngredientEditor> RecipeEditorIngredients { get; } = [];
    public bool CanCreateRecipes => _createRecipeUseCase is not null;
    public bool HasRecipePreview => RecipePreview is not null;
    public string RecipePreviewSummary => RecipePreview is { } preview
        ? string.Format(CultureInfo.CurrentCulture, RecipePreviewFormats.GetOrAdd(UiTextCatalog.Translate("Nutrition_Recipe_PreviewSummary"), CompositeFormat.Parse),
            preview.PerServingCalories, preview.PerServingProtein, preview.PerServingCarbs,
            preview.PerServingFats, preview.PerServingGrams)
        : string.Empty;

    [RelayCommand]
    public void OpenRecipeBuilder()
    {
        if (IsWriting || IsLoading || !IsEditModalVisible || !CanCreateRecipes) return;
        ResetRecipeBuilder();
        IsFastLoggingVisible = false;
        IsQuickAddCaloriesVisible = false;
        IsBarcodeScannerVisible = false;
        IsRecipeBuilderVisible = true;
        AddRecipeIngredient();
        CancelFoodSearch();
    }

    [RelayCommand]
    public void LoadSavedRecipeIntoEditor(RecipeDetailsDto? details)
    {
        if (details is null || IsWriting || !CanCreateRecipes) return;
        ResetRecipeBuilder();
        IsFastLoggingVisible = false;
        IsQuickAddCaloriesVisible = false;
        IsBarcodeScannerVisible = false;
        IsRecipeBuilderVisible = true;
        EditingRecipeId = details.Id;
        EditingRecipeVersion = Math.Max(InitialRecipeConcurrencyVersion, details.ConcurrencyVersion);
        RecipeName = details.Name;
        RecipeDescription = details.Description ?? string.Empty;
        RecipeServingsText = details.ServingsCount.ToString(CultureInfo.CurrentCulture);
        RecipeCookedWeightText = details.CookedWeightGrams.HasValue
            ? details.CookedWeightGrams.Value.ToString(CultureInfo.CurrentCulture)
            : string.Empty;

        foreach (var ing in details.Ingredients)
        {
            var row = new RecipeIngredientEditor();
            if (ing.FoodItemId.HasValue && ing.FoodItemId.Value != Guid.Empty)
            {
                row.ApplyCatalogSnapshot(
                    ing.FoodItemId.Value,
                    ing.Name,
                    ing.Grams,
                    ing.Calories,
                    ing.Protein,
                    ing.Carbs,
                    ing.Fats,
                    ing.SourceKind ?? "Catalog",
                    ing.SourceLabel,
                    ing.SourceVersion);
            }
            else
            {
                row.Name = ing.Name;
                row.Grams = ing.Grams.ToString(CultureInfo.CurrentCulture);
                row.Calories = ing.Calories.ToString(CultureInfo.CurrentCulture);
                row.Protein = ing.Protein.ToString(CultureInfo.CurrentCulture);
                row.Carbs = ing.Carbs.ToString(CultureInfo.CurrentCulture);
                row.Fats = ing.Fats.ToString(CultureInfo.CurrentCulture);
            }
            row.PropertyChanged += OnRecipeIngredientChanged;
            RecipeEditorIngredients.Add(row);
        }
        InvalidateRecipePreview();
    }

    public void SelectCatalogFoodForRecipeIngredient(RecipeIngredientEditor? row, FoodCatalogItemDto? catalogItem, float? gramsOverride = null)
    {
        if (row is null || catalogItem is null || IsWriting || !IsRecipeBuilderVisible) return;
        if (!catalogItem.Calories.HasValue || !catalogItem.Protein.HasValue ||
            !catalogItem.Carbs.HasValue || !catalogItem.Fats.HasValue)
        {
            RecipeEditorStatus = "Nutrition_Recipe_ValidationError";
            return;
        }

        var servingGrams = gramsOverride
            ?? (catalogItem.ServingSize.HasValue &&
                FoodPortionMassRule.TryConvertMassToGrams(catalogItem.ServingSize.Value, catalogItem.ServingSizeUnit, out var resolved)
                ? resolved
                : DefaultCatalogRecipeGrams);
        var scale = servingGrams / DefaultCatalogRecipeGrams;
        var stableFoodId = TryExtractCatalogGuid(catalogItem);
        row.ApplyCatalogSnapshot(
            stableFoodId,
            catalogItem.Name,
            servingGrams,
            catalogItem.Calories.Value * scale,
            catalogItem.Protein.Value * scale,
            catalogItem.Carbs.Value * scale,
            catalogItem.Fats.Value * scale,
            sourceKind: catalogItem.IsCustom ? "CustomFood" : "Catalog",
            sourceLabel: catalogItem.SourceSummary,
            sourceVersion: catalogItem.FoodKey);
        InvalidateRecipePreview();
    }

    [RelayCommand]
    public void ConvertRecipeIngredientToManual(RecipeIngredientEditor? row)
    {
        if (row is null || IsWriting || !IsRecipeBuilderVisible) return;
        row.ConvertToManual();
        InvalidateRecipePreview();
    }

    [RelayCommand]
    public void PreviewSelectedRecipePortion(RecipeSummaryDto? recipe)
    {
        if (recipe is null)
        {
            SelectedRecipePortionPreview = null;
            return;
        }

        try
        {
            float? requestedServings = !string.IsNullOrWhiteSpace(SelectedRecipePortionServingsText)
                ? ParseRecipeNumber(SelectedRecipePortionServingsText)
                : null;
            float? requestedGrams = requestedServings is null && !string.IsNullOrWhiteSpace(SelectedRecipePortionGramsText)
                ? ParseRecipeNumber(SelectedRecipePortionGramsText)
                : null;

            var batchWeight = MathF.Max(1f, recipe.PerServingGrams * recipe.ServingsCount);
            var batch = new RecipeBatchTotals(
                recipe.ServingsCount,
                batchWeight,
                batchWeight,
                recipe.PerServingCalories * recipe.ServingsCount,
                recipe.PerServingProtein * recipe.ServingsCount,
                recipe.PerServingCarbs * recipe.ServingsCount,
                recipe.PerServingNetCarbs * recipe.ServingsCount,
                recipe.PerServingFats * recipe.ServingsCount);
            var portion = RecipeBatchCalculationRule.CalculatePortion(batch, requestedServings, requestedGrams);
            SelectedRecipePortionPreview = new RecipePortionPreviewDto(
                recipe.Id,
                recipe.Name,
                batch.ServingsCount,
                batch.TotalRawWeightGrams,
                batch.CookedWeightGrams,
                batch.TotalCalories,
                batch.TotalProtein,
                batch.TotalCarbs,
                batch.TotalNetCarbs,
                batch.TotalFats,
                portion.SelectedServingsCount,
                portion.SelectedGrams,
                portion.Calories,
                portion.Protein,
                portion.Carbs,
                portion.NetCarbs,
                portion.Fats);
        }
        catch
        {
            SelectedRecipePortionPreview = null;
        }
    }

    [RelayCommand]
    public void AddRecipeIngredient()
    {
        if (IsWriting || !IsRecipeBuilderVisible) return;
        var row = new RecipeIngredientEditor();
        row.PropertyChanged += OnRecipeIngredientChanged;
        RecipeEditorIngredients.Add(row);
        InvalidateRecipePreview();
    }

    [RelayCommand]
    public void RemoveRecipeIngredient(RecipeIngredientEditor? row)
    {
        if (IsWriting || row is null || !RecipeEditorIngredients.Contains(row)) return;
        row.PropertyChanged -= OnRecipeIngredientChanged;
        RecipeEditorIngredients.Remove(row);
        InvalidateRecipePreview();
    }

    [RelayCommand]
    public void CancelRecipeBuilder()
    {
        if (IsWriting) return;
        ResetRecipeBuilder();
        IsFastLoggingVisible = IsEditModalVisible;
    }

    [RelayCommand]
    public async Task PreviewRecipeAsync()
    {
        if (IsWriting || !IsRecipeBuilderVisible || _createRecipeUseCase is null) return;
        var revision = _recipeEditorRevision;
        IsWriting = true;
        RecipePreview = null;
        RecipeEditorStatus = string.Empty;
        try
        {
            var preview = await _createRecipeUseCase.PreviewAsync(BuildRecipeRequest());
            if (revision == _recipeEditorRevision && IsRecipeBuilderVisible)
                RecipePreview = preview;
        }
        catch (FormatException) { RecipeEditorStatus = "Nutrition_Recipe_NumberError"; }
        catch (ArgumentException) { RecipeEditorStatus = "Nutrition_Recipe_ValidationError"; }
        catch { RecipeEditorStatus = "Nutrition_Recipe_PreviewError"; }
        finally { IsWriting = false; }
    }

    [RelayCommand]
    public async Task SaveRecipeAsync()
    {
        if (IsWriting || !IsRecipeBuilderVisible || _createRecipeUseCase is null) return;
        if (!HasRecipePreview)
        {
            RecipeEditorStatus = "Nutrition_Recipe_PreviewFirst";
            return;
        }

        var revision = _recipeEditorRevision;
        IsWriting = true;
        try
        {
            var createRequest = BuildRecipeRequest();
            RecipeDetailsDto saved;
            if (EditingRecipeId.HasValue && EditingRecipeId.Value != Guid.Empty)
            {
                saved = await _createRecipeUseCase.UpdateAsync(new UpdateRecipeRequest(
                    EditingRecipeId.Value,
                    EditingRecipeVersion,
                    createRequest.Name,
                    createRequest.Description,
                    createRequest.ServingsCount,
                    createRequest.CookedWeightGrams,
                    createRequest.Ingredients));
            }
            else
            {
                saved = await _createRecipeUseCase.ExecuteAsync(createRequest);
            }

            if (revision != _recipeEditorRevision || !IsRecipeBuilderVisible) return;
            var summary = new RecipeSummaryDto(saved.Id, saved.Name,
                saved.ServingsCount, saved.PerServingCalories, saved.PerServingProtein, saved.PerServingCarbs,
                saved.PerServingNetCarbs, saved.PerServingFats, saved.PerServingGrams, saved.Ingredients.Count,
                saved.CreatedAtUtc, saved.ConcurrencyVersion);
            var existingIdx = SavedRecipes.ToList().FindIndex(r => r.Id == saved.Id);
            if (existingIdx >= 0)
                SavedRecipes[existingIdx] = summary;
            else
                SavedRecipes.Insert(FirstSavedRecipeIndex, summary);

            NotifyShelfVisibility();
            ResetRecipeBuilder();
            IsFastLoggingVisible = IsEditModalVisible;
            ShowStatus(UiTextCatalog.Translate("Nutrition_Recipe_Saved"));
        }
        catch (FormatException) { RecipeEditorStatus = "Nutrition_Recipe_NumberError"; }
        catch (ArgumentException) { RecipeEditorStatus = "Nutrition_Recipe_ValidationError"; }
        catch { RecipeEditorStatus = "Nutrition_Recipe_SaveError"; }
        finally { IsWriting = false; }
    }

    private CreateRecipeRequest BuildRecipeRequest() => new(
        RecipeName,
        string.IsNullOrWhiteSpace(RecipeDescription) ? null : RecipeDescription.Trim(),
        ParseRecipeNumber(RecipeServingsText),
        string.IsNullOrWhiteSpace(RecipeCookedWeightText) ? null : ParseRecipeNumber(RecipeCookedWeightText),
        RecipeEditorIngredients.Select(row => new RecipeIngredientInputDto(
            row.FoodItemId,
            row.Name,
            ParseRecipeNumber(row.Grams),
            ParseRecipeNumber(row.Calories),
            ParseRecipeNumber(row.Protein),
            ParseRecipeNumber(row.Carbs),
            ParseRecipeNumber(row.Fats),
            SourceKind: row.SourceKind,
            SourceLabel: row.SourceLabel,
            SourceVersion: row.SourceVersion)).ToList());

    private static Guid TryExtractCatalogGuid(FoodCatalogItemDto item)
    {
        if (!string.IsNullOrWhiteSpace(item.FoodKey))
        {
            var idx = item.FoodKey.IndexOf(':');
            if (idx >= 0 && Guid.TryParse(item.FoodKey[(idx + 1)..], out var parsed) && parsed != Guid.Empty)
                return parsed;
        }
        var bytes = new byte[16];
        BitConverter.GetBytes(item.Id).CopyTo(bytes, 0);
        bytes[15] = 1;
        return new Guid(bytes);
    }

    private static float ParseRecipeNumber(string text) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value)
            ? value
            : throw new FormatException("Invalid recipe number.");

    private void OnRecipeIngredientChanged(object? sender, PropertyChangedEventArgs args) => InvalidateRecipePreview();
    partial void OnRecipeNameChanged(string value) => InvalidateRecipePreview();
    partial void OnRecipeDescriptionChanged(string value) => InvalidateRecipePreview();
    partial void OnRecipeServingsTextChanged(string value) => InvalidateRecipePreview();
    partial void OnRecipeCookedWeightTextChanged(string value) => InvalidateRecipePreview();

    private void InvalidateRecipePreview()
    {
        _recipeEditorRevision++;
        RecipePreview = null;
        RecipeEditorStatus = string.Empty;
    }

    private void ResetRecipeBuilder()
    {
        _recipeEditorRevision++;
        IsRecipeBuilderVisible = false;
        EditingRecipeId = null;
        EditingRecipeVersion = InitialRecipeConcurrencyVersion;
        foreach (var row in RecipeEditorIngredients) row.PropertyChanged -= OnRecipeIngredientChanged;
        RecipeEditorIngredients.Clear();
        RecipeName = string.Empty;
        RecipeDescription = string.Empty;
        RecipeServingsText = InitialRecipeServings.ToString(CultureInfo.CurrentCulture);
        RecipeCookedWeightText = string.Empty;
        RecipePreview = null;
        RecipeEditorStatus = string.Empty;
    }
}
