using MuscleCuties.Core.Data.Sqlite;

namespace MuscleCuties.Core.Data;

public partial class AppDatabase
{
    public async Task SeedReferenceDataAsync()
    {
        await SeedQuizQuestionsAsync();
        await SeedStarterFoodItemsAsync();
        await SeedSystemMealTemplatesAsync();
        await SeedStarterExercisesAsync();
    }

    public async Task ResetAndSeedDebugDatabaseAsync()
    {
#if DEBUG
        ChangeTracker.Clear();
        await Database.EnsureDeletedAsync();
        await Database.EnsureCreatedAsync();
        await SqliteSchemaMaintenance.RepairAsync(this);
        await SeedReferenceDataAsync();
        ChangeTracker.Clear();
#else
        throw new InvalidOperationException("Debug database reset is only available in DEBUG builds.");
#endif
    }
}
