using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Models.UI.Nutrition;
using MuscleCuties.Core.Services.Nutrition;

namespace MuscleCuties.Core.ViewModels.Nutrition;

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
        await _referenceDataPreparation.EnsureNutritionReadyAsync();
        var items = await RunScopedAsync(async services =>
        {
            var foods = await services.GetRequiredService<INutritionService>()
                .SearchFoodItemsAsync(query, FoodSearchPageSize, pageNumber);
            return foods.Select(CreateFoodSearchResultItem).ToList();
        });
        if (version != _foodSearchVersion || !string.Equals(query, SearchQuery.Trim(), StringComparison.Ordinal))
            return;

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
        HasMoreFoodResults = items.Count >= FoodSearchPageSize;

        AddFoodMessage = FoodSearchResults.Count == 0
            ? "No foods found. Try another name or create a custom food."
            : !replaceResults && items.Count == 0
                ? "No more foods found for this search."
                : string.Empty;
    }

    private void SelectFoodResult(FoodSearchResultItem? food)
    {
        if (food is null)
            return;

        _editingIngredient = null;
        IsCustomFoodPanelVisible = false;
        SelectedFoodResult = food;
        AddFoodMessage = string.Empty;
    }

    private void InvalidateFoodSearch()
    {
        _foodSearchVersion++;
        IsSearchingFood = false;
        ResetFoodSearchPaging();
    }

    partial void OnIsSearchingFoodChanged(bool value)
    {
        SearchFoodCommand.NotifyCanExecuteChanged();
        BrowseMoreFoodsCommand.NotifyCanExecuteChanged();
    }

    private void ResetFoodSearchPaging()
    {
        _foodSearchPageNumber = 0;
        _activeFoodSearchQuery = string.Empty;
        HasMoreFoodResults = false;
    }

    private bool CanSearchFood()
    {
        return !IsBusy && !IsSearchingFood && !IsBrowsingMoreFoods && !string.IsNullOrWhiteSpace(SearchQuery);
    }

    private bool CanBrowseMoreFoodResults()
    {
        return !IsBusy &&
               !IsSearchingFood &&
               !IsBrowsingMoreFoods &&
               HasFoodSearchResults &&
               HasMoreFoodResults &&
               !string.IsNullOrWhiteSpace(_activeFoodSearchQuery);
    }
}
