using System.Diagnostics;
using MuscleCuties.Core.Services;
using MuscleCuties.UI.ViewModels.Common;
using MuscleCuties.UI.ViewModels.Cycle;
using MuscleCuties.UI.ViewModels.Dashboard;
using MuscleCuties.UI.ViewModels.Nutrition;
using MuscleCuties.UI.ViewModels.Profile;
using MuscleCuties.UI.ViewModels.Workout;

namespace MuscleCuties.UI.Services;

public sealed class AppPreloadCoordinator : IAppPreloadService
{
    private readonly CycleViewModel _cycle;
    private readonly DashboardViewModel _dashboard;
    private readonly NutritionViewModel _nutrition;
    private readonly ProfileViewModel _profile;
    private readonly WorkoutViewModel _workout;

    public AppPreloadCoordinator(
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

    public DateTime LastLoadedDate { get; private set; } = DateTime.MinValue;

    public void InvalidateAll()
    {
        LastLoadedDate = DateTime.MinValue;
        _dashboard.Invalidate();
        _cycle.Invalidate();
        _workout.Invalidate();
        _nutrition.Invalidate();
        _profile.Invalidate();
    }

    public void InvalidateDashboard()
    {
        _dashboard.Invalidate();
    }

    public void InvalidateWorkout()
    {
        _workout.Invalidate();
    }

    public async Task PreloadAllAsync()
    {
        await PreloadDashboardAsync();
        await PreloadRemainingAsync();
    }

    public Task PreloadDashboardAsync()
    {
        return ExecuteAsync(_dashboard);
    }

    public async Task PreloadRemainingAsync()
    {
        // Load hidden tabs sequentially so they do not compete with the active tab.
        var cycleLoaded = await ExecuteAsync(_cycle);
        var workoutLoaded = await ExecuteAsync(_workout);
        var nutritionLoaded = await ExecuteAsync(_nutrition);
        var profileLoaded = await ExecuteAsync(_profile);

        if (cycleLoaded && workoutLoaded && nutritionLoaded && profileLoaded && !_dashboard.IsLoadError)
            LastLoadedDate = DateTime.Today;
        else
            LastLoadedDate = DateTime.MinValue;

    }

    public async Task RefreshAllAsync()
    {
        InvalidateAll();
        await PreloadAllAsync();
    }

    private static async Task<bool> ExecuteAsync(IPageLoadAware page)
    {
        try
        {
            await page.LoadDataCommand.ExecuteAsync(null);
            return !page.IsLoadError;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Preload] {page.GetType().Name} failed ({ex.GetType().Name}).");
            page.IsLoadError = true;
            return false;
        }
    }
}
