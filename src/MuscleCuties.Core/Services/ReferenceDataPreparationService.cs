using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.ViewModels.Common;

namespace MuscleCuties.Core.Services;

public sealed class ReferenceDataPreparationService : IReferenceDataPreparationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly object _taskLock = new();
    private Task? _workoutPreparation;
    private Task? _nutritionPreparation;

    public ReferenceDataPreparationService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public Task EnsureWorkoutReadyAsync()
    {
        lock (_taskLock)
        {
            if (_workoutPreparation is null || _workoutPreparation.IsFaulted || _workoutPreparation.IsCanceled)
                _workoutPreparation = PrepareAsync(database => database.SeedWorkoutReferenceDataAsync());

            return _workoutPreparation;
        }
    }

    public Task EnsureNutritionReadyAsync()
    {
        lock (_taskLock)
        {
            if (_nutritionPreparation is null || _nutritionPreparation.IsFaulted || _nutritionPreparation.IsCanceled)
                _nutritionPreparation = PrepareAsync(database => database.SeedNutritionReferenceDataAsync());

            return _nutritionPreparation;
        }
    }

    private Task PrepareAsync(Func<AppDatabase, Task> seedAsync)
    {
        return DataLoadScheduler.RunAsync(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            await seedAsync(scope.ServiceProvider.GetRequiredService<AppDatabase>());
        });
    }
}
