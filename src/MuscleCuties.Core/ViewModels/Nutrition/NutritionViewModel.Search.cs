using System.Collections.ObjectModel;
using MuscleCuties.Core.Models.UI.Nutrition;

namespace MuscleCuties.Core.ViewModels.Nutrition;

public partial class NutritionViewModel
{
    private const int FoodSearchPageSize = 15;

    private int _foodSearchPageNumber;
    private string _activeFoodSearchQuery = string.Empty;

    private async Task SearchFoodAsync()
    {
        if (!CanSearchFood())
            return;

        IsBusy = true;
        AddFoodMessage = string.Empty;
        SelectedFoodResult = null;

        try
        {
            await LoadFoodSearchPageAsync(SearchQuery.Trim(), 1, replaceResults: true);
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
            await LoadFoodSearchPageAsync(_activeFoodSearchQuery, _foodSearchPageNumber + 1, replaceResults: false);
        }
        finally
        {
            IsBrowsingMoreFoods = false;
        }
    }

    private async Task LoadFoodSearchPageAsync(string query, int pageNumber, bool replaceResults)
    {
        var foods = await _nutritionService.SearchFoodItemsAsync(query, FoodSearchPageSize, pageNumber);
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
    }

    private void SelectFoodResult(FoodSearchResultItem? food)
    {
        if (food is null)
            return;

        SelectedFoodResult = food;
        FoodSearchResults = [];
        ResetFoodSearchPaging();
        AddFoodMessage = string.Empty;
    }

    private void DismissFoodSearchResults()
    {
        FoodSearchResults = [];
        ResetFoodSearchPaging();
    }

    private void ResetFoodSearchPaging()
    {
        _foodSearchPageNumber = 0;
        _activeFoodSearchQuery = string.Empty;
        HasMoreFoodResults = false;
    }

    private bool CanSearchFood() =>
        !IsBusy && !IsBrowsingMoreFoods && !string.IsNullOrWhiteSpace(SearchQuery);

    private bool CanBrowseMoreFoodResults() =>
        !IsBusy &&
        !IsBrowsingMoreFoods &&
        HasFoodSearchResults &&
        HasMoreFoodResults &&
        !string.IsNullOrWhiteSpace(_activeFoodSearchQuery);
}
