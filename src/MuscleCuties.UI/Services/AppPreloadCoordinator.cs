using MuscleCuties.Core.Services;
using MuscleCuties.UI.ViewModels.Common;
using MuscleCuties.UI.ViewModels.Cycle;
using MuscleCuties.UI.ViewModels.Dashboard;
using MuscleCuties.UI.ViewModels.Nutrition;
using MuscleCuties.UI.ViewModels.Profile;
using MuscleCuties.UI.ViewModels.Workout;

namespace MuscleCuties.UI.Services;

/// <summary>Warms the five tab view models after sign-in so a tab is never opened onto an empty screen.</summary>
/// The dashboard loads first; the hidden tabs follow one at a time so they do not compete with it.</summary>
public sealed class AppPreloadCoordinator : IAppPreloadService
{
    private readonly CycleViewModel _cycle;
    private readonly DashboardViewModel _dashboard;
    private readonly NutritionViewModel _nutrition;
    private readonly ProfileViewModel _profile;
    private readonly WorkoutViewModel _workout;

    /// <summary>Takes the view models whose data it preloads.</summary>
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

    /// <summary>The day the last full preload finished, or the minimum when any tab failed; the day-change</summary>
    /// check compares against it.</summary>
    public DateTime LastLoadedDate { get; private set; } = DateTime.MinValue;

    /// <summary>Marks every tab stale, so the next visit reloads instead of showing yesterday's data.</summary>
    public void InvalidateAll()
    {
        LastLoadedDate = DateTime.MinValue;
        _dashboard.Invalidate();
        _cycle.Invalidate();
        _workout.Invalidate();
        _nutrition.Invalidate();
        _profile.Invalidate();
    }

    /// <summary>Marks only the dashboard stale, after an edit that changes nothing else.</summary>
    public void InvalidateDashboard()
    {
        _dashboard.Invalidate();
    }

    /// <summary>Marks only the workout tab stale.</summary>
    public void InvalidateWorkout()
    {
        _workout.Invalidate();
    }

    /// <summary>Loads the dashboard, then the remaining tabs, in that order.</summary>
    public async Task PreloadAllAsync()
    {
        await PreloadDashboardAsync();
        await PreloadRemainingAsync();
    }

    /// <summary>Loads the visible tab first, because that is what the person is looking at.</summary>
    public Task PreloadDashboardAsync()
    {
        return ExecuteAsync(_dashboard);
    }

    /// <summary>Loads the four hidden tabs sequentially and records the load day only if all of them, and the</summary>
    /// dashboard, succeeded.</summary>
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

    /// <summary>Discards everything and loads it again, for pull-to-refresh and post-sign-in recovery.</summary>
    public async Task RefreshAllAsync()
    {
        InvalidateAll();
        await PreloadAllAsync();
    }

    /// <summary>Runs one page's load and reports whether it succeeded. A failure is recorded on the page and</summary>
    /// logged by type only, so preloading never throws into the startup path.</summary>
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
