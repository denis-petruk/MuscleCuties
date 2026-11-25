namespace MuscleCuties.Core.Services;

/// <summary>The warm-up the app runs on start, so the first screen does not pay for it.</summary>
public interface IAppPreloadService
{
    DateTime LastLoadedDate { get; }
    Task PreloadAllAsync();
    Task PreloadDashboardAsync();
    Task PreloadRemainingAsync();
    Task RefreshAllAsync();
    void InvalidateAll();
    void InvalidateDashboard();
    void InvalidateWorkout();
}
