using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Repositories.Nutrition;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Nutrition;
using Xunit;

namespace MuscleCuties.Core.Tests.Services.Nutrition;

public class FoodLookupAndNutritionSearchTests
{
    private sealed class StubNutritionRepository : INutritionRepository
    {
        public List<FoodItem> FoodsToReturn { get; set; } = [];
        public int SearchCalls { get; private set; }
        public string? LastQuery { get; private set; }

        public Task<List<FoodItem>> SearchFoodItemsAsync(string query)
        {
            SearchCalls++;
            LastQuery = query;
            return Task.FromResult(FoodsToReturn);
        }

        public Task<FoodItem?> GetByIdAsync(int id) =>
            Task.FromResult(FoodsToReturn.FirstOrDefault(f => f.Id == id));

        public Task<List<FoodItem>> GetAllAsync() => Task.FromResult(FoodsToReturn);
        public Task AddAsync(FoodItem entity) => Task.CompletedTask;
        public Task UpdateAsync(FoodItem entity) => Task.CompletedTask;
        public Task DeleteAsync(FoodItem entity) => Task.CompletedTask;

        public Task<List<FoodItem>> GetFoodItemsByIdsAsync(IEnumerable<int> foodItemIds) =>
            Task.FromResult(FoodsToReturn);

        public Task<FoodItem?> GetFoodItemByFdcIdAsync(int fdcId) =>
            Task.FromResult<FoodItem?>(null);

        public Task<List<FoodItem>> GetFoodItemsByFdcIdsAsync(IEnumerable<int> fdcIds) =>
            Task.FromResult(FoodsToReturn);

        public Task SaveFoodItemsAsync(IReadOnlyCollection<FoodItem> newItems, IReadOnlyCollection<FoodItem> updatedItems) =>
            Task.CompletedTask;

        public Task<List<LoggedMeal>> GetLoggedMealsByDateAsync(int userId, DateTime date) =>
            Task.FromResult<List<LoggedMeal>>([]);

        public Task<List<LoggedMeal>> GetLoggedMealsByDateRangeAsync(int userId, DateTime startDate, DateTime endDate) =>
            Task.FromResult<List<LoggedMeal>>([]);

        public Task<LoggedMeal?> GetLoggedMealAsync(int userId, int loggedMealId) =>
            Task.FromResult<LoggedMeal?>(null);

        public Task AddLoggedMealAsync(LoggedMeal meal) => Task.CompletedTask;
        public Task UpdateLoggedMealAsync(LoggedMeal meal) => Task.CompletedTask;
        public Task DeleteLoggedMealAsync(int userId, int loggedMealId) => Task.CompletedTask;

        public Task<(double Calories, double Protein, double Carbs, double Fat)> GetDailyNutritionLogTotalsAsync(int userId, DateTime date) =>
            Task.FromResult((0.0, 0.0, 0.0, 0.0));
    }

    private sealed class StubFoodSyncService : IFoodSyncService
    {
        public List<FoodItem> SyncResultsToReturn { get; set; } = [];
        public List<FoodItem> RemoteFoodsToReturn { get; set; } = [];
        public bool ThrowInvalidOperationOnSearch { get; set; }
        public int SearchCalls { get; private set; }
        public int SearchRemoteFoodsCalls { get; private set; }

        public Task<List<FoodItem>> SearchAsync(
            string query, int pageSize = 15, int pageNumber = 1, CancellationToken cancellationToken = default)
        {
            SearchCalls++;
            if (ThrowInvalidOperationOnSearch)
                throw new InvalidOperationException("Sync unconfigured");
            return Task.FromResult(SyncResultsToReturn);
        }

        public Task<IReadOnlyList<FdcFoodSearchResult>> SearchRemoteAsync(
            string query, int pageSize = 15, int pageNumber = 1, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FdcFoodSearchResult>>([]);

        public Task<IReadOnlyList<FoodItem>> SearchRemoteFoodsAsync(
            string query, int maxResults = 20, CancellationToken cancellationToken = default)
        {
            SearchRemoteFoodsCalls++;
            return Task.FromResult<IReadOnlyList<FoodItem>>(RemoteFoodsToReturn);
        }

        public Task<FoodItem?> FetchDetailAsync(int fdcId, CancellationToken cancellationToken = default) =>
            Task.FromResult<FoodItem?>(null);
    }

    private sealed class StubUserRepository : IUserRepository
    {
        public Task<User?> GetByIdAsync(int id) => Task.FromResult<User?>(null);
        public Task<List<User>> GetAllAsync() => Task.FromResult<List<User>>([]);
        public Task AddAsync(User entity) => Task.CompletedTask;
        public Task UpdateAsync(User entity) => Task.CompletedTask;
        public Task DeleteAsync(User entity) => Task.CompletedTask;

        public Task<User?> GetByEmailAsync(string email) => Task.FromResult<User?>(null);
        public Task<bool> EmailExistsAsync(string email) => Task.FromResult(false);
        public Task<User?> GetByUsernameAsync(string username) => Task.FromResult<User?>(null);
        public Task<bool> UsernameExistsAsync(string username) => Task.FromResult(false);
        public Task<User?> GetByAppleUserIdAsync(string appleUserId) => Task.FromResult<User?>(null);
        public Task<bool?> GetOnboardingCompletionAsync(int userId) => Task.FromResult<bool?>(null);
        public Task<UserProfile?> GetProfileAsync(int userId) => Task.FromResult<UserProfile?>(null);
        public Task AddProfileAsync(UserProfile profile) => Task.CompletedTask;
        public Task UpdateProfileAsync(UserProfile profile) => Task.CompletedTask;
        public Task AddSnapshotAsync(UserProfileSnapshot snapshot) => Task.CompletedTask;
    }

    private static FoodItem CreateFood(int id, string name, float calories = 100f, float protein = 10f) => new()
    {
        Id = id,
        Name = name,
        Calories = calories,
        Protein = protein,
        Carbs = 5f,
        Fats = 2f,
        ServingSize = 100f,
        ServingSizeUnit = "g"
    };

    [Fact]
    public async Task FoodLookupService_SearchAsync_ReturnsEmpty_WhenQueryIsWhitespace()
    {
        var repo = new StubNutritionRepository();
        var sync = new StubFoodSyncService();
        var lookup = new FoodLookupService(repo, sync);

        var result = await lookup.SearchAsync("   ");

        Assert.Empty(result);
        Assert.Equal(0, repo.SearchCalls);
        Assert.Equal(0, sync.SearchRemoteFoodsCalls);
    }

    [Fact]
    public async Task FoodLookupService_SearchAsync_ReturnsLocal_WhenLocalMatchesExist()
    {
        var repo = new StubNutritionRepository
        {
            FoodsToReturn = [CreateFood(1, "Chicken Breast")]
        };
        var sync = new StubFoodSyncService();
        var lookup = new FoodLookupService(repo, sync);

        var result = await lookup.SearchAsync("chicken");

        Assert.Single(result);
        Assert.Equal("Chicken Breast", result[0].Name);
        Assert.Equal(1, repo.SearchCalls);
        Assert.Equal(0, sync.SearchRemoteFoodsCalls);
    }

    [Fact]
    public async Task FoodLookupService_SearchAsync_FallsBackToOnline_WhenLocalIsEmpty()
    {
        var repo = new StubNutritionRepository();
        var sync = new StubFoodSyncService
        {
            RemoteFoodsToReturn = [CreateFood(2, "Oatmeal")]
        };
        var lookup = new FoodLookupService(repo, sync);

        var result = await lookup.SearchAsync("oats");

        Assert.Single(result);
        Assert.Equal("Oatmeal", result[0].Name);
        Assert.Equal(1, repo.SearchCalls);
        Assert.Equal(1, sync.SearchRemoteFoodsCalls);
    }

    [Fact]
    public async Task FoodLookupService_SearchLocalAsync_PaginatesCorrectly()
    {
        var repo = new StubNutritionRepository
        {
            FoodsToReturn =
            [
                CreateFood(1, "Apple Red"),
                CreateFood(2, "Apple Green"),
                CreateFood(3, "Apple Fuji")
            ]
        };
        var lookup = new FoodLookupService(repo);

        var page1 = await lookup.SearchLocalAsync("apple", pageSize: 2, pageNumber: 1);
        var page2 = await lookup.SearchLocalAsync("apple", pageSize: 2, pageNumber: 2);

        Assert.Equal(2, page1.Count);
        Assert.Single(page2);
    }

    [Fact]
    public async Task NutritionService_SearchFoodItemsAsync_DelegatesToFoodSyncService()
    {
        var repo = new StubNutritionRepository();
        var sync = new StubFoodSyncService
        {
            SyncResultsToReturn = [CreateFood(10, "Salmon Fillet")]
        };
        var userRepo = new StubUserRepository();
        var calc = new CalorieCalculator();
        var nutritionService = new NutritionService(userRepo, repo, calc, foodSyncService: sync);

        var results = await nutritionService.SearchFoodItemsAsync("salmon");

        Assert.Single(results);
        Assert.Equal("Salmon Fillet", results[0].Name);
        Assert.Equal(1, sync.SearchCalls);
        Assert.Equal(0, repo.SearchCalls);
    }

    [Fact]
    public async Task NutritionService_SearchFoodsAsync_FallsBackToLookupService_WhenSyncThrows()
    {
        var repo = new StubNutritionRepository
        {
            FoodsToReturn = [CreateFood(20, "Greek Yogurt")]
        };
        var sync = new StubFoodSyncService
        {
            ThrowInvalidOperationOnSearch = true
        };
        var userRepo = new StubUserRepository();
        var calc = new CalorieCalculator();
        var nutritionService = new NutritionService(userRepo, repo, calc, foodSyncService: sync);

        var results = await nutritionService.SearchFoodsAsync("yogurt");

        Assert.Single(results);
        Assert.Equal("Greek Yogurt", results[0].Name);
        Assert.Equal(1, sync.SearchCalls);
        Assert.Equal(1, repo.SearchCalls);
    }
}
