using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using MuscleCuties.UI.Models.UI.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch.Contracts;

namespace MuscleCuties.UI.ViewModels.Nutrition;

public partial class NutritionViewModel
{
    private const int FoodSearchPageSize = 15;
    private string _activeFoodSearchQuery = string.Empty;

    private int _foodSearchPageNumber;
    private int _foodSearchVersion;
    [ObservableProperty] private bool _isSearchingFood;

    private async Task SearchFoodAsync()
    {
        if (!CanSearchFood())
            return;

        IsFoodFinderExpanded = true;
        IsSearchingFood = true;
        AddFoodMessage = string.Empty;
        SelectedFoodResult = null;
        FoodSearchResults = [];
        ResetFoodSearchPaging();
        var version = ++_foodSearchVersion;

        try
        {
            await LoadFoodSearchPageAsync(SearchQuery.Trim(), 1, true, version);
        }
        catch (Exception exception)
        {
            Trace.TraceError($"[Nutrition] Food search failed: {exception.GetType().Name}");
            if (version == _foodSearchVersion)
                AddFoodMessage = "Couldn't load foods. Check your connection and search again.";
        }
        finally
        {
            if (version == _foodSearchVersion)
                IsSearchingFood = false;
        }
    }

    private async Task BrowseMoreFoodsAsync()
    {
        if (!CanBrowseMoreFoodResults())
            return;

        IsBrowsingMoreFoods = true;
        AddFoodMessage = string.Empty;
        var version = _foodSearchVersion;

        try
        {
            await LoadFoodSearchPageAsync(_activeFoodSearchQuery, _foodSearchPageNumber + 1, false, version);
        }
        catch (Exception exception)
        {
            Trace.TraceError($"[Nutrition] More foods failed: {exception.GetType().Name}");
            if (version == _foodSearchVersion)
                AddFoodMessage = "Couldn't load more foods. Your current results are still available.";
        }
        finally
        {
            IsBrowsingMoreFoods = false;
        }
    }

    private async Task LoadFoodSearchPageAsync(string query, int pageNumber, bool replaceResults, int version)
    {
        var response = await _searchUseCase.SearchAsync(
            new FoodCatalogSearchRequest(query, FoodSearchPageSize, pageNumber));

        if (version != _foodSearchVersion || !string.Equals(query, SearchQuery.Trim(), StringComparison.Ordinal))
            return;

        var items = response.Items.Select(CreateFoodSearchResultItem).ToList();

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
        HasMoreFoodResults = response.HasMore;
        NotifyFoodFinderProperties();
        BrowseMoreFoodsCommand.NotifyCanExecuteChanged();
    }

    private void InvalidateFoodSearch()
    {
        _foodSearchVersion++;
        IsSearchingFood = false;
        IsBrowsingMoreFoods = false;
        ResetFoodSearchPaging();
    }

    private void ResetFoodSearchPaging()
    {
        _activeFoodSearchQuery = string.Empty;
        _foodSearchPageNumber = 1;
        HasMoreFoodResults = false;
        BrowseMoreFoodsCommand.NotifyCanExecuteChanged();
    }

    private bool CanSearchFood()
    {
        return !IsBusy && !string.IsNullOrWhiteSpace(SearchQuery);
    }

    private bool CanBrowseMoreFoodResults()
    {
        return !IsBusy &&
               !IsBrowsingMoreFoods &&
               HasMoreFoodResults &&
               !string.IsNullOrWhiteSpace(_activeFoodSearchQuery);
    }
}
