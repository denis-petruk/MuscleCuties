using System.Diagnostics;
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
    private readonly IReferenceDataPreparationService _referenceDataPreparation;

    public DateTime LastLoadedDate { get; private set; } = DateTime.MinValue;

    public AppPreloadService(
        DashboardViewModel dashboard,
        CycleViewModel cycle,
        WorkoutViewModel workout,
        NutritionViewModel nutrition,
        ProfileViewModel profile,
        IReferenceDataPreparationService referenceDataPreparation)
    {
        _dashboard = dashboard;
        _cycle = cycle;
        _workout = workout;
        _nutrition = nutrition;
        _profile = profile;
        _referenceDataPreparation = referenceDataPreparation;
    }

    public async Task PreloadAllAsync()
    {
        await PreloadDashboardAsync();
        await PreloadRemainingAsync();
    }

    public Task PreloadDashboardAsync() => ExecuteAsync(_dashboard, requiresWorkoutReferenceData: true);

    public async Task PreloadRemainingAsync()
    {
        var results = await Task.WhenAll(
            ExecuteAsync(_cycle),
            ExecuteAsync(_workout, requiresWorkoutReferenceData: true),
            ExecuteAsync(_nutrition),
            ExecuteAsync(_profile));

        if (results.All(succeeded => succeeded) && !_dashboard.IsLoadError)
            LastLoadedDate = DateTime.Today;
        else
            LastLoadedDate = DateTime.MinValue;

        // Food and template data is only needed when the user searches or asks
        // for a suggestion. Start it after the page loads; those actions await
        // the same preparation task if the user reaches them first.
        _ = PrepareNutritionInBackgroundAsync();
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
    public void InvalidateWorkout() => _workout.Invalidate();

    private async Task PrepareNutritionInBackgroundAsync()
    {
        try
        {
            await _referenceDataPreparation.EnsureNutritionReadyAsync();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Preload] Nutrition preparation failed: {ex}");
        }
    }

    private async Task<bool> ExecuteAsync(IPageLoadAware page, bool requiresWorkoutReferenceData = false)
    {
        try
        {
            if (requiresWorkoutReferenceData)
                await _referenceDataPreparation.EnsureWorkoutReadyAsync();
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
