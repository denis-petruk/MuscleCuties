using System.Collections.ObjectModel;
using System.Globalization;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.UI.Nutrition;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Nutrition.Inputs;

namespace MuscleCuties.Core.ViewModels.Nutrition;

public partial class NutritionViewModel
{
    private void AddSelectedFoodAsIngredient()
    {
        if (SelectedFoodResult is null)
        {
            AddFoodMessage = "Choose an ingredient first.";
            return;
        }

        if (SelectedServingOption is null)
        {
            AddFoodMessage = "Choose a serving option.";
            return;
        }

        if (!TryParseAmount(FoodGrams, out var amount))
        {
            AddFoodMessage = "Enter amount greater than zero.";
            return;
        }

        if (SelectedFoodResult.Calories <= 0f)
        {
            AddFoodMessage = "Choose a product with calories listed.";
            return;
        }

        AddOrUpdateIngredient(CreateIngredient(SelectedFoodResult, amount, SelectedServingOption));
        AddFoodMessage = $"{SelectedFoodResult.Name} added to this meal.";
        SelectedFoodResult = null;
        SearchQuery = string.Empty;
        ServingOptions = [];
        SelectedServingOption = null;
        FoodGrams = string.Empty;
        FoodSearchResults = [];
        ResetFoodSearchPaging();
        IsFoodFinderExpanded = false;
    }

    private void RemoveIngredient(MealIngredientItem? ingredient)
    {
        if (ingredient is null)
            return;

        MealIngredients.Remove(ingredient);
        if (MealIngredients.Count == 0)
            IsFoodFinderExpanded = false;
    }

    private async Task LogMealAsync()
    {
        if (MealIngredients.Count == 0)
        {
            AddFoodMessage = "Add at least one ingredient to the meal.";
            return;
        }

        IsBusy = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var loggedAt = DateTime.Today.Add(SelectedMealTime);
            var ingredients = MealIngredients
                .Select(i => new MealIngredientInput(i.FoodItemId, i.Grams))
                .ToList();

            if (IsEditingMeal)
            {
                await _nutritionService.UpdateMealAsync(
                    userId,
                    _editingMealId,
                    ingredients,
                    SelectedMealType,
                    loggedAt);
                AddFoodMessage = $"{SelectedMealType} updated at {loggedAt:h:mm tt}.";
            }
            else
            {
                await _nutritionService.LogMealAsync(
                    userId,
                    ingredients,
                    SelectedMealType,
                    loggedAt);
                AddFoodMessage = $"{SelectedMealType} logged at {loggedAt:h:mm tt}.";
            }

            TriggerCelebration();
            IsAddFoodPanelVisible = false;
            IsCustomFoodPanelVisible = false;
            SelectedFoodResult = null;
            IsFoodFinderExpanded = false;
            SearchQuery = string.Empty;
            FoodSearchResults = [];
            ResetFoodSearchPaging();
            FoodGrams = string.Empty;
            MealIngredients.Clear();
            IsEditingMeal = false;
            _editingMealId = 0;

            _loadGate.MarkStale();
            await _loadGate.RunAsync(LoadDataCoreAsync, force: true);
        }
        catch (InvalidOperationException ex)
        {
            AddFoodMessage = ex.Message;
        }
        catch (ArgumentException ex)
        {
            AddFoodMessage = ex.Message;
        }
        catch
        {
            AddFoodMessage = "Could not log this meal. Please refresh and try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task EditMealAsync(MealItem? meal)
    {
        if (meal is null)
            return;

        IsBusy = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var loggedMeal = await _nutritionService.GetLoggedMealAsync(userId, meal.LoggedMealId);

            if (loggedMeal is null)
            {
                AddFoodMessage = "Could not find that meal. Please refresh and try again.";
                return;
            }

            BeginMealEdit(loggedMeal);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyReadyMealTemplate(MealTemplateItem? template)
    {
        if (template is null || template.Ingredients.Count == 0)
            return;

        MealIngredients.Clear();
        foreach (var ingredient in template.Ingredients)
            MealIngredients.Add(CopyIngredient(ingredient));

        SelectedMealType = template.MealType;
        if (!IsEditingMeal)
            SelectedMealTime = DateTime.Now.TimeOfDay;

        IsAddFoodPanelVisible = true;
        IsFoodFinderExpanded = false;
        SelectedFoodResult = null;
        SearchQuery = string.Empty;
        FoodSearchResults = [];
        ResetFoodSearchPaging();
        AddFoodMessage = $"{template.Name} added. Adjust the time or log it.";
        NotifyMealIngredientProperties();
    }

    private void BeginMealEdit(LoggedMeal meal)
    {
        MealIngredients.Clear();
        foreach (var entry in meal.Entries.Where(entry => entry.FoodItem is not null))
            MealIngredients.Add(CreateIngredient(entry.FoodItem!, entry.Grams));

        _editingMealId = meal.Id;
        IsEditingMeal = true;
        SelectedMealType = meal.MealType;
        SelectedMealTime = meal.LoggedAt.TimeOfDay;
        IsAddFoodPanelVisible = true;
        IsFoodFinderExpanded = false;
        SelectedFoodResult = null;
        SearchQuery = string.Empty;
        FoodSearchResults = [];
        ResetFoodSearchPaging();
        AddFoodMessage = "Edit the ingredients, time, or meal type.";
        NotifyMealIngredientProperties();
    }

    private void ResetMealDraft()
    {
        _editingMealId = 0;
        IsEditingMeal = false;
        SelectedFoodResult = null;
        SearchQuery = string.Empty;
        ServingOptions = [];
        SelectedServingOption = null;
        FoodGrams = string.Empty;
        FoodSearchResults = [];
        ResetFoodSearchPaging();
        IsFoodFinderExpanded = false;
        IsCustomFoodPanelVisible = false;
        MealIngredients.Clear();
        AddFoodMessage = string.Empty;
        NotifyMealIngredientProperties();
    }

    private void AddOrUpdateIngredient(MealIngredientItem ingredient)
    {
        var existing = MealIngredients.FirstOrDefault(i => i.FoodItemId == ingredient.FoodItemId);
        if (existing is null)
        {
            MealIngredients.Add(ingredient);
            return;
        }

        var index = MealIngredients.IndexOf(existing);
        MealIngredients[index] = CreateIngredient(existing, existing.Grams + ingredient.Grams);
        NotifyMealIngredientProperties();
    }

    private static MealIngredientItem CreateIngredient(
        FoodSearchResultItem food,
        float amount,
        FoodServingOptionItem servingOption)
    {
        var grams = amount * servingOption.Grams;
        return new MealIngredientItem
        {
            FoodItemId = food.FoodItemId,
            Name = food.Name,
            Grams = grams,
            Amount = amount,
            ServingLabel = servingOption.Label,
            Calories = food.Calories,
            Protein = food.Protein,
            Carbs = food.Carbs,
            Fats = food.Fats,
            SourceSummary = food.SourceSummary
        };
    }

    private static MealIngredientItem CreateIngredient(MealIngredientItem ingredient, float grams) =>
        new()
        {
            FoodItemId = ingredient.FoodItemId,
            Name = ingredient.Name,
            Grams = grams,
            Amount = grams,
            ServingLabel = "g",
            Calories = ingredient.Calories,
            Protein = ingredient.Protein,
            Carbs = ingredient.Carbs,
            Fats = ingredient.Fats,
            SourceSummary = ingredient.SourceSummary
        };

    private static MealIngredientItem CreateIngredient(FoodItem food, float grams) =>
        new()
        {
            FoodItemId = food.Id,
            Name = food.Name,
            Grams = grams,
            Amount = grams,
            ServingLabel = "g",
            Calories = food.Calories,
            Protein = food.Protein,
            Carbs = food.Carbs,
            Fats = food.Fats,
            SourceSummary = BuildSourceSummary(food)
        };

    private static MealIngredientItem CopyIngredient(MealIngredientItem ingredient) =>
        new()
        {
            FoodItemId = ingredient.FoodItemId,
            Name = ingredient.Name,
            Grams = ingredient.Grams,
            Amount = ingredient.Amount,
            ServingLabel = ingredient.ServingLabel,
            Calories = ingredient.Calories,
            Protein = ingredient.Protein,
            Carbs = ingredient.Carbs,
            Fats = ingredient.Fats,
            SourceSummary = ingredient.SourceSummary
        };

    private async Task<MacroNutrients> LoadMealsAsync(int userId)
    {
        var meals = await _nutritionService.GetLoggedMealsByDateAsync(userId, DateTime.Today);
        var mealList = meals.ToList();
        var allEntries = meals.SelectMany(meal => meal.Entries).ToList();

        ReplaceMeals(mealList.Select(meal => BuildMealItem(meal, mealList)));

        Micronutrients = new ObservableCollection<DailyMicronutrientItem>(
            BuildMicronutrients(mealList, _micronutrientGoals));

        return MacroNutrients.SumMealEntries(allEntries);
    }

    private async Task LoadReadyMealTemplatesAsync(int userId, CyclePhase phase)
    {
        var templates = await _nutritionService.GetReadyMealTemplatesAsync(userId, phase, DateTime.Today) ?? [];
        ReadyMealTemplates = new ObservableCollection<MealTemplateItem>(
            templates.Select(BuildMealTemplateItem));
    }

    private void ReplaceMeals(IEnumerable<MealItem> meals)
    {
        Meals.Clear();

        foreach (var meal in meals)
            Meals.Add(meal);

        OnPropertyChanged(nameof(HasMeals));
        OnPropertyChanged(nameof(HasNoMeals));
    }

    private MealItem BuildMealItem(LoggedMeal meal, IReadOnlyCollection<LoggedMeal> dailyMeals)
    {
        var entries = meal.Entries.Where(e => e.FoodItem is not null).ToList();
        var macros = MacroNutrients.SumMealEntries(entries);
        var micronutrients = BuildMicronutrients([meal], dailyMeals, _micronutrientGoals).ToList();

        return new MealItem
        {
            LoggedMealId = meal.Id,
            Time = meal.LoggedAt.ToString("h:mm tt", CultureInfo.CurrentCulture),
            MealType = meal.MealType.ToString().ToUpperInvariant(),
            Name = BuildMealCardName(entries),
            CaloriesText = $"{macros.Calories:N0} kcal",
            MacrosText = macros.ToMacroText(),
            FiberText = BuildFiberText(micronutrients),
            NutrientSummaryText = BuildMicronutrientSummaryText(micronutrients),
            Macros = macros,
            MacroItems = new ObservableCollection<MacroBreakdownItem>(BuildMacroBreakdownItems(macros)),
            Micronutrients = new ObservableCollection<DailyMicronutrientItem>(micronutrients)
        };
    }

    private static string BuildMealCardName(IReadOnlyList<LoggedMealEntry> entries)
    {
        var names = entries
            .Select(entry => ShortenIngredientName(entry.FoodItem!.Name))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return names.Count switch
        {
            0 => "Logged meal",
            1 => names[0],
            2 => $"{names[0]} + {names[1]}",
            _ => $"{names[0]} + {names[1]} + {names.Count - 2} more"
        };
    }

    private static string ShortenIngredientName(string name)
    {
        var cleanName = name
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? name.Trim();

        return cleanName.Length <= 24 ? cleanName : $"{cleanName[..21]}...";
    }

    private static MealTemplateItem BuildMealTemplateItem(MealTemplate template)
    {
        var ingredients = template.Entries
            .Where(entry => entry.FoodItem is not null)
            .Select(entry => CreateIngredient(entry.FoodItem!, entry.Grams))
            .ToList();
        var calories = ingredients.Sum(ingredient => ingredient.CaloriesForAmount);
        var ingredientLabel = ingredients.Count == 1 ? "ingredient" : "ingredients";

        return new MealTemplateItem
        {
            MealTemplateId = template.Id,
            Name = template.Name,
            Description = template.Description ?? string.Empty,
            MealType = template.MealType,
            SummaryText = $"{ingredients.Count} {ingredientLabel} · {calories:N0} kcal",
            Ingredients = ingredients
        };
    }

    private void NotifyMealIngredientProperties()
    {
        OnPropertyChanged(nameof(HasMealIngredients));
        NotifyFoodFinderProperties();
        OnPropertyChanged(nameof(MealIngredientsTotalText));
        OnPropertyChanged(nameof(LogMealButtonText));
        LogMealCommand.NotifyCanExecuteChanged();
    }

    private bool CanAddIngredient() =>
        !IsBusy &&
        SelectedFoodResult is not null &&
        SelectedServingOption is not null &&
        HasCalories(SelectedFoodResult.Calories) &&
        TryParseAmount(FoodGrams, out _);

    private bool CanLogMeal() =>
        !IsBusy && MealIngredients.Count > 0;
}
