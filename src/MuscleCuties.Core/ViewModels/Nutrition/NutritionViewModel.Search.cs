using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Models.UI.Nutrition;
using MuscleCuties.Core.Services.Nutrition;

namespace MuscleCuties.Core.ViewModels.Nutrition;

public partial class NutritionViewModel
{
    private const int FoodSearchPageSize = 15;
    private string _activeFoodSearchQuery = string.Empty;

    private int _foodSearchPageNumber;

    private async Task SearchFoodAsync()
    {
        if (!CanSearchFood())
            return;

        IsBusy = true;
        AddFoodMessage = string.Empty;
        SelectedFoodResult = null;

        try
        {
            await LoadFoodSearchPageAsync(SearchQuery.Trim(), 1, true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task BrowseMoreFoodsAsync()
    {
        if (!CanBrowseMoreFoodResults())
            return;

        IsBrowsingMoreFoods = true;
        AddFoodMessage = string.Empty;

        try
        {
            await LoadFoodSearchPageAsync(_activeFoodSearchQuery, _foodSearchPageNumber + 1, false);
        }
        finally
        {
            IsBrowsingMoreFoods = false;
        }
    }

    private async Task LoadFoodSearchPageAsync(string query, int pageNumber, bool replaceResults)
    {
        using var scope = _scopeFactory.CreateScope();
        var nutritionService = scope.ServiceProvider.GetRequiredService<INutritionService>();

        var foods = await nutritionService.SearchFoodItemsAsync(query, FoodSearchPageSize, pageNumber);
        var items = foods.Select(CreateFoodSearchResultItem).ToList();

        if (replaceResults)
        {
            FoodSearchResults = new ObservableCollection<FoodSearchResultItem>(items);
        }
        else
        {
            var existingIds = FoodSearchResults.Select(item => item.FoodItemId).ToHashSet();
            var merged = FoodSearchResults
                .Concat(items.Where(item => existingIds.Add(item.FoodItemId)))
                .ToList();
            FoodSearchResults = new ObservableCollection<FoodSearchResultItem>(merged);
        }

        _activeFoodSearchQuery = query;
        _foodSearchPageNumber = pageNumber;
        HasMoreFoodResults = foods.Count >= FoodSearchPageSize;

        AddFoodMessage = FoodSearchResults.Count == 0
            ? "No complete nutrition matches found. Try a simpler name, like carrot or oil."
            : !replaceResults && items.Count == 0
                ? "No more foods found for this search."
                : string.Empty;
        IsFoodSearchModalVisible = FoodSearchResults.Count > 0;
    }

    private void SelectFoodResult(FoodSearchResultItem? food)
    {
        if (food is null)
            return;

        SelectedFoodResult = food;
        IsFoodSearchModalVisible = false;
        ResetFoodSearchPaging();
        AddFoodMessage = string.Empty;
    }

    private void DismissFoodSearchResults()
    {
        IsFoodSearchModalVisible = false;
        ResetFoodSearchPaging();
    }

    private void ResetFoodSearchPaging()
    {
        _foodSearchPageNumber = 0;
        _activeFoodSearchQuery = string.Empty;
        HasMoreFoodResults = false;
    }

    private bool CanSearchFood()
    {
        return !IsBusy && !IsBrowsingMoreFoods && !string.IsNullOrWhiteSpace(SearchQuery);
    }

    private bool CanBrowseMoreFoodResults()
    {
        return !IsBusy &&
               !IsBrowsingMoreFoods &&
               HasFoodSearchResults &&
               HasMoreFoodResults &&
               !string.IsNullOrWhiteSpace(_activeFoodSearchQuery);
    }
}
