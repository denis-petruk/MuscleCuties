using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.ViewModels.Common;
using MuscleCuties.Core.ViewModels.Cycle;
using MuscleCuties.Core.ViewModels.Dashboard;
using MuscleCuties.Core.ViewModels.Nutrition;
using MuscleCuties.Core.ViewModels.Profile;
using MuscleCuties.Core.ViewModels.Workout;

namespace MuscleCuties.Core.Services;

public sealed class AppPreloadService : IAppPreloadService
{
    private readonly DashboardViewModel _dashboard;
    private readonly CycleViewModel _cycle;
    private readonly WorkoutViewModel _workout;
    private readonly NutritionViewModel _nutrition;
    private readonly ProfileViewModel _profile;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly object _preparationLock = new();
    private Task? _databasePreparation;

    public DateTime LastLoadedDate { get; private set; } = DateTime.MinValue;

    public AppPreloadService(
        DashboardViewModel dashboard,
        CycleViewModel cycle,
        WorkoutViewModel workout,
        NutritionViewModel nutrition,
        ProfileViewModel profile,
        IServiceScopeFactory scopeFactory)
    {
        _dashboard = dashboard;
        _cycle = cycle;
        _workout = workout;
        _nutrition = nutrition;
        _profile = profile;
        _scopeFactory = scopeFactory;
    }

    public async Task PreloadAllAsync()
    {
        await PreloadDashboardAsync();
        await PreloadRemainingAsync();
    }

    public Task PreloadDashboardAsync() => ExecuteAsync(_dashboard);

    public Task PreloadCycleAsync() => ExecuteAsync(_cycle);

    public Task PreloadNutritionAsync() => ExecuteAsync(_nutrition);

    public Task PreloadProfileAsync() => ExecuteAsync(_profile);

    public Task PreloadWorkoutAsync() => ExecuteAsync(_workout);

    public async Task PreloadRemainingAsync()
    {
        var results = await Task.WhenAll(
            ExecuteAsync(_cycle),
            ExecuteAsync(_workout),
            ExecuteAsync(_nutrition),
            ExecuteAsync(_profile));

        if (results.All(succeeded => succeeded) && !_dashboard.IsLoadError)
            LastLoadedDate = DateTime.Today;
        else
            LastLoadedDate = DateTime.MinValue;
    }

    public async Task RefreshAllAsync()
    {
        InvalidateAll();
        await PreloadAllAsync();
    }

    public void InvalidateAll()
    {
        LastLoadedDate = DateTime.MinValue;
        _dashboard.Invalidate();
        _cycle.Invalidate();
        _workout.Invalidate();
        _nutrition.Invalidate();
        _profile.Invalidate();
    }

    public void InvalidateDashboard() => _dashboard.Invalidate();
    public void InvalidateNutrition() => _nutrition.Invalidate();
    public void InvalidateWorkout() => _workout.Invalidate();

    private Task EnsureDatabaseReadyAsync()
    {
        lock (_preparationLock)
        {
            // Share preparation across startup/login/onboarding preloads. A
            // failed attempt is retryable; no page queries run before it succeeds.
            if (_databasePreparation is null || _databasePreparation.IsFaulted || _databasePreparation.IsCanceled)
            {
                _databasePreparation = DataLoadScheduler.RunAsync(async () =>
                {
                    using var scope = _scopeFactory.CreateScope();
                    var database = scope.ServiceProvider.GetRequiredService<AppDatabase>();
                    await database.InitializeStartupAsync();
                    await database.SeedDeferredReferenceDataAsync();
                });
            }

            return _databasePreparation;
        }
    }

    private async Task<bool> ExecuteAsync(IPageLoadAware page)
    {
        try
        {
            await EnsureDatabaseReadyAsync();
            await page.LoadDataCommand.ExecuteAsync(null);
            return !page.IsLoadError;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Preload] {page.GetType().Name} failed: {ex}");
            page.IsLoadError = true;
            return false;
        }
    }
}
