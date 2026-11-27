// Recipe creation, ingredient rows, and saved-recipe loading.
// Second half of FoodLogViewModel; see its declaration for the full contract.

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text;
using MuscleCuties.UI.Services.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.Recipes.Contracts;

namespace MuscleCuties.UI.ViewModels.Nutrition;

/// <summary>The recipe builder: ingredients from the catalog or by hand, a live preview of the totals, and loading a</summary>
/// saved recipe back into the editor.</summary>
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
    private RecipeDetailsItem? _recipePreview;

    /// <summary>The ingredients being edited.</summary>
    public ObservableCollection<RecipeIngredientEditor> RecipeEditorIngredients { get; } = [];
    /// <summary>True when recipe creation is available.</summary>
    public bool CanCreateRecipes => _createRecipeUseCase is not null;
    /// <summary>True when a preview is shown.</summary>
    public bool HasRecipePreview => RecipePreview is not null;
    /// <summary>The recipe's totals as one line, or the prompt to add ingredients while it has none.</summary>
    public string RecipePreviewSummary => RecipePreview is { } preview
        ? string.Format(CultureInfo.CurrentCulture, RecipePreviewFormats.GetOrAdd(UiTextCatalog.Get("Nutrition_Recipe_PreviewSummary"), CompositeFormat.Parse),
            preview.PerServingCalories, preview.PerServingProtein, preview.PerServingCarbs,
            preview.PerServingFats, preview.PerServingGrams)
        : string.Empty;

    /// <summary>Opens the recipe builder.</summary>
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

    /// <summary>Fills the builder from a saved recipe, so an existing recipe is edited rather than recreated.</summary>
    public void LoadSavedRecipeIntoEditor(RecipeDetailsItem? details)
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

    /// <summary>Points an ingredient row at a catalog food, carrying its macros and weight across so the row's totals</summary>
    /// update without a second lookup.</summary>
    public void SelectCatalogFoodForRecipeIngredient(RecipeIngredientEditor? row, FoodCatalogItem? catalogItem, float? gramsOverride = null)
    {
        if (row is null || catalogItem is null || IsWriting || !IsRecipeBuilderVisible) return;
        if (!catalogItem.Calories.HasValue || !catalogItem.Protein.HasValue ||
            !catalogItem.Carbs.HasValue || !catalogItem.Fats.HasValue)
        {
            RecipeEditorStatus = "Nutrition_Recipe_ValidationError";
            return;
        }

        var servingConversion = catalogItem.ServingSize is { } servingSize
            ? _recipePortionUseCase.ConvertMassToGrams(servingSize, catalogItem.ServingSizeUnit)
            : null;
        var servingGrams = gramsOverride ??
            (servingConversion is { Succeeded: true, Grams: { } resolved }
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

    /// <summary>Adds an empty ingredient row.</summary>
    [RelayCommand]
    public void AddRecipeIngredient()
    {
        if (IsWriting || !IsRecipeBuilderVisible) return;
        var row = new RecipeIngredientEditor();
        row.PropertyChanged += OnRecipeIngredientChanged;
        RecipeEditorIngredients.Add(row);
        InvalidateRecipePreview();
    }

    /// <summary>Removes an ingredient row.</summary>
    [RelayCommand]
    public void RemoveRecipeIngredient(RecipeIngredientEditor? row)
    {
        if (IsWriting || row is null || !RecipeEditorIngredients.Contains(row)) return;
        row.PropertyChanged -= OnRecipeIngredientChanged;
        RecipeEditorIngredients.Remove(row);
        InvalidateRecipePreview();
    }

    /// <summary>Closes the builder and drops the draft.</summary>
    [RelayCommand]
    public void CancelRecipeBuilder()
    {
        if (IsWriting) return;
        ResetRecipeBuilder();
        IsFastLoggingVisible = IsEditModalVisible;
    }

    /// <summary>Previews the recipe's nutrition.</summary>
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
            var preview = await NutritionPresentationMapper.PreviewRecipeAsync(
                _createRecipeUseCase, RecipeName,
                string.IsNullOrWhiteSpace(RecipeDescription) ? null : RecipeDescription.Trim(),
                ParseRecipeNumber(RecipeServingsText),
                string.IsNullOrWhiteSpace(RecipeCookedWeightText) ? null : ParseRecipeNumber(RecipeCookedWeightText),
                BuildRecipeIngredients());
            if (revision == _recipeEditorRevision && IsRecipeBuilderVisible)
                RecipePreview = preview;
        }
        catch (FormatException) { RecipeEditorStatus = "Nutrition_Recipe_NumberError"; }
        catch (ArgumentException) { RecipeEditorStatus = "Nutrition_Recipe_ValidationError"; }
        catch { RecipeEditorStatus = "Nutrition_Recipe_PreviewError"; }
        finally { IsWriting = false; }
    }

    /// <summary>Saves the recipe.</summary>
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
            var saved = await NutritionPresentationMapper.SaveRecipeAsync(
                _createRecipeUseCase, EditingRecipeId, EditingRecipeVersion, RecipeName,
                string.IsNullOrWhiteSpace(RecipeDescription) ? null : RecipeDescription.Trim(),
                ParseRecipeNumber(RecipeServingsText),
                string.IsNullOrWhiteSpace(RecipeCookedWeightText) ? null : ParseRecipeNumber(RecipeCookedWeightText),
                BuildRecipeIngredients());

            if (revision != _recipeEditorRevision || !IsRecipeBuilderVisible) return;
            var summary = new RecipeSummaryItem(saved.Id, saved.Name,
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
            ShowStatus(UiTextCatalog.Get("Nutrition_Recipe_Saved"));
        }
        catch (FormatException) { RecipeEditorStatus = "Nutrition_Recipe_NumberError"; }
        catch (ArgumentException) { RecipeEditorStatus = "Nutrition_Recipe_ValidationError"; }
        catch { RecipeEditorStatus = "Nutrition_Recipe_SaveError"; }
        finally { IsWriting = false; }
    }

    /// <summary>The ingredients as request inputs.</summary>
    private List<RecipeIngredientInput> BuildRecipeIngredients() =>
        RecipeEditorIngredients.Select(row => new RecipeIngredientInput(
            row.FoodItemId,
            row.Name,
            ParseRecipeNumber(row.Grams),
            ParseRecipeNumber(row.Calories),
            ParseRecipeNumber(row.Protein),
            ParseRecipeNumber(row.Carbs),
            ParseRecipeNumber(row.Fats),
            SourceKind: row.SourceKind,
            SourceLabel: row.SourceLabel,
            SourceVersion: row.SourceVersion)).ToList();

    /// <summary>The catalog food's identifier when it is one the recipe can reference, otherwise empty.</summary>
    private static Guid TryExtractCatalogGuid(FoodCatalogItem item)
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

    /// <summary>Reads the number, or zero when unreadable.</summary>
    private static float ParseRecipeNumber(string text) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value)
            ? value
            : throw new FormatException("Invalid recipe number.");

    /// <summary>Drops the stale preview when an ingredient changes.</summary>
    private void OnRecipeIngredientChanged(object? sender, PropertyChangedEventArgs args) => InvalidateRecipePreview();
    /// <summary>Drops the stale preview when the name changes.</summary>
    partial void OnRecipeNameChanged(string value) => InvalidateRecipePreview();
    /// <summary>Drops the stale preview when the description changes.</summary>
    partial void OnRecipeDescriptionChanged(string value) => InvalidateRecipePreview();
    /// <summary>Drops the stale preview when the servings change.</summary>
    partial void OnRecipeServingsTextChanged(string value) => InvalidateRecipePreview();
    /// <summary>Drops the stale preview when the cooked weight changes.</summary>
    partial void OnRecipeCookedWeightTextChanged(string value) => InvalidateRecipePreview();

    /// <summary>Marks the preview stale after an ingredient changed, so it is recomputed before it is shown again.</summary>
    private void InvalidateRecipePreview()
    {
        _recipeEditorRevision++;
        RecipePreview = null;
        RecipeEditorStatus = string.Empty;
    }

    /// <summary>Empties the builder — name, ingredients and preview — before a new recipe.</summary>
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
