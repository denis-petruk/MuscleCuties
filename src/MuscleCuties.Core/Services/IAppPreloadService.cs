namespace MuscleCuties.Core.Services;

public interface IAppPreloadService
{
    Task PreloadAllAsync();
    Task PreloadDashboardAsync();
    Task PreloadCycleAsync();
    Task PreloadNutritionAsync();
    Task PreloadProfileAsync();
    Task PreloadRemainingAsync();
    Task PreloadWorkoutAsync();
    Task RefreshAllAsync();
    void InvalidateAll();
    void InvalidateDashboard();
    void InvalidateNutrition();
    void InvalidateWorkout();
    DateTime LastLoadedDate { get; }
}
