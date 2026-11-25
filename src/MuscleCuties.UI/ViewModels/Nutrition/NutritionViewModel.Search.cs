using MuscleCuties.UI.Services.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch.Contracts;

namespace MuscleCuties.UI.ViewModels.Nutrition;

/// <summary>Food search: paged results over the catalog, a debounce so typing does not fire a request per</summary>
/// keystroke, and a version so a late page cannot overwrite a newer query.</summary>
public partial class NutritionViewModel
{
    private const int FoodSearchPageSize = 15;
    private const int SearchDebounceMilliseconds = 400;
    private CancellationTokenSource? _searchDebounce;
    private string _activeFoodSearchQuery = string.Empty;

    private int _foodSearchPageNumber;
    private int _foodSearchVersion;
    [ObservableProperty] private bool _isSearchingFood;

    /// <summary>Runs the first page for the current query, ignoring a request the page cannot make.</summary>
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
                AddFoodMessage = "Ui_Nutrition_Error_LoadFoods";
        }
        finally
        {
            if (version == _foodSearchVersion)
                IsSearchingFood = false;
        }
    }

    /// <summary>Appends the next page of results to the ones already shown.</summary>
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
                AddFoodMessage = "Ui_Nutrition_Error_LoadMoreFoods";
        }
        finally
        {
            IsBrowsingMoreFoods = false;
        }
    }

    /// <summary>Loads one page and applies it only when it belongs to the current query version, so fast typing cannot</summary>
    /// leave older results on screen.</summary>
    private async Task LoadFoodSearchPageAsync(string query, int pageNumber, bool replaceResults, int version)
    {
        var response = await _searchUseCase.SearchAsync(
            new FoodCatalogSearchRequest(query, FoodSearchPageSize, pageNumber));

        if (version != _foodSearchVersion || !string.Equals(query, SearchQuery.Trim(), StringComparison.Ordinal))
            return;

        var items = response.Items.Select(i => CreateFoodSearchResultItem(
            NutritionPresentationMapper.FromCatalogItem(i))).ToList();

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

    /// <summary>Retires the results in flight, so anything still loading is discarded when it arrives.</summary>
    private void InvalidateFoodSearch()
    {
        _foodSearchVersion++;
        IsSearchingFood = false;
        IsBrowsingMoreFoods = false;
        ResetFoodSearchPaging();
    }

    /// <summary>Returns paging to the first page, so a new query cannot start on the old query's page number.</summary>
    private void ResetFoodSearchPaging()
    {
        _activeFoodSearchQuery = string.Empty;
        _foodSearchPageNumber = 1;
        HasMoreFoodResults = false;
        BrowseMoreFoodsCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    ///     Searching on submit alone meant the keyboard had to be dismissed before a result could be
    ///     seen (D51). Typing now schedules a debounced search, so the hits appear under the field while
    ///     the keys are still up.
    /// </summary>
    private void ScheduleFoodSearch(string value)
    {
        _searchDebounce?.Cancel();
        _searchDebounce?.Dispose();
        _searchDebounce = null;

        if (string.IsNullOrWhiteSpace(value))
            return;

        var cancellation = new CancellationTokenSource();
        _searchDebounce = cancellation;
        _ = DebouncedSearchAsync(cancellation.Token);
    }

    /// <summary>Waits out the debounce before searching, abandoning the request when the user has typed again.</summary>
    private async Task DebouncedSearchAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SearchDebounceMilliseconds, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested)
            return;

        await SearchFoodCommand.ExecuteAsync(null);
    }

    /// <summary>Search runs only with a non-blank query and while the page is idle.</summary>
    private bool CanSearchFood()
    {
        return !IsBusy && !string.IsNullOrWhiteSpace(SearchQuery);
    }

    /// <summary>More results can be loaded only while a page is not already loading and the catalog says there are more.</summary>
    private bool CanBrowseMoreFoodResults()
    {
        return !IsBusy &&
               !IsBrowsingMoreFoods &&
               HasMoreFoodResults &&
               !string.IsNullOrWhiteSpace(_activeFoodSearchQuery);
    }
}
