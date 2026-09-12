using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
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

    public DateTime LastLoadedDate { get; private set; } = DateTime.MinValue;

    public AppPreloadService(
        DashboardViewModel dashboard,
        CycleViewModel cycle,
        WorkoutViewModel workout,
        NutritionViewModel nutrition,
        ProfileViewModel profile)
    {
        _dashboard = dashboard;
        _cycle = cycle;
        _workout = workout;
        _nutrition = nutrition;
        _profile = profile;
    }

    public async Task PreloadAllAsync()
    {
        await PreloadDashboardAsync();
        await PreloadRemainingAsync();
    }

    public async Task PreloadDashboardAsync()
    {
        await ExecuteAsync(_dashboard.LoadDataCommand);
        LastLoadedDate = DateTime.Today;
    }

    public Task PreloadCycleAsync() => ExecuteAsync(_cycle.LoadDataCommand);

    public Task PreloadNutritionAsync() => ExecuteAsync(_nutrition.LoadDataCommand);

    public Task PreloadProfileAsync() => ExecuteAsync(_profile.LoadDataCommand);

    public Task PreloadWorkoutAsync() => ExecuteAsync(_workout.LoadDataCommand);

    public async Task PreloadRemainingAsync()
    {
        await Task.WhenAll(
            TryPreloadAsync(PreloadCycleAsync, "Cycle"),
            TryPreloadAsync(PreloadWorkoutAsync, "Workout"),
            TryPreloadAsync(PreloadNutritionAsync, "Nutrition"),
            TryPreloadAsync(PreloadProfileAsync, "Profile"));

        LastLoadedDate = DateTime.Today;
    }

    public async Task RefreshAllAsync()
    {
        InvalidateAll();
        await PreloadAllAsync();
    }

    public void InvalidateAll()
    {
        _dashboard.Invalidate();
        _cycle.Invalidate();
        _workout.Invalidate();
        _nutrition.Invalidate();
        _profile.Invalidate();
    }

    public void InvalidateDashboard() => _dashboard.Invalidate();
    public void InvalidateNutrition() => _nutrition.Invalidate();
    public void InvalidateWorkout() => _workout.Invalidate();

    private static Task ExecuteAsync(IAsyncRelayCommand command)
    {
        return command.ExecuteAsync(null);
    }

    private static async Task TryPreloadAsync(Func<Task> preload, string pageName)
    {
        try
        {
            await preload();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Preload] {pageName} failed: {ex.Message}");
        }
    }
}
